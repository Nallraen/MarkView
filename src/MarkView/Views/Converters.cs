using System.Globalization;
using System.Windows.Data;

namespace MarkView.Views;

/// <summary>value == parameter → true. ConvertBack: true → parameter (for radio-style toggles bound two-way to an enum).</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Equals(value, parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter : Binding.DoNothing;
}
