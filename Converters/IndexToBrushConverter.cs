using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace claude_model_setting.Converters;

/// <summary>
/// 索引转换为配色画刷（6 色循环）
/// </summary>
public sealed class IndexToBrushConverter : IValueConverter
{
    private static readonly Brush[] AccentBrushes =
    [
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97757")),
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1890FF")),
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#52C41A")),
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#722ED1")),
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAAD14")),
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#13C2C2")),
    ];

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index)
            return AccentBrushes[index % AccentBrushes.Length];
        return AccentBrushes[0];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
