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
                
                try
                {
                    return Color.FromArgb(colorName);
                }
                catch (ArgumentException)
                {
                    // Invalid color name, return default
                    return Colors.Gray;
                }
            }
        }
        
        return Colors.Gray;
    }
    
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This converter is one-way only, so conversion back is not supported
        // Return a safe default value instead of throwing
        return false;
    }
}