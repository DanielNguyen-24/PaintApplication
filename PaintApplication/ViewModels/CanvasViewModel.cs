using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PaintApplication.Models;
using PaintApplication.Helpers;

namespace PaintApplication.ViewModels
{
    public class CanvasViewModel : ViewModelBase
    {
        private ShapeModel? _currentShape;
        public ShapeModel? CurrentShape
        {
            get => _currentShape;
            set => SetProperty(ref _currentShape, value);
        }
        private double _canvasWidth = 1000;
        public double CanvasWidth
        {
            get => _canvasWidth;
            set => SetProperty(ref _canvasWidth, value);
        }

        private double _canvasHeight = 600;
        public double CanvasHeight
        {
            get => _canvasHeight;
            set => SetProperty(ref _canvasHeight, value);
        }

        private string _selectedColor = "#FF000000"; // mặc định màu đen
        public string SelectedColor
        {
            get => _selectedColor;
            set => SetProperty(ref _selectedColor, value);
        }


        public ObservableCollection<ShapeModel> Shapes { get; set; } = new ObservableCollection<ShapeModel>();

        // Bắt đầu vẽ
        public void StartDrawing(Point startPoint, ToolType tool, Color color, double thickness)
        {
            var shape = new ShapeModel
            {
                StrokeColor = color.ToString(),
                Thickness = thickness
            };

            switch (tool)
            {
                case ToolType.Pencil:
                    shape.ShapeType = ShapeType.Pencil;
                    shape.Points.Add(startPoint);
                    break;

                case ToolType.Eraser:
                    shape.ShapeType = ShapeType.Eraser;
                    shape.Points.Add(startPoint);
                    break;

                case ToolType.Line:
                    shape.ShapeType = ShapeType.Line;
                    shape.X1 = startPoint.X;
                    shape.Y1 = startPoint.Y;
                    shape.X2 = startPoint.X;
                    shape.Y2 = startPoint.Y;
                    shape.Points.Add(startPoint);
                    break;

                case ToolType.Rectangle:
                    shape.ShapeType = ShapeType.Rectangle;
                    shape.X = startPoint.X;
                    shape.Y = startPoint.Y;
                    shape.Width = 0;
                    shape.Height = 0;
                    shape.Points.Add(startPoint);
                    break;

                case ToolType.Ellipse:
                    shape.ShapeType = ShapeType.Ellipse;
                    shape.X = startPoint.X;
                    shape.Y = startPoint.Y;
                    shape.Width = 0;
                    shape.Height = 0;
                    shape.Points.Add(startPoint);
                    break;

                default:
                    return; // chưa hỗ trợ tool khác
            }

            CurrentShape = shape;
        }

        // Khi đang vẽ
        public void UpdateDrawing(Point currentPoint)
        {
            if (CurrentShape == null) return;

            switch (CurrentShape.ShapeType)
            {
                case ShapeType.Pencil:
                case ShapeType.Eraser:
                    CurrentShape.Points.Add(currentPoint);
                    break;

                case ShapeType.Line:
                    if (CurrentShape.Points.Count == 1)
                        CurrentShape.Points.Add(currentPoint);
                    else
                        CurrentShape.Points[1] = currentPoint;

                    CurrentShape.X2 = currentPoint.X;
                    CurrentShape.Y2 = currentPoint.Y;
                    break;

                case ShapeType.Rectangle:
                case ShapeType.Ellipse:
                    if (CurrentShape.Points.Count == 1)
                        CurrentShape.Points.Add(currentPoint);
                    else
                        CurrentShape.Points[1] = currentPoint;

                    var x = Math.Min(CurrentShape.Points.First().X, currentPoint.X);
                    var y = Math.Min(CurrentShape.Points.First().Y, currentPoint.Y);
                    var w = Math.Abs(currentPoint.X - CurrentShape.Points.First().X);
                    var h = Math.Abs(currentPoint.Y - CurrentShape.Points.First().Y);

                    CurrentShape.X = x;
                    CurrentShape.Y = y;
                    CurrentShape.Width = w;
                    CurrentShape.Height = h;
                    break;
            }
        }

        // Kết thúc vẽ
        public void EndDrawing()
        {
            if (CurrentShape == null) return;
            Shapes.Add(CurrentShape);
            CurrentShape = null;
        }
    }
}
