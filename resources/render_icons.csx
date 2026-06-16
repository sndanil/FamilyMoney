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
    using var icon = RenderSvg(source, size, size);
    var outputDir = Path.Combine(androidRoot, folder);
    SavePng(icon, Path.Combine(outputDir, "ic_launcher.png"));
    SavePng(icon, Path.Combine(outputDir, "ic_launcher_round.png"));
    Console.WriteLine($"Saved {folder}/ic_launcher*.png: {size}x{size}");
}
