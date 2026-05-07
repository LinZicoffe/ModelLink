using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace claude_model_setting.Views;

/// <summary>
/// 导航索引转 Visibility 转换器
/// </summary>
public sealed class IndexToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string param && int.TryParse(param, out var targetIndex))
        {
            return index == targetIndex ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
