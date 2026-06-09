#load "SvgPng.csx"

var root = Directory.GetCurrentDirectory();
var source = Path.Combine(root, "Icon_flat.svg");

int[] pngSizes = [16, 32, 48, 64, 128, 256, 512];
int[] icoSizes = [16, 32, 48, 256];
var icoImages = new List<(int Size, byte[] PngData)>();

foreach (var size in pngSizes)
{
    using var icon = RenderSvg(source, size, size);
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
