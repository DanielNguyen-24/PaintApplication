using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using PaintApplication.Helpers;

namespace PaintApplication.Models
{
    public class ShapeModel : ViewModelBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Dùng enum từ Enums.cs
        public ShapeType ShapeType { get; set; }

        public Geometry? Geometry { get; set; }

        private double _x;
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        private UIElement? _visual;
        public UIElement? Visual
        {
            get => _visual;
            set => SetProperty(ref _visual, value);
        }

        // Polyline
        public List<Point> Points { get; set; } = new List<Point>();

        // Line
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        // Rectangle / Ellipse
        public double Width { get; set; }
        public double Height { get; set; }

        public string StrokeColor { get; set; } = "#FF000000";
        public double Thickness { get; set; } = 2.0;
        public bool IsFilled { get; set; } = false;
        public string FillColor { get; set; } = "#00000000";

        public string? Text { get; set; }
        public string? FontFamilyName { get; set; }
        public double FontSize { get; set; } = 16;
        public string ForegroundColor { get; set; } = "#FF000000";

        public byte[]? ImageData { get; set; }
    }
}
