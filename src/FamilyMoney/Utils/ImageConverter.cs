using Avalonia.Media.Imaging;
using System.IO;

namespace FamilyMoney.Utils;

public static class ImageConverter
{
    public static Bitmap? ToImage(Stream? stream)
    {
        if (stream == null) 
            return null;

		try
		{
            return Bitmap.DecodeToWidth(stream, 400);
        }
        catch
		{
			return null;
		}
    }
}
