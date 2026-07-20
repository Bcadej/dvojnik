using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FileExplorerClone;

public class CompareStateToBrushConverter : IValueConverter
{
    private static readonly Brush IdenticalBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xF0, 0xC8)); // light green
    private static readonly Brush DiffersBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE3, 0xB3));   // light orange
    private static readonly Brush PlainBrush = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CompareState state)
        {
            // Green marks a match and orange a mismatch; anything present on only one side —
            // like a row while Sync View is off — is left plain.
            return state switch
            {
                CompareState.Identical => IdenticalBrush,
                CompareState.Differs => DiffersBrush,
                _ => PlainBrush
            };
        }
        return PlainBrush;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
