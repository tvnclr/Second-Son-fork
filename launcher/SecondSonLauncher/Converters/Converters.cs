using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SecondSonLauncher.Converters;

/// value == parameter -> true; back: true -> parameter (for RadioButtons bound to one string).
public sealed class EqualsToBool : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c) =>
        Equals(value?.ToString(), parameter?.ToString());

    public object ConvertBack(object? value, Type t, object? parameter, CultureInfo c) =>
        value is true ? parameter ?? Binding.DoNothing : Binding.DoNothing;
}

/// value == parameter -> Visible.
public sealed class EqualsToVisibility : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c) =>
        Equals(value?.ToString(), parameter?.ToString()) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type t, object? parameter, CultureInfo c) => Binding.DoNothing;
}

/// true -> Visible; parameter "invert" flips it. Also accepts non-empty strings and non-null objects.
public sealed class TruthyToVisibility : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c)
    {
        var truthy = value switch
        {
            bool b => b,
            string s => s.Length > 0,
            null => false,
            _ => true,
        };
        if (parameter is "invert")
            truthy = !truthy;
        return truthy ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type t, object? parameter, CultureInfo c) => Binding.DoNothing;
}
