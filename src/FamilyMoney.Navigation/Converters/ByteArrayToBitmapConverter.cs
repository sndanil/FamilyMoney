using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace FamilyMoney.Navigation.Converters;

public sealed class ByteArrayToBitmapConverter : IValueConverter
{
    // Иконки отображаются в размере 20-24px; 48px хватает и для экранов с высокой плотностью.
    private const int DecodeWidth = 48;

    // Массивы ImageData переиспользуются закэшированными ViewModel, поэтому ключ по ссылке
    // работает, а слабые ссылки не дают кэшу удерживать память после смены данных.
    private static readonly ConditionalWeakTable<byte[], Bitmap> Cache = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] data || data.Length == 0)
        {
            return null;
        }

        return Cache.GetValue(data, static d =>
        {
            using var stream = new MemoryStream(d);
            return Bitmap.DecodeToWidth(stream, DecodeWidth);
        });
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
