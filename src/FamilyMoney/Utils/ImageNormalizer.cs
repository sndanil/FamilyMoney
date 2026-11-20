using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Svg.Skia;

namespace FamilyMoney.Utils;

public enum ImageFitMode
{
    /// <summary>Центральный квадрат: заполнить плитку, лишнее обрезать.</summary>
    Fill,

    /// <summary>Вписать целиком: сохранить пропорции, добавить поля.</summary>
    Fit,
}

public sealed class ImageNormalizationResult
{
    public byte[]? Data { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public long Bytes => Data?.LongLength ?? 0;

    public bool HasAlpha { get; init; }

    public int? OriginalWidth { get; init; }

    public int? OriginalHeight { get; init; }

    public long OriginalBytes { get; init; }

    public bool WasNormalized => Data is { Length: > 0 } && Error is null;

    public string? Warning { get; init; }

    public string? Error { get; init; }

    public static ImageNormalizationResult Fail(string error, long originalBytes = 0) => new()
    {
        Error = error,
        OriginalBytes = originalBytes,
    };
}

/// <summary>
/// Приводит пользовательскую картинку к квадрату 256×256: EXIF-ориентация,
/// кадрирование/вписывание, высококачественный ресайз и перекодирование.
/// SVG рендерится через Svg.Skia. Внешние ссылки и HEIC не поддерживаются.
/// </summary>
public static class ImageNormalizer
{
    public const int TargetSize = 256;
    public const long MaxInputBytes = 20L * 1024 * 1024;
    public const long MaxSvgBytes = 2L * 1024 * 1024;
    public const long MaxOutputBytes = 96L * 1024;
    public const int MaxDimension = 12_000;
    public const long MaxPixels = 40_000_000;

    public static ImageNormalizationResult Normalize(
        byte[]? input,
        string? fileName = null,
        ImageFitMode fit = ImageFitMode.Fill,
        int targetSize = TargetSize)
    {
        if (input is not { Length: > 0 })
        {
            return ImageNormalizationResult.Fail("Пустой файл.");
        }

        var originalBytes = input.LongLength;
        if (originalBytes > MaxInputBytes)
        {
            return ImageNormalizationResult.Fail(
                "Файл больше 20 МБ. Выберите картинку поменьше.",
                originalBytes);
        }

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        var svg = extension == ".svg" || LooksLikeSvg(input);
        if (svg)
        {
            return NormalizeSvg(input, originalBytes, fit, targetSize);
        }

        if (IsHeif(input) || extension is ".heic" or ".heif" or ".avif")
        {
            return ImageNormalizationResult.Fail(
                "HEIC/HEIF не поддерживается. Пересохраните файл в JPEG или PNG.",
                originalBytes);
        }

        try
        {
            using var data = SKData.CreateCopy(input);
            using var codec = SKCodec.Create(data);
            if (codec is null)
            {
                return ImageNormalizationResult.Fail(
                    "Не удалось прочитать изображение. Попробуйте PNG или JPEG.",
                    originalBytes);
            }

            var info = codec.Info;
            if (!IsWithinLimits(info.Width, info.Height))
            {
                return ImageNormalizationResult.Fail(
                    "Слишком большая картинка: больше 12 000 px или 40 Мп.",
                    originalBytes);
            }

            using var decoded = SKBitmap.Decode(codec);
            if (decoded is null)
            {
                return ImageNormalizationResult.Fail(
                    "Не удалось декодировать изображение. Попробуйте PNG или JPEG.",
                    originalBytes);
            }

            using var oriented = ApplyOrientation(decoded, codec.EncodedOrigin);
            var hasAlpha = info.AlphaType != SKAlphaType.Opaque;
            return EncodeResult(oriented, fit, targetSize, originalBytes, hasAlpha);
        }
        catch (Exception)
        {
            return ImageNormalizationResult.Fail(
                "Не удалось прочитать изображение. Попробуйте PNG или JPEG.",
                originalBytes);
        }
    }

