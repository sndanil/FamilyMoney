using FamilyMoney.Models.Sync;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace FamilyMoney.Sync.Peer;

/// <summary>
/// TCP/TLS-клиент peer-сессии. Проверяет сертификат по отпечатку из QR.
/// </summary>
public sealed class PeerSyncClient : IPeerTransport, IAsyncDisposable
{
    private readonly TcpClient _tcp;
    private readonly SslStream _stream;

    private PeerSyncClient(TcpClient tcp, SslStream stream)
    {
        _tcp = tcp;
        _stream = stream;
    }

    public static async Task<PeerSyncClient> ConnectAsync(
        PeerServerEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = endpoint.Fingerprint;
        var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken);
            tcp.NoDelay = true;

            var ssl = new SslStream(
                tcp.GetStream(),
                leaveInnerStreamOpen: false,
                (_, certificate, _, _) =>
                    certificate != null
                    && PeerCertificate.Matches(certificate, fingerprint));

            try
            {
                await ssl.AuthenticateAsClientAsync(
                    new SslClientAuthenticationOptions
                    {
                        TargetHost = "familymoney-peer",
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    },
                    cancellationToken);

                return new PeerSyncClient(tcp, ssl);
            }
            catch
            {
                await ssl.DisposeAsync();
                throw;
            }
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    public async Task<PeerHelloAck> HelloAsync(
        PeerHello hello,
        CancellationToken cancellationToken = default)
    {
        await PeerWire.WriteAsync(_stream, PeerWireTypes.Hello, hello, cancellationToken);
        var envelope = await ReadExpectedAsync(PeerWireTypes.HelloAck, cancellationToken);
        return PeerWire.ReadPayload<PeerHelloAck>(envelope);
    }

    public async Task<PeerChangesResponse> PullAsync(
        PeerPullRequest request,
        CancellationToken cancellationToken = default)
    {
        await PeerWire.WriteAsync(_stream, PeerWireTypes.Pull, request, cancellationToken);
        var envelope = await ReadExpectedAsync(PeerWireTypes.Changes, cancellationToken);
        return PeerWire.ReadPayload<PeerChangesResponse>(envelope);
    }

    public async Task<PeerPushAck> PushAsync(
        PeerPushRequest request,
        Func<SyncImageRecord, CancellationToken, Task<Stream?>> openImage,
        CancellationToken cancellationToken = default)
    {
        await PeerWire.WriteAsync(_stream, PeerWireTypes.Push, request, cancellationToken);

        while (true)
        {
            var envelope = await ReadEnvelopeAsync(cancellationToken);
            switch (envelope.Type)
            {
                case PeerWireTypes.PushAck:
                    return PeerWire.ReadPayload<PeerPushAck>(envelope);
                case PeerWireTypes.ImageRequest:
                    await RespondWithImageAsync(envelope, openImage, cancellationToken);
                    break;
                default:
                    ThrowForError(envelope);
                    throw new PeerSyncException(
                        PeerErrorCodes.Protocol,
                        $"Неожиданное сообщение: {envelope.Type}.");
            }
        }
    }

    public async Task<Stream?> DownloadImageAsync(
        SyncImageRecord image,
        CancellationToken cancellationToken = default)
    {
        await PeerWire.WriteAsync(
            _stream,
            PeerWireTypes.ImageRequest,
            new PeerImageRequest
            {
                EntityId = image.EntityId,
                FileName = image.FileName,
                DeletedAt = image.DeletedAt,
            },
            cancellationToken);

        var envelope = await ReadExpectedAsync(PeerWireTypes.ImageResponse, cancellationToken);
        var header = PeerWire.ReadPayload<PeerImageHeader>(envelope);
        if (!header.Found)
        {
            return null;
        }

        var bytes = await PeerWire.ReadBytesAsync(_stream, header.Length, cancellationToken);
        return new MemoryStream(bytes, writable: false);
    }

    private async Task RespondWithImageAsync(
        PeerEnvelope envelope,
        Func<SyncImageRecord, CancellationToken, Task<Stream?>> openImage,
        CancellationToken cancellationToken)
    {
        var request = PeerWire.ReadPayload<PeerImageRequest>(envelope);
        var record = new SyncImageRecord
        {
            EntityId = request.EntityId,
            FileName = request.FileName,
            DeletedAt = request.DeletedAt,
        };

        await using var image = await openImage(record, cancellationToken);
        if (image == null)
        {
            await PeerWire.WriteAsync(
                _stream,
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
            _stream,
            PeerWireTypes.ImageResponse,
            new PeerImageHeader
            {
                Found = true,
                FileName = record.FileName,
                Length = (int)memory.Length,
            },
            cancellationToken);
        await PeerWire.WriteBytesAsync(
            _stream,
            memory.ToArray(),
            cancellationToken);
    }

    private async Task<PeerEnvelope> ReadExpectedAsync(
        string type,
        CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(cancellationToken);
        if (envelope.Type != type)
        {
            ThrowForError(envelope);
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                $"Ожидалось сообщение {type}, пришло {envelope.Type}.");
        }

        return envelope;
    }

    private async Task<PeerEnvelope> ReadEnvelopeAsync(
        CancellationToken cancellationToken)
    {
        var envelope = await PeerWire.ReadAsync(_stream, cancellationToken);
        ThrowForError(envelope);
        return envelope;
    }

    private static void ThrowForError(PeerEnvelope envelope)
    {
        if (envelope.Type != PeerWireTypes.Error)
        {
            return;
        }

        var error = PeerWire.ReadPayload<PeerError>(envelope);
        throw new PeerSyncException(error.Code, error.Message);
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
        _tcp.Dispose();
    }
}
