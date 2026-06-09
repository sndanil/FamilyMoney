#r "nuget: SkiaSharp, 3.116.1"
#r "nuget: Svg.Skia, 3.0.4"

using SkiaSharp;
using Svg.Skia;

static SKBitmap RenderSvg(string svgPath, int width, int height)
{
    var svg = new SKSvg();
    if (svg.Load(svgPath) is null)
    {
        throw new InvalidOperationException($"Failed to load SVG: {svgPath}");
    }

    var bitmap = new SKBitmap(width, height);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);

    var bounds = svg.Picture?.CullRect ?? SKRect.Create(0, 0, width, height);
    var scale = Math.Min(width / bounds.Width, height / bounds.Height);
    var dx = (width - bounds.Width * scale) / 2f - bounds.Left * scale;
    var dy = (height - bounds.Height * scale) / 2f - bounds.Top * scale;

    canvas.Save();
    canvas.Translate(dx, dy);
    canvas.Scale(scale);
    canvas.DrawPicture(svg.Picture);
    canvas.Restore();

    return bitmap;
}

static SKBitmap ComposeCentered(SKBitmap icon, int canvasSize)
{
    var canvas = new SKBitmap(canvasSize, canvasSize);
    using var c = new SKCanvas(canvas);
    c.Clear(SKColors.Transparent);
    c.DrawBitmap(icon, (canvasSize - icon.Width) / 2f, (canvasSize - icon.Height) / 2f);
    return canvas;
}

static void SavePng(SKBitmap bitmap, string outputPath)
{
    var directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    using var stream = File.Create(outputPath);
    data.SaveTo(stream);
}
