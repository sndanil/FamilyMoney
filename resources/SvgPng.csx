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

static byte[] EncodePng(SKBitmap bitmap)
{
    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

static void SavePng(SKBitmap bitmap, string outputPath)
{
    var directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    using var stream = File.Create(outputPath);
    stream.Write(EncodePng(bitmap));
}

static void SaveIco(string outputPath, IReadOnlyList<(int Size, byte[] PngData)> images)
{
    if (images.Count == 0)
    {
        throw new ArgumentException("ICO must contain at least one image.", nameof(images));
    }

    var directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    using var stream = File.Create(outputPath);
    using var writer = new BinaryWriter(stream);

    writer.Write((short)0);
    writer.Write((short)1);
    writer.Write((short)images.Count);

    var offset = 6 + images.Count * 16;
    foreach (var (size, png) in images)
    {
        writer.Write(ToIcoDimension(size));
        writer.Write(ToIcoDimension(size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(png.Length);
        writer.Write(offset);
        offset += png.Length;
    }

    foreach (var (_, png) in images)
    {
        writer.Write(png);
    }
}

static byte ToIcoDimension(int size) => size >= 256 ? (byte)0 : (byte)size;
