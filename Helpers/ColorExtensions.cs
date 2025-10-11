using System.Windows.Media;

namespace PaintApplication.Helpers
{
    public static class ColorExtensions
    {
        public static string ToHex(this Color color)
        {
            return color.ToString(); // "#FF000000"
        }

        public static Color ToColor(this string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }
}
