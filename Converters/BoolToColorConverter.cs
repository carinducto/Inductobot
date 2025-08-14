using System.Globalization;

namespace Inductobot.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string colors)
        {
            var colorParts = colors.Split('|');
            if (colorParts.Length == 2)
            {
                var trueColor = colorParts[0];
                var falseColor = colorParts[1];
                
                var colorName = boolValue ? trueColor : falseColor;
                return Color.FromArgb(colorName);
            }
        }
        
        return Colors.Gray;
    }
    
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}