using Avalonia.Data.Converters;
using System.Globalization;

namespace FamilyMoney.Navigation.Converters;

/// <summary>
/// Короткая подпись состояния синхронизации для мобильного экрана.
/// Сообщения об ошибке и ходе синхронизации проходят как есть.
/// </summary>
public sealed class SyncStatusCaptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            "S3 синхронизация включена" => "Включена",
            "S3 синхронизация отключена" => "Выключена",
            string text => text,
            _ => string.Empty,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