    /// <summary>
    /// Нужно ли пересохранить старую картинку. Проверяет только заголовок,
    /// без полного декодирования.
    /// </summary>
    public static bool NeedsNormalization(byte[]? data)
    {
        if (data is not { Length: > 0 })
        {
            return false;
        }

        if (data.LongLength > MaxOutputBytes)
        {
            return true;
        }

        if (LooksLikeSvg(data))
        {
            return true;
        }

        try
        {
            using var skData = SKData.CreateCopy(data);
            using var codec = SKCodec.Create(skData);
            if (codec is null)
            {
                return false;
            }

            var info = codec.Info;
            return info.Width > TargetSize || info.Height > TargetSize;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Старую картинку при сохранении вписываем целиком, без обрезки.
    /// Если нормализовать не удалось — возвращаем исходные байты.
    /// </summary>
    public static async Task<byte[]?> NormalizeLegacyAsync(
        byte[]? data,
        CancellationToken cancellationToken = default)
    {
        if (data is not { Length: > 0 } || !NeedsNormalization(data))
        {
            return data;
        }

        var result = await Task.Run(
            () => Normalize(data, null, ImageFitMode.Fit),
            cancellationToken).ConfigureAwait(false);

        return result.WasNormalized ? result.Data : data;
    }

    private static ImageNormalizationResult NormalizeSvg(
        byte[] input,
        long originalBytes,
        ImageFitMode fit,
        int targetSize)
    {
        if (originalBytes > MaxSvgBytes)
        {
            return ImageNormalizationResult.Fail(
                "SVG больше 2 МБ. Упростите файл или сохраните его в PNG.",
                originalBytes);
        }

        string text;
        try
        {
            text = Encoding.UTF8.GetString(input);
        }
        catch (Exception)
        {
            return ImageNormalizationResult.Fail("Не удалось прочитать SVG.", originalBytes);
        }

        if (HasUnsafeSvgContent(text))
        {
            return ImageNormalizationResult.Fail(
                "В SVG есть скрипты, внешние ссылки или встроенные изображения. " +
                "Сохраните картинку внутрь SVG и попробуйте снова.",
                originalBytes);
        }

        try
        {
            using var stream = new MemoryStream(input, writable: false);
            var svg = new SKSvg();
            var picture = svg.Load(stream);
            if (picture is null)
            {
                return ImageNormalizationResult.Fail("Не удалось разобрать SVG.", originalBytes);
            }

            var bounds = picture.CullRect;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return ImageNormalizationResult.Fail(
                    "У SVG нет размеров или viewBox.",
                    originalBytes);
            }

            using var rendered = new SKBitmap(
                targetSize,
                targetSize,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using (var canvas = new SKCanvas(rendered))
            {
                canvas.Clear(SKColors.Transparent);
                var scale = Math.Min(targetSize / bounds.Width, targetSize / bounds.Height);
                var dx = (targetSize - bounds.Width * scale) / 2f - bounds.Left * scale;
                var dy = (targetSize - bounds.Height * scale) / 2f - bounds.Top * scale;
                canvas.Translate(dx, dy);
                canvas.Scale(scale);
                canvas.DrawPicture(picture);
            }

            return EncodeResult(
                rendered,
                ImageFitMode.Fit,
                targetSize,
                originalBytes,
                hasAlpha: true,
                originalWidth: (int)Math.Ceiling(bounds.Width),
                originalHeight: (int)Math.Ceiling(bounds.Height));
        }
        catch (Exception)
        {
            return ImageNormalizationResult.Fail("Не удалось разобрать SVG.", originalBytes);
        }
    }

    private static ImageNormalizationResult EncodeResult(
        SKBitmap source,
        ImageFitMode fit,
        int targetSize,
        long originalBytes,
        bool hasAlpha,
        int? originalWidth = null,
        int? originalHeight = null)
    {
        using var square = ComposeSquare(source, fit, targetSize, hasAlpha);
        var resultSize = targetSize;
        byte[]? best = null;

        foreach (var size in new[] { targetSize, Math.Min(192, targetSize), Math.Min(128, targetSize) }
                     .Distinct())
        {
            using var candidate = size == targetSize ? null : ResizeSquare(square, size, hasAlpha);
            var bitmap = candidate ?? square;
            var qualities = hasAlpha ? new[] { 100 } : new[] { 85, 75 };
            foreach (var quality in qualities)
            {
                var data = Encode(bitmap, hasAlpha, quality);
                best = data;
                resultSize = size;

                if (data.LongLength <= MaxOutputBytes)
                {
                    break;
                }
            }

            if (best is not null && best.LongLength <= MaxOutputBytes)
            {
                break;
            }
        }

        return new ImageNormalizationResult
        {
            Data = best,
            Width = resultSize,
            Height = resultSize,
            HasAlpha = hasAlpha,
            OriginalWidth = originalWidth ?? source.Width,
            OriginalHeight = originalHeight ?? source.Height,
            OriginalBytes = originalBytes,
        };
    }

    private static SKBitmap ComposeSquare(
        SKBitmap source,
        ImageFitMode fit,
        int size,
        bool hasAlpha)
    {
        var result = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(result);
        canvas.Clear(hasAlpha ? SKColors.Transparent : SKColors.White);
        using var image = SKImage.FromBitmap(source);
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        if (fit == ImageFitMode.Fill)
        {
            var side = Math.Min(source.Width, source.Height);
            var sourceRect = SKRect.Create(
                (source.Width - side) / 2f,
                (source.Height - side) / 2f,
                side,
                side);
            var targetRect = SKRect.Create(0, 0, size, size);
            canvas.DrawImage(image, sourceRect, targetRect, sampling);
        }
        else
        {
            var scale = Math.Min(size / (float)source.Width, size / (float)source.Height);
            var width = source.Width * scale;
            var height = source.Height * scale;
            var targetRect = SKRect.Create(
                (size - width) / 2f,
                (size - height) / 2f,
                width,
                height);
            canvas.DrawImage(image, targetRect, sampling);
        }

        return result;
    }

    private static SKBitmap ResizeSquare(SKBitmap source, int size, bool hasAlpha)
    {
        var result = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(result);
        canvas.Clear(hasAlpha ? SKColors.Transparent : SKColors.White);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(
            image,
            SKRect.Create(0, 0, size, size),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        return result;
    }

    private static byte[] Encode(SKBitmap bitmap, bool hasAlpha, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = hasAlpha
            ? image.Encode(SKEncodedImageFormat.Png, 100)
            : image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data?.ToArray() ?? [];
    }

    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        var swap = origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

        var width = swap ? source.Height : source.Width;
        var height = swap ? source.Width : source.Height;
        var result = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(width / 2f, height / 2f);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.RotateDegrees(270);
                break;
        }

        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    private static bool IsWithinLimits(int width, int height)
    {
        return width > 0
            && height > 0
            && width <= MaxDimension
            && height <= MaxDimension
            && (long)width * height <= MaxPixels;
    }

    private static bool LooksLikeSvg(byte[] input)
    {
        var limit = Math.Min(input.Length, 2048);
        var text = Encoding.UTF8.GetString(input, 0, limit);
        var trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return trimmed.StartsWith('<') && text.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHeif(byte[] input)
    {
        if (input.Length < 12
            || input[4] != 'f'
            || input[5] != 't'
            || input[6] != 'y'
            || input[7] != 'p')
        {
            return false;
        }

        var brand = Encoding.ASCII.GetString(input, 8, 4);
        return brand is "heic" or "heix" or "hevc" or "heim" or "heis"
            or "mif1" or "msf1" or "avif";
    }

    private static bool HasUnsafeSvgContent(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("<script", StringComparison.Ordinal)
            || lower.Contains("<image", StringComparison.Ordinal))
        {
            return true;
        }

        return lower.Contains("href=\"http", StringComparison.Ordinal)
            || lower.Contains("href='http", StringComparison.Ordinal)
            || lower.Contains("href=\"file", StringComparison.Ordinal)
            || lower.Contains("href='file", StringComparison.Ordinal)
            || lower.Contains("url(http", StringComparison.Ordinal)
            || lower.Contains("url(file", StringComparison.Ordinal);
    }
}
