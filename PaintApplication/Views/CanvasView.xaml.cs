using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PaintApplication.ViewModels;
using PaintApplication.Models;

namespace PaintApplication.Views
{
    public partial class CanvasView : UserControl
    {
        private Polyline _currentLine;
        private Shape _currentShape;
        private Point _startPoint;

        public CanvasView()
        {
            InitializeComponent();
        }

        private MainViewModel MainVM =>
            (Application.Current.MainWindow.DataContext as MainViewModel);

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || MainVM == null) return;

            var toolbox = MainVM.Toolbox;
            _startPoint = e.GetPosition(PART_Canvas);

            switch (toolbox.SelectedTool)
            {
                case ToolType.Pencil:
                    _currentLine = new Polyline
                    {
                        Stroke = new SolidColorBrush(toolbox.SelectedColor),
                        StrokeThickness = toolbox.Thickness
                    };
                    _currentLine.Points.Add(_startPoint);
                    PART_Canvas.Children.Add(_currentLine);
                    break;

                case ToolType.Eraser:
                    _currentLine = new Polyline
                    {
                        Stroke = Brushes.White,
                        StrokeThickness = toolbox.Thickness * 2 // bự hơn nét vẽ
                    };
                    _currentLine.Points.Add(_startPoint);
                    PART_Canvas.Children.Add(_currentLine);
                    break;

                case ToolType.Line:
                    _currentShape = new Line
                    {
                        X1 = _startPoint.X,
                        Y1 = _startPoint.Y,
                        X2 = _startPoint.X,
                        Y2 = _startPoint.Y,
                        Stroke = new SolidColorBrush(toolbox.SelectedColor),
                        StrokeThickness = toolbox.Thickness
                    };
                    PART_Canvas.Children.Add(_currentShape);
                    break;

                case ToolType.Rectangle:
                    _currentShape = new Rectangle
                    {
                        Stroke = new SolidColorBrush(toolbox.SelectedColor),
                        StrokeThickness = toolbox.Thickness
                    };
                    Canvas.SetLeft(_currentShape, _startPoint.X);
                    Canvas.SetTop(_currentShape, _startPoint.Y);
                    PART_Canvas.Children.Add(_currentShape);
                    break;

                case ToolType.Ellipse:
                    _currentShape = new Ellipse
                    {
                        Stroke = new SolidColorBrush(toolbox.SelectedColor),
                        StrokeThickness = toolbox.Thickness
                    };
                    Canvas.SetLeft(_currentShape, _startPoint.X);
                    Canvas.SetTop(_currentShape, _startPoint.Y);
                    PART_Canvas.Children.Add(_currentShape);
                    break;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(PART_Canvas);
            var toolbox = MainVM?.Toolbox;
            if (toolbox == null) return;

            switch (toolbox.SelectedTool)
            {
                case ToolType.Pencil:
                case ToolType.Eraser:
                    if (_currentLine != null && e.LeftButton == MouseButtonState.Pressed)
                        _currentLine.Points.Add(pos);
                    break;

                case ToolType.Line:
                    if (_currentShape is Line line && e.LeftButton == MouseButtonState.Pressed)
                    {
                        line.X2 = pos.X;
                        line.Y2 = pos.Y;
                    }
                    break;

                case ToolType.Rectangle:
                    if (_currentShape is Rectangle rect && e.LeftButton == MouseButtonState.Pressed)
                    {
                        double x = Math.Min(pos.X, _startPoint.X);
                        double y = Math.Min(pos.Y, _startPoint.Y);
                        double w = Math.Abs(pos.X - _startPoint.X);
                        double h = Math.Abs(pos.Y - _startPoint.Y);
                        Canvas.SetLeft(rect, x);
                        Canvas.SetTop(rect, y);
                        rect.Width = w;
                        rect.Height = h;
                    }
                    break;

                case ToolType.Ellipse:
                    if (_currentShape is Ellipse ell && e.LeftButton == MouseButtonState.Pressed)
                    {
                        double x = Math.Min(pos.X, _startPoint.X);
                        double y = Math.Min(pos.Y, _startPoint.Y);
                        double w = Math.Abs(pos.X - _startPoint.X);
                        double h = Math.Abs(pos.Y - _startPoint.Y);
                        Canvas.SetLeft(ell, x);
                        Canvas.SetTop(ell, y);
                        ell.Width = w;
                        ell.Height = h;
                    }
                    break;
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _currentLine = null;
            _currentShape = null;
        }
    }
}
