using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;

namespace PaintApplication.Converters
{
    public class UIElementToTypeNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "Unknown";

            return value switch
            {
                Line => "Line",
                Rectangle => "Rectangle",
                Ellipse => "Ellipse / Circle",
                Polygon polygon => polygon.Points.Count == 3 ? "Triangle" : 
                                  polygon.Points.Count == 10 ? "Star" :
                                  polygon.Points.Count == 5 ? "Pentagon" :
                                  polygon.Points.Count == 6 ? "Hexagon" :
                                  polygon.Points.Count == 4 ? "Diamond" : "Polygon",
                Polyline => "Pencil / Brush Stroke",
                Path => "Custom Shape",
                TextBlock tb => $"Text: {tb.Text?.Substring(0, Math.Min(15, tb.Text?.Length ?? 0)) ?? ""}",
                Image => "Image",
                _ => value.GetType().Name
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IndexToNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
                return (index + 1).ToString();
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
