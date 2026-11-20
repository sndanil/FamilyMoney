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

