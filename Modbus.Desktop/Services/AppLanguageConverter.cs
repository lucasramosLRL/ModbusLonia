using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Modbus.Desktop.Services;

public class AppLanguageConverter : IValueConverter
{
    public static readonly AppLanguageConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is AppLanguage lang ? lang switch
        {
            AppLanguage.Portuguese => "Português (BR)",
            _                      => "English"
        } : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
