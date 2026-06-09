#load "SvgPng.csx"

var root = Directory.GetCurrentDirectory();
var source = Path.Combine(root, "Icon.svg");
var outputDir = Path.Combine(root, "..", "src", "FamilyMoney.Android", "Resources", "drawable");

// Android 12+ masks splash icons to a circle (~66% of canvas).
const double SafeZoneRatio = 0.50;

void Render(int size, string filename)
{
    var iconSize = Math.Max(1, (int)(size * SafeZoneRatio));
    using var icon = RenderSvg(source, iconSize, iconSize);
    using var canvas = ComposeCentered(icon, size);
    var outputPath = Path.Combine(outputDir, filename);
    SavePng(canvas, outputPath);
    Console.WriteLine($"Saved {filename}: {size}x{size} (icon {iconSize}x{iconSize})");
}

Render(288, "splash_icon.png");
Render(512, "icon.png");
