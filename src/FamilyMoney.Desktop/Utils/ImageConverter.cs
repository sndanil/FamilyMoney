using Avalonia.Media.Imaging;
using FamilyMoney.Utils;
using System.IO;

namespace FamilyMoney.Utils;

public static class ImageConverter
{
    public static Bitmap? ToImage(Stream? stream)
    {
        if (stream == null)
        {
            return null;
        }

        try
        {
            return Bitmap.DecodeToWidth(stream, 400);
        }
        catch
        {
            return null;
        }
    }

    public static byte[]? ToImageData(Stream? stream)
    {
        return ImageDataHelper.ToByteArray(stream);
    }

    public static byte[]? ToImageData(Bitmap? bitmap)
    {
        if (bitmap == null)
        {
            return null;
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }
}
