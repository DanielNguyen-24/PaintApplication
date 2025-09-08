using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using PaintApplication.Models;

namespace PaintApplication.Converters
{
    public class ShapeToUIElementConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ShapeModel shape)
            {
                Brush stroke = (SolidColorBrush)new BrushConverter().ConvertFromString(shape.StrokeColor);
                Brush fill = (SolidColorBrush)new BrushConverter().ConvertFromString(shape.FillColor);

                switch (shape.ShapeType)
                {
                    case ShapeType.Pencil:
                    case ShapeType.Eraser:
                        var geometry = new StreamGeometry();
                        using (var ctx = geometry.Open())
                        {
                            if (shape.Points.Count > 0)
                            {
                                ctx.BeginFigure(shape.Points[0], false, false);
                                for (int i = 1; i < shape.Points.Count; i++)
                                    ctx.LineTo(shape.Points[i], true, false);
                            }
                        }
                        geometry.Freeze();
                        return new Path
                        {
                            Data = geometry,
                            Stroke = stroke,
                            StrokeThickness = shape.Thickness
                        };

                    case ShapeType.Line:
                        return new Line
                        {
                            X1 = shape.X1,
                            Y1 = shape.Y1,
                            X2 = shape.X2,
                            Y2 = shape.Y2,
                            Stroke = stroke,
                            StrokeThickness = shape.Thickness
                        };

                    case ShapeType.Rectangle:
                        return new Rectangle
                        {
                            Width = shape.Width,
                            Height = shape.Height,
                            Stroke = stroke,
                            StrokeThickness = shape.Thickness,
                            Fill = shape.IsFilled ? fill : Brushes.Transparent
                        };

                    case ShapeType.Ellipse:
                        return new Ellipse
                        {
                            Width = shape.Width,
                            Height = shape.Height,
                            Stroke = stroke,
                            StrokeThickness = shape.Thickness,
                            Fill = shape.IsFilled ? fill : Brushes.Transparent
                        };
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
