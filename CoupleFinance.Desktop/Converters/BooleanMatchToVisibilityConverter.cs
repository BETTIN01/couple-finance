using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CoupleFinance.Desktop.Converters;

public sealed class BooleanMatchToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var expected = parameter is null || bool.Parse(parameter.ToString()!);
        return value is bool boolean && boolean == expected ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
