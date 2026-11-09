using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FamilyMoney.Services;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace FamilyMoney.Navigation.Desktop.Services;

/// <summary>
/// На компьютере камеры в этом окне нет: QR читается из выбранного изображения.
/// </summary>
public sealed class DesktopQrScannerService(Func<TopLevel?> topLevelProvider) : IQrScannerService
{
    public async Task<string?> ScanAsync()
    {
        var topLevel = topLevelProvider();
        if (topLevel == null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Изображение с QR-кодом",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Изображение")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"],
                },
            ],
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var text = Decode(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("На изображении нет QR-кода.");
        }

        return text;
    }

    private static string? Decode(string path)
    {
        using var bitmap = SKBitmap.Decode(path);
        if (bitmap == null)
        {
            throw new InvalidOperationException("Не удалось открыть изображение.");
        }

        var bytes = new byte[bitmap.Width * bitmap.Height * 3];
        var index = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                bytes[index++] = color.Red;
                bytes[index++] = color.Green;
                bytes[index++] = color.Blue;
            }
        }

        var source = new RGBLuminanceSource(bytes, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.RGB24);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                PossibleFormats = [BarcodeFormat.QR_CODE],
                TryHarder = true,
            },
        };
        return reader.Decode(source)?.Text;
    }
}
