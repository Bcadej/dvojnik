using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FileExplorerClone;

public class CompareStateToBrushConverter : IValueConverter
{
    private static readonly Brush OnlyHereBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xF0, 0xC8)); // light green
    private static readonly Brush DiffersBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE3, 0xB3));  // light orange
    private static readonly Brush NormalBrush = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CompareState state)
        {
            return state switch
            {
                CompareState.OnlyHere => OnlyHereBrush,
                CompareState.Differs => DiffersBrush,
                _ => NormalBrush
            };
        }
        return NormalBrush;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
