using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CoupleFinance.Desktop.Converters;

public sealed class CurrencyColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not decimal amount)
        {
            return Brushes.White;
        }

        return amount >= 0
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF488399"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEA9393"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
