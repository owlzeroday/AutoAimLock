using System.Globalization;
using System.Windows.Data;

namespace AUTOAIMLOCK.App.ViewModels;

/// <summary>Converts bool to text for status displays.</summary>
public sealed class BoolToTextConverter : IValueConverter
{
    public static readonly BoolToTextConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            // If parameter is "Enabled/Disabled" mode
            if (parameter is string p && p == "toggle")
                return b ? "ON" : "OFF";
            // Default: target lock mode
            return b ? "LOCKED" : "NO TARGET";
        }
        return "UNKNOWN";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
