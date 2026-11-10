#load "SvgPng.csx"

using SkiaSharp;

var root = Directory.GetCurrentDirectory();
var source = Path.Combine(root, "AppIcon.png");
var splashOnly = Environment.GetCommandLineArgs().Contains("--splash");

if (!splashOnly)
{
    int[] pngSizes = [16, 32, 48, 64, 128, 256, 512];
    int[] icoSizes = [16, 32, 48, 256];
    var icoImages = new List<(int Size, byte[] PngData)>();

    foreach (var size in pngSizes)
    {
        using var icon = RenderPng(source, size);
        var outputPath = Path.Combine(root, $"icon-{size}.png");
        SavePng(icon, outputPath);
        Console.WriteLine($"Saved icon-{size}.png: {size}x{size}");

        if (icoSizes.Contains(size))
        {
            icoImages.Add((size, EncodePng(icon)));
        }
    }

    var icoPath = Path.Combine(root, "wallet.ico");
    SaveIco(icoPath, icoImages);
    Console.WriteLine($"Saved wallet.ico: {string.Join(", ", icoSizes.Select(s => $"{s}x{s}"))}");

    foreach (var target in new[]
             {
                 Path.Combine(root, "..", "src", "FamilyMoney.Desktop", "wallet.ico"),
                 Path.Combine(root, "..", "src", "FamilyMoney.Desktop", "Assets", "wallet.ico"),
                 Path.Combine(root, "..", "src", "FamilyMoney.Navigation.Desktop", "wallet.ico"),
                 Path.Combine(root, "..", "src", "FamilyMoney.Navigation.Desktop", "Assets", "wallet.ico"),
             })
    {
        File.Copy(icoPath, target, overwrite: true);
        Console.WriteLine($"Copied wallet.ico -> {Path.GetRelativePath(root, target)}");
    }

    var androidRoot = Path.Combine(root, "..", "src", "FamilyMoney.Android", "Resources");
    (int Size, string Folder)[] androidMipmaps =
    [
        (48, "mipmap-mdpi"),
        (72, "mipmap-hdpi"),
        (96, "mipmap-xhdpi"),
        (144, "mipmap-xxhdpi"),
        (192, "mipmap-xxxhdpi"),
    ];

    foreach (var (size, folder) in androidMipmaps)
    {
        using var icon = RenderPng(source, size);
        var outputDir = Path.Combine(androidRoot, folder);
        SavePng(icon, Path.Combine(outputDir, "ic_launcher.png"));
        SavePng(icon, Path.Combine(outputDir, "ic_launcher_round.png"));
        Console.WriteLine($"Saved {folder}/ic_launcher*.png: {size}x{size}");
    }

    var iconPng = Path.Combine(androidRoot, "drawable", "icon.png");
    using (var icon = RenderPng(source, 512))
    {
        SavePng(icon, iconPng);
        Console.WriteLine($"Saved {Path.GetRelativePath(root, iconPng)}: 512x512");
    }
}

// Android 12+ маскирует значок запуска кругом диаметром 2/3 холста.
// Рисунок должен целиком лежать внутри этого круга, иначе срезаются края.
var splashPng = Path.Combine(root, "..", "src", "FamilyMoney.Android", "Resources", "drawable", "splash_icon.png");
using (var splash = RenderSplashPng(source, 512))
{
    SavePng(splash, splashPng);
    Console.WriteLine($"Saved {Path.GetRelativePath(root, splashPng)}: 512x512");
}

static SKBitmap RenderSplashPng(string pngPath, int size)
{
    using var source = SKBitmap.Decode(pngPath)
        ?? throw new InvalidOperationException($"Failed to load PNG: {pngPath}");
    var bounds = OpaqueBounds(source);
    // Круг маски — диаметр 2/3 холста. 0.50 оставляет запас и для скруглённых углов кошелька.
    const float safeFraction = 0.50f;
    var scale = Math.Min(size * safeFraction / bounds.Width, size * safeFraction / bounds.Height);
    var bitmap = new SKBitmap(size, size);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);
    var destination = SKRect.Create(
        (size - bounds.Width * scale) / 2f,
        (size - bounds.Height * scale) / 2f,
        bounds.Width * scale,
        bounds.Height * scale);
    using var image = SKImage.FromBitmap(source);
    canvas.DrawImage(image, bounds, destination, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    return bitmap;
}

static SKRectI OpaqueBounds(SKBitmap bitmap)
{
    var left = bitmap.Width;
    var top = bitmap.Height;
    var right = 0;
    var bottom = 0;
    for (var y = 0; y < bitmap.Height; y++)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, y).Alpha <= 8)
            {
                continue;
            }

            if (x < left) left = x;
            if (y < top) top = y;
            if (x > right) right = x;
            if (y > bottom) bottom = y;
        }
    }

    if (right < left || bottom < top)
    {
        return SKRectI.Create(0, 0, bitmap.Width, bitmap.Height);
    }

    return SKRectI.Create(left, top, right - left + 1, bottom - top + 1);
}
