using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace claude_model_setting.Converters;

/// <summary>
/// 集合计数转 Visibility：count > 0 时 Visible，否则 Collapsed
/// 支持反转（参数为 "Invert"）
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        var invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        var hasItems = count > 0;
        return (hasItems != invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
