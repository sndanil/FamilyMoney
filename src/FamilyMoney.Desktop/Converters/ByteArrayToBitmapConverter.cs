using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Globalization;

namespace FamilyMoney.Converters;

public class ByteArrayToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] data || data.Length == 0)
        {
            return null;
        }

        using var stream = new MemoryStream(data);
        return Bitmap.DecodeToWidth(stream, 400);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
