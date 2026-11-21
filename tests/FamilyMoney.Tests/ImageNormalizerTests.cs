using System;
using System.Text;
using System.Threading.Tasks;
using FamilyMoney.Utils;
using SkiaSharp;
using Xunit;

namespace FamilyMoney.Tests;

public class ImageNormalizerTests
{
    [Fact]
    public void Normalize_Svg_RendersSquarePngWithAlpha()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="60" viewBox="0 0 120 60">
              <rect width="120" height="60" fill="#3390ec"/>
              <circle cx="60" cy="30" r="20" fill="#ffffff"/>
            </svg>
            """;

        var result = ImageNormalizer.Normalize(Encoding.UTF8.GetBytes(svg), "logo.svg");

        Assert.True(result.WasNormalized, result.Error);
        Assert.Equal(256, result.Width);
        Assert.Equal(256, result.Height);
        Assert.True(result.HasAlpha);
        Assert.NotNull(result.Data);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, result.Data![..4]);
        Assert.InRange(result.Bytes, 1, ImageNormalizer.MaxOutputBytes);
    }

    [Fact]
    public void Normalize_OpaqueRaster_DownscalesAndEncodesJpeg()
    {
        var jpeg = CreateRaster(800, 400, SKColors.Orange, SKAlphaType.Opaque, SKEncodedImageFormat.Jpeg);

        var result = ImageNormalizer.Normalize(jpeg, "photo.jpg", ImageFitMode.Fill);

        Assert.True(result.WasNormalized, result.Error);
        Assert.Equal(256, result.Width);
        Assert.False(result.HasAlpha);
        Assert.NotNull(result.Data);
        Assert.Equal(0xFF, result.Data![0]);
        Assert.Equal(0xD8, result.Data[1]);
        Assert.InRange(result.Bytes, 1, ImageNormalizer.MaxOutputBytes);
        Assert.Equal(800, result.OriginalWidth);
        Assert.Equal(400, result.OriginalHeight);
    }

    [Fact]
    public void Normalize_Fit_ProducesSquareImage()
    {
        var png = CreateRaster(900, 300, SKColors.SeaGreen, SKAlphaType.Opaque, SKEncodedImageFormat.Png);

        var result = ImageNormalizer.Normalize(png, "wide.png", ImageFitMode.Fit);

        Assert.True(result.WasNormalized, result.Error);
        Assert.Equal(256, result.Width);
        Assert.Equal(256, result.Height);
    }

    [Fact]
    public void Normalize_Heic_ReturnsFriendlyMessage()
    {
        var heic = new byte[]
        {
            0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p',
            (byte)'h', (byte)'e', (byte)'i', (byte)'c', 0, 0, 0, 0,
        };

        var result = ImageNormalizer.Normalize(heic, "IMG_0001.heic");

        Assert.False(result.WasNormalized);
        Assert.Contains("HEIC", result.Error);
    }

    [Fact]
    public void Normalize_BrokenFile_ReturnsMessage()
    {
        var result = ImageNormalizer.Normalize(new byte[] { 1, 2, 3, 4, 5 }, "broken.png");

        Assert.False(result.WasNormalized);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Normalize_SvgWithExternalImage_IsRejected()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10">
              <image href="https://example.com/logo.png" width="10" height="10"/>
            </svg>
            """;

        var result = ImageNormalizer.Normalize(Encoding.UTF8.GetBytes(svg), "external.svg");

