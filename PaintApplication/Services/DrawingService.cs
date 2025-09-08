using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PaintApplication.ViewModels;

namespace PaintApplication.Services
{
    class DrawingService
    {
        private Point _lastpoint;
        private readonly Canvas _canvas;
        private readonly ToolboxViewModel _toolbox;
        private bool isDrawing;

        public DrawingService(Canvas canvas, ToolboxViewModel toolbox)
        {
            _canvas = canvas;
            _toolbox = toolbox;
        }

        public void MouseLeftButtonDown(Point position)
        {
            isDrawing = true;
            _lastpoint = position;
        }
        public void MouseMove(Point position)
        {
            if (!isDrawing) return;
            var line = new System.Windows.Shapes.Line
            {
                Stroke = new SolidColorBrush(_toolbox.SelectedColor),
                StrokeThickness = _toolbox.Thickness,
                X1 = _lastpoint.X,
                Y1 = _lastpoint.Y,
                X2 = position.X,
                Y2 = position.Y
            };
            _canvas.Children.Add(line);
            _lastpoint = position;
        }
    }
}
