using FamilyMoney.Utils;
using System.Buffers.Binary;
using System.Text.Json;

namespace FamilyMoney.Sync.Peer;

/// <summary>
/// Простой framed-протокол: 4 байта big-endian длины + JSON-конверт.
/// Картинки передаются отдельным заголовком и сырыми байтами после него.
/// </summary>
public static class PeerWire
{
    public const int MaxImageBytes = 8 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.CamelCaseCompact;

    public static async Task WriteAsync<T>(
        Stream stream,
        string type,
        T payload,
        CancellationToken cancellationToken = default)
    {
        var envelope = new PeerEnvelope
        {
            Type = type,
            Payload = JsonSerializer.SerializeToElement(payload, JsonOptions),
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        if (json.Length == 0 || json.Length > PeerProtocol.MaxFrameBytes)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                "Слишком большой кадр протокола.");
        }

        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, json.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(json, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<PeerEnvelope> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length <= 0 || length > PeerProtocol.MaxFrameBytes)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                "Некорректная длина кадра протокола.");
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        var envelope = JsonSerializer.Deserialize<PeerEnvelope>(payload, JsonOptions)
            ?? throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                "Пустой кадр протокола.");
        return envelope;
    }

    public static T ReadPayload<T>(PeerEnvelope envelope)
    {
        try
        {
            return envelope.Payload.Deserialize<T>(JsonOptions)
                ?? throw new PeerSyncException(
                    PeerErrorCodes.Protocol,
                    "Пустой payload протокола.");
        }
        catch (JsonException ex)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                "Некорректный payload протокола: " + ex.Message);
        }
    }

    public static async Task WriteBytesAsync(
        Stream stream,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
    {
        if (bytes.Length > MaxImageBytes)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Image,
                "Слишком большая картинка.");
        }

        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<byte[]> ReadBytesAsync(
        Stream stream,
        int length,
        CancellationToken cancellationToken = default)
    {
        if (length < 0 || length > MaxImageBytes)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Image,
                "Некорректный размер картинки.");
        }

        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }

    public static async Task WriteErrorAsync(
        Stream stream,
        string code,
        string message,
        CancellationToken cancellationToken = default)
    {
        await WriteAsync(
            stream,
            PeerWireTypes.Error,
            new PeerError { Code = code, Message = message },
            cancellationToken);
    }
}

public sealed class PeerError
{
    public string Code { get; set; } = PeerErrorCodes.Internal;

    public string Message { get; set; } = string.Empty;
}
