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

static SKBitmap RenderPng(string pngPath, int size)
{
    using var source = SKBitmap.Decode(pngPath)
        ?? throw new InvalidOperationException($"Failed to load PNG: {pngPath}");
    var bitmap = new SKBitmap(size, size);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);

    // Slightly enlarge the artwork within the square so it reads at the same
    // visual size as native desktop icons, while keeping the canvas dimensions.
    var scale = Math.Min(size / (float)source.Width, size / (float)source.Height) * 1.12f;
    var width = source.Width * scale;
    var height = source.Height * scale;
    var destination = SKRect.Create((size - width) / 2f, (size - height) / 2f, width, height);
    using var image = SKImage.FromBitmap(source);
    canvas.DrawImage(image, destination, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
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

    var dibImages = images.Select(x => (x.Size, Data: EncodeIcoDib(x.Size, x.PngData))).ToList();
    var offset = 6 + dibImages.Count * 16;
    foreach (var (size, dib) in dibImages)
    {
        writer.Write(ToIcoDimension(size));
        writer.Write(ToIcoDimension(size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(dib.Length);
        writer.Write(offset);
        offset += dib.Length;
    }

    foreach (var (_, dib) in dibImages)
    {
        writer.Write(dib);
    }
}

static byte[] EncodeIcoDib(int size, byte[] pngData)
{
    using var bitmap = SKBitmap.Decode(pngData)
        ?? throw new InvalidOperationException("Failed to decode PNG for ICO.");
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);

    writer.Write(40); // BITMAPINFOHEADER size
    writer.Write(size);
    writer.Write(size * 2); // ICO DIB height includes the AND mask
    writer.Write((short)1);
    writer.Write((short)32);
    writer.Write(0);
    writer.Write(size * size * 4);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);

    for (var y = size - 1; y >= 0; y--)
    for (var x = 0; x < size; x++)
    {
        var color = bitmap.GetPixel(x, y);
        writer.Write(color.Blue);
        writer.Write(color.Green);
        writer.Write(color.Red);
        writer.Write(color.Alpha);
    }

    var maskRowSize = ((size + 31) / 32) * 4;
    writer.Write(new byte[maskRowSize * size]);
    return stream.ToArray();
}

static byte ToIcoDimension(int size) => size >= 256 ? (byte)0 : (byte)size;
