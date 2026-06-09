#load "SvgPng.csx"

var root = Directory.GetCurrentDirectory();
var source = Path.Combine(root, "Icon_flat.svg");

int[] pngSizes = [16, 32, 48, 64, 128, 256, 512];

foreach (var size in pngSizes)
{
    using var icon = RenderSvg(source, size, size);
    var outputPath = Path.Combine(root, $"icon-{size}.png");
    SavePng(icon, outputPath);
    Console.WriteLine($"Saved icon-{size}.png: {size}x{size}");
}

int[] icoSizes = [16, 32, 48, 256];
Console.WriteLine($"wallet.ico: {string.Join(", ", icoSizes.Select(s => $"{s}x{s}"))} (ICO generation not implemented)");