        Assert.False(result.WasNormalized);
        Assert.Contains("ссыл", result.Error);
    }

    [Fact]
    public void Normalize_TooLargeFile_IsRejectedBeforeDecode()
    {
        var data = new byte[ImageNormalizer.MaxInputBytes + 1];

        var result = ImageNormalizer.Normalize(data, "huge.png");

        Assert.False(result.WasNormalized);
        Assert.Contains("20 МБ", result.Error);
    }

    [Fact]
    public void NeedsNormalization_ChecksSizeAndDimensions()
    {
        var large = CreateRaster(600, 600, SKColors.Blue, SKAlphaType.Opaque, SKEncodedImageFormat.Png);
        var small = CreateRaster(128, 128, SKColors.Blue, SKAlphaType.Opaque, SKEncodedImageFormat.Png);

        Assert.True(ImageNormalizer.NeedsNormalization(large));
        Assert.False(ImageNormalizer.NeedsNormalization(small));
        Assert.False(ImageNormalizer.NeedsNormalization(null));
        Assert.False(ImageNormalizer.NeedsNormalization([]));
    }

    [Fact]
    public async Task NormalizeLegacyAsync_ResizesOldImage()
    {
        var large = CreateRaster(600, 300, SKColors.Purple, SKAlphaType.Opaque, SKEncodedImageFormat.Png);

        var normalized = await ImageNormalizer.NormalizeLegacyAsync(large);

        Assert.NotNull(normalized);
        Assert.True(ImageNormalizer.NeedsNormalization(large));
        Assert.False(ImageNormalizer.NeedsNormalization(normalized));
    }

    [Fact]
    public void Normalize_RemovesUniformBackground()
    {
        var png = CreateLogoOnWhite(400);

        var result = ImageNormalizer.Normalize(
            png,
            "logo.png",
            ImageFitMode.Fill,
            removeBackground: true);

        Assert.True(result.WasNormalized, result.Error);
        Assert.True(result.BackgroundRemoved);
        Assert.True(result.HasAlpha);
        Assert.Equal(256, result.Width);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public void Normalize_DoesNotRemoveBusyBackground()
    {
        using var bitmap = new SKBitmap(400, 400, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(bitmap))
        {
            for (var x = 0; x < 400; x += 8)
            {
                canvas.DrawRect(
                    SKRect.Create(x, 0, 8, 400),
                    new SKPaint { Color = new SKColor((byte)(x % 255), 40, (byte)(x % 180)) });
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var result = ImageNormalizer.Normalize(
            data.ToArray(),
            "busy.png",
            removeBackground: true);

        Assert.True(result.WasNormalized, result.Error);
        Assert.False(result.BackgroundRemoved);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public void Normalize_AppliesCropZoomAndOffset()
    {
        var png = CreateTwoTone(800, 400);

        var defaultResult = ImageNormalizer.Normalize(png, "photo.png");
        var zoomed = ImageNormalizer.Normalize(
            png,
            "photo.png",
            crop: new ImageCropSettings(Zoom: 2f, OffsetX: 0.5f, OffsetY: 0.5f));

        Assert.True(defaultResult.WasNormalized, defaultResult.Error);
        Assert.True(zoomed.WasNormalized, zoomed.Error);
        Assert.Equal(256, zoomed.Width);
        Assert.NotEqual(defaultResult.Data, zoomed.Data);
    }

    [Fact]
    public void Normalize_InvalidCropValues_FallBackToCenter()
    {
        var png = CreateTwoTone(800, 400);

        var result = ImageNormalizer.Normalize(
            png,
            "photo.png",
            crop: new ImageCropSettings(float.NaN, float.NaN, float.NaN));

        Assert.True(result.WasNormalized, result.Error);
        Assert.Equal(256, result.Width);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public void Normalize_CanceledToken_Aborts()
    {
        var png = CreateRaster(800, 400, SKColors.Orange, SKAlphaType.Opaque, SKEncodedImageFormat.Png);
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<System.OperationCanceledException>(
            () => ImageNormalizer.Normalize(png, cancellationToken: cts.Token));
    }

    private static byte[] CreateTwoTone(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Red);
            using var paint = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(SKRect.Create(width / 2f, 0, width / 2f, height), paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] CreateLogoOnWhite(int size)
    {
        using var bitmap = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Crimson };
            canvas.DrawRect(
                SKRect.Create(size * 0.25f, size * 0.25f, size * 0.5f, size * 0.5f),
                paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] CreateRaster(
        int width,
        int height,
        SKColor color,
        SKAlphaType alpha,
        SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, alpha);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(color);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100);
        return data.ToArray();
    }
}

