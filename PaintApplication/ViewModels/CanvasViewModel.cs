using PaintApplication.Helpers;
using PaintApplication.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace PaintApplication.ViewModels
{
    public class CanvasViewModel : ViewModelBase
    {
        public ObservableCollection<UIElement> Shapes { get; } = new();

        private Polyline? _currentLine;
        private Shape? _currentShape;
        private Point _startPoint;
        private readonly ToolboxViewModel _toolbox;
        private event Action? StateChanged;
        // Mouse position hiển thị ở StatusBar
        private Point _mousePosition;
        public Point MousePosition
        {
            get => _mousePosition;
            set => SetProperty(ref _mousePosition, value);
        }

        public int CanvasWidth { get; set; } = 1000;
        public int CanvasHeight { get; set; } = 600;

        // Undo/Redo stack
        private readonly Stack<List<ShapeModel>> _undoStack = new();
        private readonly Stack<List<ShapeModel>> _redoStack = new();

        // Commands
        public ICommand MouseDownCommand { get; }
        public ICommand MouseMoveCommand { get; }
        public ICommand MouseUpCommand { get; }

        public CanvasViewModel(ToolboxViewModel toolbox)
        {
            _toolbox = toolbox;

            MouseDownCommand = new RelayCommand(param =>
            {
                if (param is Point p) OnMouseDown(p);
            });

            MouseMoveCommand = new RelayCommand(param =>
            {
                if (param is Point p) OnMouseMove(p);
            });

            MouseUpCommand = new RelayCommand(_ => OnMouseUp());
        }

        // ========== Vẽ ==========
        private void OnMouseDown(Point pos)
        {
            _startPoint = pos;
            var color = _toolbox.SelectedColor;
            var thickness = _toolbox.Thickness;

            switch (_toolbox.SelectedTool)
            {
                case ToolType.Pencil:
                    StartPolyline(color, thickness, ShapeType.Pencil);
                    break;

                case ToolType.Eraser:
                    StartPolyline(Colors.White, thickness * 2, ShapeType.Eraser);
                    break;

                case ToolType.Line:
                    StartLine(color, thickness);
                    break;

                case ToolType.Rectangle:
                    StartRectangle(color, thickness);
                    break;

                case ToolType.Ellipse:
                    StartEllipse(color, thickness);
                    break;

                case ToolType.Fill:
                    DoFloodFill(pos, _toolbox.SelectedColor);
                    PushUndoState();
                    break;

                case ToolType.Text:
                    // TODO: thêm TextBox
                    break;
            }
        }

        private void OnMouseMove(Point pos)
        {
            MousePosition = pos;

            switch (_toolbox.SelectedTool)
            {
                case ToolType.Pencil:
                case ToolType.Eraser:
                    _currentLine?.Points.Add(pos);
                    break;

                case ToolType.Line:
                    if (_currentShape is Line line)
                    {
                        line.X2 = pos.X;
                        line.Y2 = pos.Y;
                    }
                    break;

                case ToolType.Rectangle:
                    if (_currentShape is Rectangle rect)
                    {
                        double x = Math.Min(pos.X, _startPoint.X);
                        double y = Math.Min(pos.Y, _startPoint.Y);
                        rect.Width = Math.Abs(pos.X - _startPoint.X);
                        rect.Height = Math.Abs(pos.Y - _startPoint.Y);
                        Canvas.SetLeft(rect, x);
                        Canvas.SetTop(rect, y);
                    }
                    break;

                case ToolType.Ellipse:
                    if (_currentShape is Ellipse ell)
                    {
                        double x = Math.Min(pos.X, _startPoint.X);
                        double y = Math.Min(pos.Y, _startPoint.Y);
                        ell.Width = Math.Abs(pos.X - _startPoint.X);
                        ell.Height = Math.Abs(pos.Y - _startPoint.Y);
                        Canvas.SetLeft(ell, x);
                        Canvas.SetTop(ell, y);
                    }
                    break;
            }
        }

        private void OnMouseUp()
        {
            // Nếu là polyline thì check khép kín
            if (_toolbox.SelectedTool == ToolType.Pencil && _currentLine != null)
            {
                if (_currentLine.Points.Count > 2)
                {
                    Point start = _currentLine.Points[0];
                    Point end = _currentLine.Points[^1];

                    double dx = start.X - end.X;
                    double dy = start.Y - end.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    // Nếu điểm đầu và cuối gần nhau thì tự khép kín
                    if (dist < 10)
                    {
                        _currentLine.Points.Add(start);
                    }
                }
            }

            _currentLine = null;
            _currentShape = null;

            // Lưu trạng thái để Undo/Redo
            PushUndoState();
            StateChanged?.Invoke();
        }


        // ========== Undo/Redo ==========
        public void PushUndoState()
        {
            var snapshot = CloneShapes(ExportShapes());
            _undoStack.Push(snapshot);
            _redoStack.Clear();
        }

        public void Undo()
        {
            if (_undoStack.Count > 1)
            {
                var current = _undoStack.Pop();
                _redoStack.Push(CloneShapes(current));
                var prev = _undoStack.Peek();
                ImportShapes(CloneShapes(prev));
            }
        }

        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var state = _redoStack.Pop();
                _undoStack.Push(CloneShapes(state));
                ImportShapes(CloneShapes(state));
            }
        }

        // ========== Hỗ trợ vẽ ==========
        private void StartPolyline(Color color, double thickness, ShapeType type)
        {
            _currentLine = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness
            };
            _currentLine.Points.Add(_startPoint);
            Shapes.Add(_currentLine);
        }

        private void StartLine(Color color, double thickness)
        {
            _currentShape = new Line
            {
                X1 = _startPoint.X,
                Y1 = _startPoint.Y,
                X2 = _startPoint.X,
                Y2 = _startPoint.Y,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness
            };
            Shapes.Add(_currentShape);
        }

        private void StartRectangle(Color color, double thickness)
        {
            _currentShape = new Rectangle
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness
            };
            Canvas.SetLeft(_currentShape, _startPoint.X);
            Canvas.SetTop(_currentShape, _startPoint.Y);
            Shapes.Add(_currentShape);
        }

        private void StartEllipse(Color color, double thickness)
        {
            _currentShape = new Ellipse
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness
            };
            Canvas.SetLeft(_currentShape, _startPoint.X);
            Canvas.SetTop(_currentShape, _startPoint.Y);
            Shapes.Add(_currentShape);
        }

        // ========== Export/Import Shapes ==========
        public List<ShapeModel> ExportShapes()
        {
            var models = new List<ShapeModel>();

            foreach (var element in Shapes)
            {
                if (element is Line line)
                {
                    models.Add(new ShapeModel
                    {
                        ShapeType = ShapeType.Line,
                        X1 = line.X1,
                        Y1 = line.Y1,
                        X2 = line.X2,
                        Y2 = line.Y2,
                        StrokeColor = line.Stroke?.ToString() ?? "#FF000000",
                        Thickness = line.StrokeThickness
                    });
                }
                else if (element is Rectangle rect)
                {
                    models.Add(new ShapeModel
                    {
                        ShapeType = ShapeType.Rectangle,
                        X = Canvas.GetLeft(rect),
                        Y = Canvas.GetTop(rect),
                        Width = rect.Width,
                        Height = rect.Height,
                        StrokeColor = rect.Stroke?.ToString() ?? "#FF000000",
                        Thickness = rect.StrokeThickness,
                        FillColor = rect.Fill?.ToString() ?? "#00000000",
                        IsFilled = rect.Fill != null
                    });
                }
                else if (element is Ellipse ell)
                {
                    models.Add(new ShapeModel
                    {
                        ShapeType = ShapeType.Ellipse,
                        X = Canvas.GetLeft(ell),
                        Y = Canvas.GetTop(ell),
                        Width = ell.Width,
                        Height = ell.Height,
                        StrokeColor = ell.Stroke?.ToString() ?? "#FF000000",
                        Thickness = ell.StrokeThickness,
                        FillColor = ell.Fill?.ToString() ?? "#00000000",
                        IsFilled = ell.Fill != null
                    });
                }
                else if (element is Polyline poly)
                {
                    var shape = new ShapeModel
                    {
                        ShapeType = ShapeType.Freeform,
                        StrokeColor = poly.Stroke?.ToString() ?? "#FF000000",
                        Thickness = poly.StrokeThickness
                    };
                    foreach (var p in poly.Points)
                        shape.Points.Add(new Point(p.X, p.Y));

                    models.Add(shape);
                }
            }

            return models;
        }

        public void ImportShapes(List<ShapeModel> models)
        {
            Shapes.Clear();

            foreach (var m in models)
            {
                switch (m.ShapeType)
                {
                    case ShapeType.Line:
                        var line = new Line
                        {
                            X1 = m.X1,
                            Y1 = m.Y1,
                            X2 = m.X2,
                            Y2 = m.Y2,
                            Stroke = (SolidColorBrush)(new BrushConverter().ConvertFromString(m.StrokeColor)),
                            StrokeThickness = m.Thickness
                        };
                        Shapes.Add(line);
                        break;

                    case ShapeType.Rectangle:
                        var rect = new Rectangle
                        {
                            Width = m.Width,
                            Height = m.Height,
                            Stroke = (SolidColorBrush)(new BrushConverter().ConvertFromString(m.StrokeColor)),
                            StrokeThickness = m.Thickness
                        };
                        if (m.IsFilled)
                            rect.Fill = (SolidColorBrush)(new BrushConverter().ConvertFromString(m.FillColor));
                        Canvas.SetLeft(rect, m.X);
                        Canvas.SetTop(rect, m.Y);
                        Shapes.Add(rect);
                        break;

                    case ShapeType.Ellipse:
                        var ell = new Ellipse
                        {
                            Width = m.Width,
                            Height = m.Height,
                            Stroke = (SolidColorBrush)(new BrushConverter().ConvertFromString(m.StrokeColor)),
                            StrokeThickness = m.Thickness
                        };
                        if (m.IsFilled)
                            ell.Fill = (SolidColorBrush)(new BrushConverter().ConvertFromString(m.FillColor));
                        Canvas.SetLeft(ell, m.X);
                        Canvas.SetTop(ell, m.Y);
                        Shapes.Add(ell);
                        break;

                    case ShapeType.Pencil:
                    case ShapeType.Eraser:
                    case ShapeType.Freeform:
                        var poly = new Polyline
                        {
                            Stroke = (SolidColorBrush)(new BrushConverter().ConvertFromString(m.StrokeColor)),
                            StrokeThickness = m.Thickness
                        };
                        foreach (var p in m.Points)
                            poly.Points.Add(p);
                        Shapes.Add(poly);
                        break;
                }
            }
        }

        private List<ShapeModel> CloneShapes(List<ShapeModel> source)
        {
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<List<ShapeModel>>(json) ?? new List<ShapeModel>();
        }

        // ========== Save/Open Image ==========
        public void SaveCanvas(string filePath, int width, int height)
        {
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();

            using (var dc = dv.RenderOpen())
            {
                foreach (var element in Shapes)
                {
                    if (element is Shape shape)
                    {
                        dc.DrawRectangle(new VisualBrush(shape), null,
                            new Rect(Canvas.GetLeft(shape), Canvas.GetTop(shape),
                                     shape.RenderSize.Width, shape.RenderSize.Height));
                    }
                    else if (element is Image img && img.Source != null)
                    {
                        dc.DrawImage(img.Source, new Rect(0, 0, img.Width, img.Height));
                    }
                }
            }

            rtb.Render(dv);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var fs = new FileStream(filePath, FileMode.Create);
            encoder.Save(fs);
        }

        public void OpenImage(string filePath, int width, int height)
        {
            var bitmap = new BitmapImage(new Uri(filePath, UriKind.Absolute));

            Shapes.Clear();
            Shapes.Add(new Image
            {
                Source = bitmap,
                Width = width,
                Height = height
            });

            PushUndoState();
        }

        // ========== Flood Fill (tạm) ==========
        private void DoFloodFill(Point startPoint, Color newColor)
        {
            int width = CanvasWidth;
            int height = CanvasHeight;

            // Render toàn bộ Shapes thành bitmap
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();

            using (var dc = dv.RenderOpen())
            {
                foreach (var element in Shapes)
                {
                    if (element is Shape shape)
                    {
                        // Đảm bảo shape có kích thước hợp lệ khi render
                        shape.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        shape.Arrange(new Rect(
                            Canvas.GetLeft(shape),
                            Canvas.GetTop(shape),
                            shape.RenderSize.Width > 0 ? shape.RenderSize.Width : shape.DesiredSize.Width,
                            shape.RenderSize.Height > 0 ? shape.RenderSize.Height : shape.DesiredSize.Height
                        ));

                        dc.DrawRectangle(new VisualBrush(shape), null,
                            new Rect(Canvas.GetLeft(shape), Canvas.GetTop(shape),
                                     shape.RenderSize.Width, shape.RenderSize.Height));
                    }
                    else if (element is Image img && img.Source != null)
                    {
                        dc.DrawImage(img.Source, new Rect(0, 0, img.Width, img.Height));
                    }
                }
            }
            rtb.Render(dv);

            // Copy pixel data
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            rtb.CopyPixels(pixels, stride, 0);

            int x = (int)startPoint.X;
            int y = (int)startPoint.Y;
            if (x < 0 || x >= width || y < 0 || y >= height) return;

            // Lấy màu gốc tại điểm click
            int index = (y * width + x) * 4;
            Color targetColor = Color.FromArgb(
                pixels[index + 3], pixels[index + 2], pixels[index + 1], pixels[index]);

            if (targetColor == newColor) return;

            // BFS queue cho flood fill
            Queue<Point> queue = new();
            queue.Enqueue(new Point(x, y));

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                int px = (int)p.X;
                int py = (int)p.Y;

                if (px < 0 || px >= width || py < 0 || py >= height) continue;

                int idx = (py * width + px) * 4;
                Color c = Color.FromArgb(
                    pixels[idx + 3], pixels[idx + 2], pixels[idx + 1], pixels[idx]);

                if (c != targetColor) continue;

                // Đổi pixel thành màu mới
                pixels[idx] = newColor.B;
                pixels[idx + 1] = newColor.G;
                pixels[idx + 2] = newColor.R;
                pixels[idx + 3] = newColor.A;

                // Thêm neighbor
                queue.Enqueue(new Point(px + 1, py));
                queue.Enqueue(new Point(px - 1, py));
                queue.Enqueue(new Point(px, py + 1));
                queue.Enqueue(new Point(px, py - 1));
            }

            // Tạo WriteableBitmap kết quả
            var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);

            // Thay thế Shapes bằng 1 Image (bitmap mới)
            Shapes.Clear();
            Shapes.Add(new Image
            {
                Source = wb,
                Width = width,
                Height = height
            });
        }

    }
}
