using System.Globalization;
using System.Windows.Data;
using CoupleFinance.Desktop.Presentation;

namespace CoupleFinance.Desktop.Converters;

public sealed class EnumDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        DisplayText.FromEnum(value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
