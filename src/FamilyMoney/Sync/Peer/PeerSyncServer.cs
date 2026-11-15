using FamilyMoney.Models.Sync;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace FamilyMoney.Sync.Peer;

/// <summary>
/// TCP/TLS-хост одной peer-сессии. Логика обмена делегируется
/// <see cref="PeerSyncServerEngine"/>; здесь только транспорт и TLS.
/// </summary>
public sealed class PeerSyncServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly PeerSyncServerEngine _engine;
    private readonly X509Certificate2 _certificate;
    private readonly bool _ownsCertificate;
    private readonly ILogger<PeerSyncServer>? _logger;
    private bool _started;

    public PeerSyncServer(
        PeerSyncServerEngine engine,
        IPAddress address,
        int port = 0,
        X509Certificate2? certificate = null,
        ILogger<PeerSyncServer>? logger = null)
    {
        _engine = engine;
        _listener = new TcpListener(address, port);
        _certificate = certificate ?? PeerCertificate.CreateSelfSigned();
        _ownsCertificate = certificate == null;
        _logger = logger;
    }

    public PeerServerEndpoint? Endpoint { get; private set; }

    public PeerServerEndpoint Start()
    {
        if (_started)
        {
            return Endpoint ?? throw new InvalidOperationException("Peer server endpoint is unavailable.");
        }

        _listener.Start();
        _started = true;
        var local = (IPEndPoint)_listener.LocalEndpoint;
        Endpoint = new PeerServerEndpoint(
            local.Address.ToString(),
            local.Port,
            PeerCertificate.GetFingerprint(_certificate));
        return Endpoint;
    }

    public async Task<PeerSessionResult> AcceptOnceAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_started || Endpoint == null)
        {
            throw new InvalidOperationException("Peer server is not started.");
        }

        using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
        client.NoDelay = true;

        await using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        try
        {
            await ssl.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificate = _certificate,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                },
                cancellationToken);

            return await HandleSessionAsync(ssl, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Peer TLS/session failed");
            return new PeerSessionResult(false, ex.Message);
        }
    }

    private async Task<PeerSessionResult> HandleSessionAsync(
        SslStream stream,
        CancellationToken cancellationToken)
    {
        var helloSeen = false;

        while (true)
        {
            PeerEnvelope envelope;
            try
            {
                envelope = await PeerWire.ReadAsync(stream, cancellationToken);
            }
            catch (EndOfStreamException)
            {
                return PeerSessionResult.Completed();
            }

            try
            {
                switch (envelope.Type)
                {
                    case PeerWireTypes.Hello:
                    {
                        var hello = PeerWire.ReadPayload<PeerHello>(envelope);
                        var ack = _engine.Hello(hello);
                        helloSeen = true;
                        await PeerWire.WriteAsync(stream, PeerWireTypes.HelloAck, ack, cancellationToken);
                        break;
                    }
                    case PeerWireTypes.Pull:
                    {
                        RequireHello(helloSeen);
                        var request = PeerWire.ReadPayload<PeerPullRequest>(envelope);
                        var response = _engine.Pull(request);
                        await PeerWire.WriteAsync(stream, PeerWireTypes.Changes, response, cancellationToken);
                        break;
                    }
                    case PeerWireTypes.Push:
                    {
                        RequireHello(helloSeen);
                        var request = PeerWire.ReadPayload<PeerPushRequest>(envelope);
                        var ack = await _engine.PushAsync(
                            request,
                            (image, token) => RequestImageFromClientAsync(stream, image, token),
                            cancellationToken);
                        await PeerWire.WriteAsync(stream, PeerWireTypes.PushAck, ack, cancellationToken);
                        break;
                    }
                    case PeerWireTypes.ImageRequest:
                    {
                        RequireHello(helloSeen);
                        var request = PeerWire.ReadPayload<PeerImageRequest>(envelope);
                        await SendImageAsync(stream, request, cancellationToken);
                        break;
                    }
                    default:
                        throw new PeerSyncException(
                            PeerErrorCodes.Protocol,
                            $"Неизвестный тип сообщения: {envelope.Type}.");
                }
            }
            catch (PeerSyncException ex)
            {
                await PeerWire.WriteErrorAsync(stream, ex.Code, ex.Message, cancellationToken);
                return new PeerSessionResult(false, ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Peer session message failed");
                await PeerWire.WriteErrorAsync(
                    stream,
                    PeerErrorCodes.Internal,
                    ex.Message,
                    cancellationToken);
                return new PeerSessionResult(false, ex.Message);
            }
        }
    }

    private async Task<Stream?> RequestImageFromClientAsync(
        SslStream stream,
        SyncImageRecord image,
        CancellationToken cancellationToken)
    {
        await PeerWire.WriteAsync(
            stream,
            PeerWireTypes.ImageRequest,
            new PeerImageRequest
            {
                EntityId = image.EntityId,
                FileName = image.FileName,
                DeletedAt = image.DeletedAt,
            },
            cancellationToken);

        var envelope = await ReadExpectedAsync(
            stream,
            PeerWireTypes.ImageResponse,
            cancellationToken);
        var header = PeerWire.ReadPayload<PeerImageHeader>(envelope);
        if (!header.Found)
        {
            return null;
        }

        var bytes = await PeerWire.ReadBytesAsync(
            stream,
            header.Length,
            cancellationToken);
        return new MemoryStream(bytes, writable: false);
    }

    private async Task SendImageAsync(
        SslStream stream,
        PeerImageRequest request,
        CancellationToken cancellationToken)
    {
        var record = new SyncImageRecord
        {
            EntityId = request.EntityId,
            FileName = request.FileName,
            DeletedAt = request.DeletedAt,
        };

        await using var image = await _engine.DownloadImageAsync(record, cancellationToken);
        if (image == null)
        {
            await PeerWire.WriteAsync(
                stream,
                PeerWireTypes.ImageResponse,
                new PeerImageHeader { Found = false },
                cancellationToken);
            return;
        }

        using var memory = new MemoryStream();
        await image.CopyToAsync(memory, cancellationToken);
        if (memory.Length > PeerWire.MaxImageBytes)
        {
            throw new PeerSyncException(PeerErrorCodes.Image, "Картинка слишком большая.");
        }

        await PeerWire.WriteAsync(
            stream,
            PeerWireTypes.ImageResponse,
            new PeerImageHeader
            {
                Found = true,
                FileName = record.FileName,
                Length = (int)memory.Length,
            },
            cancellationToken);
        await PeerWire.WriteBytesAsync(
            stream,
            memory.ToArray(),
            cancellationToken);
    }

    private static void RequireHello(bool helloSeen)
    {
        if (!helloSeen)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                "Сначала нужно выполнить hello.");
        }
    }

    private static async Task<PeerEnvelope> ReadExpectedAsync(
        SslStream? stream,
        string type,
        CancellationToken cancellationToken)
    {
        if (stream == null)
        {
            throw new InvalidOperationException("Peer stream is not available.");
        }

        var envelope = await PeerWire.ReadAsync(stream, cancellationToken);
        if (envelope.Type == PeerWireTypes.Error)
        {
            var error = PeerWire.ReadPayload<PeerError>(envelope);
            throw new PeerSyncException(error.Code, error.Message);
        }

        if (envelope.Type != type)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                $"Ожидалось сообщение {type}, пришло {envelope.Type}.");
        }

        return envelope;
    }

    public ValueTask DisposeAsync()
    {
        if (_started)
        {
            _listener.Stop();
            _started = false;
        }

        if (_ownsCertificate)
        {
            _certificate.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
