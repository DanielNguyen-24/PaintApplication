// CanvasViewModel.cs (fixed)
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
using Path = System.Windows.Shapes.Path;

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

        private readonly Random _rand = new Random();

        // Mouse position hiển thị ở StatusBar
        private Point _mousePosition;
        public Point MousePosition
        {
            get => _mousePosition;
            set
            {
                if (SetProperty(ref _mousePosition, value))
                {
                    OnPropertyChanged(nameof(MousePositionDisplay));
                }
            }
        }

        public string MousePositionDisplay => $"X={MousePosition.X:0}, Y={MousePosition.Y:0}";

        private double _brushSize = 2; // giá trị mặc định
        public double BrushSize
        {
            get => _brushSize;
            set => SetProperty(ref _brushSize, value);
        }


        public int CanvasWidth { get; private set; } = 1000;
        public int CanvasHeight { get; private set; } = 600;

        // Undo/Redo stack
        private readonly Stack<List<ShapeModel>> _undoStack = new();
        private readonly Stack<List<ShapeModel>> _redoStack = new();

        // Commands
        public ICommand MouseDownCommand { get; }
        public ICommand MouseMoveCommand { get; }
        public ICommand MouseUpCommand { get; }

        public CanvasViewModel(ToolboxViewModel toolbox)
        {
            _toolbox = toolbox ?? throw new ArgumentNullException(nameof(toolbox));

            MouseDownCommand = new RelayCommand(param =>
            {
                if (param is Point p) OnMouseDown(p);
            });

            MouseMoveCommand = new RelayCommand(param =>
            {
                if (param is Point p) OnMouseMove(p);
            });

            MouseUpCommand = new RelayCommand(_ => OnMouseUp());

            // initial empty state to allow Undo (empty canvas)
            PushUndoState();
        }

        // Cho View gọi khi có kích thước canvas thực tế
        public void UpdateCanvasSize(int width, int height)
        {
            CanvasWidth = Math.Max(1, width);
            CanvasHeight = Math.Max(1, height);
        }

        // ========== Vẽ ==========
        private void OnMouseDown(Point pos)
        {
            _startPoint = pos;
            var color = _toolbox.SelectedColor;
            var thickness = (BrushSize > 0) ? BrushSize : _toolbox.Thickness;

            switch (_toolbox.SelectedTool)
            {
                case ToolType.Pencil:
                    StartPolyline(color, thickness, ShapeType.None);
                    break;

                case ToolType.Eraser:
                    StartPolyline(Colors.White, thickness * 2, ShapeType.None);
                    break;

                case ToolType.Shape:
                    HandleShapeMouseDown(pos, color, thickness);
                    break;

                case ToolType.Fill:
                    DoFloodFill(pos, _toolbox.SelectedColor);
                    PushUndoState();
                    StateChanged?.Invoke();
                    break;

                case ToolType.Text:
                    // TODO: add TextBox creation
                    break;

                case ToolType.Brush:
                    BeginBrushStroke(pos, color, thickness);
                    break;
            }
        }

        private void OnMouseMove(Point pos)
        {
            MousePosition = pos;

            if (Mouse.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var color = _toolbox.SelectedColor;
            var thickness = (BrushSize > 0) ? BrushSize : _toolbox.Thickness;

            switch (_toolbox.SelectedTool)
            {
                case ToolType.Pencil:
                case ToolType.Eraser:
                    if (_currentLine != null)
                        _currentLine.Points.Add(pos);
                    break;

                case ToolType.Shape:
                    HandleShapeMouseMove(pos);
                    break;

                case ToolType.Brush:
                    UpdateBrushStroke(pos, color, thickness);
                    break;
            }
        }


        private void OnMouseUp()
        {
            // Nếu là polyline thì check khép kín (chỉ áp dụng cho Pencil)
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
        private void BeginBrushStroke(Point pos, Color color, double thickness)
        {
            switch (_toolbox.SelectedBrush)
            {
                case BrushType.Brush:
                    StartPolyline(color, thickness, ShapeType.None);
                    break;

                case BrushType.CalligraphyBrush:
                    _currentLine = BeginPolylineStroke(color, thickness * 1.5, PenLineJoin.Bevel, PenLineCap.Triangle, PenLineCap.Triangle);
                    break;

                case BrushType.CalligraphyPen:
                    _currentLine = BeginPolylineStroke(color, thickness * 0.7, PenLineJoin.Round, PenLineCap.Flat, PenLineCap.Flat);
                    break;

                case BrushType.Airbrush:
                    ScatterBrushStroke(pos, color, thickness);
                    break;

                case BrushType.OilBrush:
                    _currentLine = BeginPolylineStroke(Color.FromArgb(200, color.R, color.G, color.B), thickness * 2, PenLineJoin.Round);
                    break;

                case BrushType.Crayon:
                    ScatterBrushStroke(pos, color, thickness);
                    break;

                case BrushType.Marker:
                    _currentLine = BeginPolylineStroke(Color.FromArgb(100, color.R, color.G, color.B), thickness * 3, PenLineJoin.Round, PenLineCap.Square, PenLineCap.Square);
                    break;

                case BrushType.NaturalPencil:
                case BrushType.WatercolorBrush:
                    ScatterBrushStroke(pos, color, thickness);
                    break;
            }
        }

        private void UpdateBrushStroke(Point pos, Color color, double thickness)
        {
            switch (_toolbox.SelectedBrush)
            {
                case BrushType.Brush:
                case BrushType.CalligraphyBrush:
                case BrushType.CalligraphyPen:
                case BrushType.Marker:
                    AddPointToCurrentLine(pos, color, thickness);
                    break;

                case BrushType.OilBrush:
                    if (_currentLine == null)
                    {
                        BeginBrushStroke(pos, color, thickness);
                    }
                    else
                    {
                        if (_currentLine.Stroke is SolidColorBrush stroke)
                        {
                            byte alpha = (byte)_rand.Next(150, 220);
                            stroke.Color = Color.FromArgb(alpha, color.R, color.G, color.B);
                        }
                        _currentLine.Points.Add(pos);
                    }
                    break;

                case BrushType.Airbrush:
                case BrushType.Crayon:
                case BrushType.NaturalPencil:
                case BrushType.WatercolorBrush:
                    ScatterBrushStroke(pos, color, thickness);
                    break;
            }
        }

        private void AddPointToCurrentLine(Point pos, Color color, double thickness)
        {
            if (_currentLine == null)
            {
                BeginBrushStroke(pos, color, thickness);
            }
            else
            {
                _currentLine.Points.Add(pos);
            }
        }

        private void ScatterBrushStroke(Point pos, Color color, double thickness)
        {
            switch (_toolbox.SelectedBrush)
            {
                case BrushType.Airbrush:
                    StartAirbrush(pos, color, thickness);
                    break;

                case BrushType.Crayon:
                    StartCrayon(pos, color, thickness);
                    break;

                case BrushType.NaturalPencil:
                    StartNaturalPencil(pos, color, thickness);
                    break;

                case BrushType.WatercolorBrush:
                    StartWatercolor(pos, color, thickness);
                    break;
            }
        }

        private Polyline BeginPolylineStroke(Color color, double thickness, PenLineJoin lineJoin = PenLineJoin.Round, PenLineCap startCap = PenLineCap.Round, PenLineCap endCap = PenLineCap.Round)
        {
            var line = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                StrokeLineJoin = lineJoin,
                StrokeStartLineCap = startCap,
                StrokeEndLineCap = endCap
            };
            line.Points.Add(_startPoint);
            Shapes.Add(line);
            return line;
        }

        private void StartPolyline(Color color, double thickness, ShapeType type)
        {
            _currentLine = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
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
                StrokeThickness = thickness,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(_currentShape, _startPoint.X);
            Canvas.SetTop(_currentShape, _startPoint.Y);
            Shapes.Add(_currentShape);
        }

        private void StartPentagon(Color color, double thickness)
        {
            _currentShape = new Polygon
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Points = CreateRegularPolygonPoints(_startPoint, _startPoint, 5)
            };
            Shapes.Add(_currentShape);
        }

        private void StartHexagon(Color color, double thickness)
        {
            _currentShape = new Polygon
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Points = CreateRegularPolygonPoints(_startPoint, _startPoint, 6)
            };
            Shapes.Add(_currentShape);
        }

        private void StartHeart(Color color, double thickness)
        {
            _currentShape = new Path
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Data = CreateHeartGeometry(_startPoint, _startPoint)
            };
            Shapes.Add(_currentShape);
        }

        private void StartCloud(Color color, double thickness)
        {
            _currentShape = new Path
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Data = CreateCloudGeometry(_startPoint, _startPoint)
            };
            Shapes.Add(_currentShape);
        }

        private void StartEllipse(Color color, double thickness)
        {
            _currentShape = new Ellipse
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(_currentShape, _startPoint.X);
            Canvas.SetTop(_currentShape, _startPoint.Y);
            Shapes.Add(_currentShape);
        }

        private void StartTriangle(Color color, double thickness)
        {
            _currentShape = new Polygon
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Points = new PointCollection
                {
                    _startPoint, _startPoint, _startPoint
                }
            };
            Shapes.Add(_currentShape);
        }

        private void StartStar(Color color, double thickness)
        {
            _currentShape = new Polygon
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Points = CreateStarPoints(_startPoint, _startPoint)
            };
            Shapes.Add(_currentShape);
        }

        private PointCollection CreateStarPoints(Point start, Point end)
        {
            double centerX = (start.X + end.X) / 2;
            double centerY = (start.Y + end.Y) / 2;
            double radius = Math.Min(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)) / 2;

            var points = new PointCollection();
            for (int i = 0; i < 5; i++)
            {
                double angleOuter = i * 72 * Math.PI / 180 - Math.PI / 2;
                double angleInner = angleOuter + 36 * Math.PI / 180;

                points.Add(new Point(centerX + radius * Math.Cos(angleOuter),
                                     centerY + radius * Math.Sin(angleOuter)));
                points.Add(new Point(centerX + radius / 2 * Math.Cos(angleInner),
                                     centerY + radius / 2 * Math.Sin(angleInner)));
            }

            return points;
        }

        private void StartDiamond(Color color, double thickness)
        {
            _currentShape = new Polygon
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Points = CreateDiamondPoints(_startPoint, _startPoint)
            };
            Shapes.Add(_currentShape);
        }

        private void StartStar4(Color color, double thickness)
        {
            _currentShape = new Polygon
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Points = CreateStarNPoints(_startPoint, _startPoint, 4)
            };
            Shapes.Add(_currentShape);
        }

        private void StartStar6(Color color, double thickness)
        {
            _currentShape = new Polygon
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Points = CreateStarNPoints(_startPoint, _startPoint, 6)
            };
            Shapes.Add(_currentShape);
        }

        private void StartArrows4(Color color, double thickness)
        {
            _currentShape = new Path
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Data = CreateArrows4Geometry(_startPoint, _startPoint)
            };
            Shapes.Add(_currentShape);
        }

        private void StartLightning(Color color, double thickness)
        {
            _currentShape = new Path
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                Data = CreateLightningGeometry(_startPoint, _startPoint)
            };
            Shapes.Add(_currentShape);
        }

        private PointCollection CreateDiamondPoints(Point start, Point end)
        {
            double centerX = (start.X + end.X) / 2;
            double centerY = (start.Y + end.Y) / 2;
            double width = Math.Abs(end.X - start.X) / 2;
            double height = Math.Abs(end.Y - start.Y) / 2;

            return new PointCollection
            {
                new Point(centerX, centerY - height), // top
                new Point(centerX + width, centerY), // right
                new Point(centerX, centerY + height), // bottom
                new Point(centerX - width, centerY)  // left
            };
        }

        private PointCollection CreateStarNPoints(Point start, Point end, int branches)
        {
            var points = new PointCollection();
            double centerX = (start.X + end.X) / 2;
            double centerY = (start.Y + end.Y) / 2;
            double radius = Math.Min(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)) / 2;

            for (int i = 0; i < branches; i++)
            {
                double angleOuter = (2 * Math.PI / branches) * i - Math.PI / 2;
                double angleInner = angleOuter + Math.PI / branches;

                points.Add(new Point(centerX + radius * Math.Cos(angleOuter),
                                     centerY + radius * Math.Sin(angleOuter)));
                points.Add(new Point(centerX + radius / 2 * Math.Cos(angleInner),
                                     centerY + radius / 2 * Math.Sin(angleInner)));
            }

            return points;
        }

        private Geometry CreateArrows4Geometry(Point start, Point end)
        {
            double width = Math.Abs(end.X - start.X);
            double height = Math.Abs(end.Y - start.Y);
            double cx = (start.X + end.X) / 2;
            double cy = (start.Y + end.Y) / 2;
            double size = Math.Min(width, height) / 4;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                // Mũi tên lên
                ctx.BeginFigure(new Point(cx, cy - size * 2), true, true);
                ctx.LineTo(new Point(cx - size, cy - size), true, false);
                ctx.LineTo(new Point(cx + size, cy - size), true, false);

                // Mũi tên phải
                ctx.BeginFigure(new Point(cx + size * 2, cy), true, true);
                ctx.LineTo(new Point(cx + size, cy - size), true, false);
                ctx.LineTo(new Point(cx + size, cy + size), true, false);

                // Mũi tên xuống
                ctx.BeginFigure(new Point(cx, cy + size * 2), true, true);
                ctx.LineTo(new Point(cx - size, cy + size), true, false);
                ctx.LineTo(new Point(cx + size, cy + size), true, false);

                // Mũi tên trái
                ctx.BeginFigure(new Point(cx - size * 2, cy), true, true);
                ctx.LineTo(new Point(cx - size, cy - size), true, false);
                ctx.LineTo(new Point(cx - size, cy + size), true, false);
            }
            geo.Freeze();
            return geo;
        }

        private Geometry CreateLightningGeometry(Point start, Point end)
        {
            double width = Math.Abs(end.X - start.X);
            double height = Math.Abs(end.Y - start.Y);
            double x = Math.Min(start.X, end.X);
            double y = Math.Min(start.Y, end.Y);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(x + width * 0.4, y), true, true);
                ctx.LineTo(new Point(x + width * 0.6, y + height * 0.4), true, false);
                ctx.LineTo(new Point(x + width * 0.5, y + height * 0.4), true, false);
                ctx.LineTo(new Point(x + width * 0.7, y + height), true, false);
                ctx.LineTo(new Point(x + width * 0.3, y + height * 0.6), true, false);
                ctx.LineTo(new Point(x + width * 0.5, y + height * 0.6), true, false);
            }
            geo.Freeze();
            return geo;
        }

        // regular polygon (dùng cho Pentagon, Hexagon...)
        private PointCollection CreateRegularPolygonPoints(Point start, Point end, int sides)
        {
            var points = new PointCollection();
            if (sides < 3) return points;

            double centerX = (start.X + end.X) / 2.0;
            double centerY = (start.Y + end.Y) / 2.0;
            double radius = Math.Min(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)) / 2.0;
            double startAngle = -Math.PI / 2.0;

            for (int i = 0; i < sides; i++)
            {
                double angle = startAngle + 2 * Math.PI * i / sides;
                points.Add(new Point(centerX + radius * Math.Cos(angle),
                                     centerY + radius * Math.Sin(angle)));
            }
            return points;
        }

        private Geometry CreateHeartGeometry(Point start, Point end)
        {
            double width = Math.Abs(end.X - start.X);
            double height = Math.Abs(end.Y - start.Y);
            double x = Math.Min(start.X, end.X);
            double y = Math.Min(start.Y, end.Y);

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                Point topCenter = new Point(x + width / 2, y + height / 4);
                Point bottom = new Point(x + width / 2, y + height);

                ctx.BeginFigure(bottom, true, true);
                ctx.BezierTo(new Point(x + width * 0.2, y + height * 0.75),
                             new Point(x, y + height * 0.4),
                             topCenter, true, true);
                ctx.BezierTo(new Point(x + width, y + height * 0.4),
                             new Point(x + width * 0.8, y + height * 0.75),
                             bottom, true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        private Geometry CreateCloudGeometry(Point start, Point end)
        {
            double width = Math.Abs(end.X - start.X);
            double height = Math.Abs(end.Y - start.Y);
            double x = Math.Min(start.X, end.X);
            double y = Math.Min(start.Y, end.Y);

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(x + width * 0.25, y + height * 0.5), true, true);
                ctx.BezierTo(new Point(x, y), new Point(x + width * 0.5, y - height * 0.2),
                             new Point(x + width * 0.5, y + height * 0.3), true, true);
                ctx.BezierTo(new Point(x + width * 0.5, y - height * 0.2), new Point(x + width, y),
                             new Point(x + width * 0.75, y + height * 0.5), true, true);
                ctx.BezierTo(new Point(x + width, y + height), new Point(x + width * 0.5, y + height * 1.2),
                             new Point(x + width * 0.25, y + height * 0.5), true, true);
            }
            geometry.Freeze();
            return geometry;
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
                    var fillBrush = rect.Fill as SolidColorBrush;
                    bool isFilled = fillBrush != null && fillBrush.Color.A != 0;

                    models.Add(new ShapeModel
                    {
                        ShapeType = ShapeType.Rectangle,
                        X = double.IsNaN(Canvas.GetLeft(rect)) ? 0 : Canvas.GetLeft(rect),
                        Y = double.IsNaN(Canvas.GetTop(rect)) ? 0 : Canvas.GetTop(rect),
                        Width = rect.Width,
                        Height = rect.Height,
                        StrokeColor = rect.Stroke?.ToString() ?? "#FF000000",
                        Thickness = rect.StrokeThickness,
                        FillColor = fillBrush?.ToString() ?? "#00000000",
                        IsFilled = isFilled
                    });
                }
                else if (element is Ellipse ell)
                {
                    var fillBrush = ell.Fill as SolidColorBrush;
                    bool isFilled = fillBrush != null && fillBrush.Color.A != 0;

                    models.Add(new ShapeModel
                    {
                        ShapeType = ShapeType.Ellipse,
                        X = double.IsNaN(Canvas.GetLeft(ell)) ? 0 : Canvas.GetLeft(ell),
                        Y = double.IsNaN(Canvas.GetTop(ell)) ? 0 : Canvas.GetTop(ell),
                        Width = ell.Width,
                        Height = ell.Height,
                        StrokeColor = ell.Stroke?.ToString() ?? "#FF000000",
                        Thickness = ell.StrokeThickness,
                        FillColor = fillBrush?.ToString() ?? "#00000000",
                        IsFilled = isFilled
                    });
                }
                else if (element is Polyline polyline)
                {
                    var shape = new ShapeModel
                    {
                        ShapeType = ShapeType.Freeform,
                        StrokeColor = polyline.Stroke?.ToString() ?? "#FF000000",
                        Thickness = polyline.StrokeThickness
                    };
                    foreach (var p in polyline.Points)
                        shape.Points.Add(new Point(p.X, p.Y));

                    models.Add(shape);
                }
                else if (element is Polygon poly)
                {
                    var shape = new ShapeModel();

                    if (poly.Points.Count == 3)
                        shape.ShapeType = ShapeType.Triangle;
                    else if (poly.Points.Count == 10)
                        shape.ShapeType = ShapeType.Star;
                    else
                        shape.ShapeType = ShapeType.Polygon;

                    shape.StrokeColor = poly.Stroke?.ToString() ?? "#FF000000";
                    shape.Thickness = poly.StrokeThickness;
                    foreach (var p in poly.Points)
                        shape.Points.Add(new Point(p.X, p.Y));

                    models.Add(shape);
                }
                else if (element is Image img && img.Source != null)
                {
                    // Optionally export images
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
                        if (m.IsFilled && !string.IsNullOrEmpty(m.FillColor))
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
                        if (m.IsFilled && !string.IsNullOrEmpty(m.FillColor))
                            ell.Fill = (SolidColorBrush)(new BrushConverter().ConvertFromString(m.FillColor));
                        Canvas.SetLeft(ell, m.X);
                        Canvas.SetTop(ell, m.Y);
                        Shapes.Add(ell);
                        break;

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

                    case ShapeType.Triangle:
                    case ShapeType.Star:
                    case ShapeType.Polygon:
                        var polygon = new Polygon
                        {
                            Stroke = (SolidColorBrush)(new BrushConverter().ConvertFromString(m.StrokeColor)),
                            StrokeThickness = m.Thickness,
                            Fill = Brushes.Transparent
                        };
                        foreach (var p in m.Points)
                            polygon.Points.Add(p);
                        Shapes.Add(polygon);
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
        private Rect GetShapeBounds(Shape shape)
        {
            if (shape is Line l)
            {
                double minX = Math.Min(l.X1, l.X2);
                double minY = Math.Min(l.Y1, l.Y2);
                double w = Math.Abs(l.X2 - l.X1);
                double h = Math.Abs(l.Y2 - l.Y1);
                return new Rect(minX, minY, Math.Max(1, w), Math.Max(1, h));
            }
            else if (shape is Polyline pl && pl.Points.Count > 0)
            {
                double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
                foreach (var p in pl.Points)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }
                if (double.IsInfinity(minX)) return new Rect(0, 0, 1, 1);
                return new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
            }
            else if (shape is Polygon pg && pg.Points.Count > 0)
            {
                double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
                foreach (var p in pg.Points)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }
                if (double.IsInfinity(minX)) return new Rect(0, 0, 1, 1);
                return new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
            }
            else if (shape is Path path && path.Data != null)
            {
                var b = path.Data.Bounds;
                if (b.IsEmpty) return new Rect(0, 0, 1, 1);
                return b;
            }
            else
            {
                double left = Canvas.GetLeft(shape); if (double.IsNaN(left)) left = 0;
                double top = Canvas.GetTop(shape); if (double.IsNaN(top)) top = 0;
                double width = shape.RenderSize.Width > 0 ? shape.RenderSize.Width : shape.DesiredSize.Width;
                double height = shape.RenderSize.Height > 0 ? shape.RenderSize.Height : shape.DesiredSize.Height;
                if (width <= 0) width = 1;
                if (height <= 0) height = 1;
                return new Rect(left, top, width, height);
            }
        }

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
                        var bounds = GetShapeBounds(shape);

                        // Measure & Arrange with bounds to ensure correct rendering
                        shape.Measure(new Size(bounds.Width, bounds.Height));
                        shape.Arrange(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));

                        dc.DrawRectangle(new VisualBrush(shape), null, bounds);
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

        // ========== Flood Fill ==========
        private void DoFloodFill(Point startPoint, Color newColor)
        {
            int width = CanvasWidth;
            int height = CanvasHeight;
            if (width <= 0 || height <= 0) return;

            // Render toàn bộ Shapes thành bitmap
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                foreach (var element in Shapes)
                {
                    if (element is Shape shape)
                    {
                        var bounds = GetShapeBounds(shape);
                        shape.Measure(new Size(bounds.Width, bounds.Height));
                        shape.Arrange(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
                        dc.DrawRectangle(new VisualBrush(shape), null, bounds);
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
            Color targetColor = Color.FromArgb(pixels[index + 3], pixels[index + 2], pixels[index + 1], pixels[index]);

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
                Color c = Color.FromArgb(pixels[idx + 3], pixels[idx + 2], pixels[idx + 1], pixels[idx]);

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

        // Helper: shapes handling split-out để code gọn
        private void HandleShapeMouseDown(Point pos, Color color, double thickness)
        {
            switch (_toolbox.SelectedShape)
            {
                case ShapeType.Line:
                    StartLine(color, thickness);
                    break;
                case ShapeType.Rectangle:
                    StartRectangle(color, thickness);
                    break;
                case ShapeType.Pentagon:
                    StartPentagon(color, thickness);
                    break;
                case ShapeType.Hexagon:
                    StartHexagon(color, thickness);
                    break;
                case ShapeType.Ellipse:
                    StartEllipse(color, thickness);
                    break;
                case ShapeType.Triangle:
                    StartTriangle(color, thickness);
                    break;
                case ShapeType.Star:
                    StartStar(color, thickness);
                    break;
                case ShapeType.Diamond:
                    StartDiamond(color, thickness);
                    break;
                case ShapeType.Star4:
                    StartStar4(color, thickness);
                    break;
                case ShapeType.Star6:
                    StartStar6(color, thickness);
                    break;
                case ShapeType.ArrowUp:
                    StartArrows4(color, thickness);
                    break;
                case ShapeType.Lightning:
                    StartLightning(color, thickness);
                    break;
                case ShapeType.Heart:
                    StartHeart(color, thickness);
                    break;
                case ShapeType.Cloud:
                    StartCloud(color, thickness);
                    break;
            }
        }

        private void HandleShapeMouseMove(Point pos)
        {
            switch (_toolbox.SelectedShape)
            {
                case ShapeType.Line:
                    if (_currentShape is Line line)
                    {
                        line.X2 = pos.X;
                        line.Y2 = pos.Y;
                    }
                    break;

                case ShapeType.Rectangle:
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

                case ShapeType.Pentagon:
                    if (_currentShape is Polygon pent)
                        pent.Points = CreateRegularPolygonPoints(_startPoint, pos, 5);
                    break;

                case ShapeType.Hexagon:
                    if (_currentShape is Polygon hex)
                        hex.Points = CreateRegularPolygonPoints(_startPoint, pos, 6);
                    break;

                case ShapeType.Heart:
                    if (_currentShape is Path heart)
                        heart.Data = CreateHeartGeometry(_startPoint, pos);
                    break;

                case ShapeType.Cloud:
                    if (_currentShape is Path cloud)
                        cloud.Data = CreateCloudGeometry(_startPoint, pos);
                    break;

                case ShapeType.Ellipse:
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

                case ShapeType.Triangle:
                    if (_currentShape is Polygon tri)
                    {
                        tri.Points = new PointCollection
                        {
                            new Point((_startPoint.X + pos.X) / 2, _startPoint.Y), // top vertex
                            new Point(pos.X, pos.Y),                               // bottom-right
                            new Point(_startPoint.X, pos.Y)                        // bottom-left
                        };
                    }
                    break;

                case ShapeType.Star:
                    if (_currentShape is Polygon star)
                    {
                        star.Points = CreateStarPoints(_startPoint, pos);
                    }
                    break;

                case ShapeType.Diamond:
                    if (_currentShape is Polygon dia)
                        dia.Points = CreateDiamondPoints(_startPoint, pos);
                    break;

                case ShapeType.Star4:
                    if (_currentShape is Polygon s4)
                        s4.Points = CreateStarNPoints(_startPoint, pos, 4);
                    break;

                case ShapeType.Star6:
                    if (_currentShape is Polygon s6)
                        s6.Points = CreateStarNPoints(_startPoint, pos, 6);
                    break;

                case ShapeType.ArrowUp:
                    if (_currentShape is Path arr)
                        arr.Data = CreateArrows4Geometry(_startPoint, pos);
                    break;

                case ShapeType.Lightning:
                    if (_currentShape is Path bolt)
                        bolt.Data = CreateLightningGeometry(_startPoint, pos);
                    break;
            }
        }
        private void StartAirbrush(Point pos, Color color, double size)
        {
            int dots = (int)(size * 8);
            for (int i = 0; i < dots; i++)
            {
                double angle = _rand.NextDouble() * 2 * Math.PI;
                double radius = _rand.NextDouble() * size;
                double x = pos.X + radius * Math.Cos(angle);
                double y = pos.Y + radius * Math.Sin(angle);

                var dot = new Ellipse
                {
                    Width = _rand.NextDouble() * 2 + 0.5,
                    Height = _rand.NextDouble() * 2 + 0.5,
                    Fill = new SolidColorBrush(Color.FromArgb(
                        (byte)_rand.Next(120, 200), color.R, color.G, color.B))
                };
                Canvas.SetLeft(dot, x);
                Canvas.SetTop(dot, y);
                Shapes.Add(dot);
            }
        }

        private void StartCrayon(Point pos, Color color, double size)
        {
            for (int i = 0; i < 6; i++)
            {
                var line = new Line
                {
                    X1 = pos.X + _rand.NextDouble() * size - size / 2,
                    Y1 = pos.Y + _rand.NextDouble() * size - size / 2,
                    X2 = pos.X + _rand.NextDouble() * size - size / 2,
                    Y2 = pos.Y + _rand.NextDouble() * size - size / 2,
                    Stroke = new SolidColorBrush(Color.FromArgb(
                        (byte)_rand.Next(60, 150), color.R, color.G, color.B)),
                    StrokeThickness = 1
                };
                Shapes.Add(line);
            }
        }

        private void StartNaturalPencil(Point pos, Color color, double size)
        {
            for (int i = 0; i < 3; i++)
            {
                var line = new Line
                {
                    X1 = pos.X + _rand.NextDouble() * 1.5 - 0.75,
                    Y1 = pos.Y + _rand.NextDouble() * 1.5 - 0.75,
                    X2 = pos.X + _rand.NextDouble() * 1.5 - 0.75,
                    Y2 = pos.Y + _rand.NextDouble() * 1.5 - 0.75,
                    Stroke = new SolidColorBrush(Color.FromArgb(
                        (byte)_rand.Next(90, 220), color.R, color.G, color.B)),
                    StrokeThickness = Math.Max(0.5, size * 0.6)
                };
                Shapes.Add(line);
            }
        }

        private void StartWatercolor(Point pos, Color color, double size)
        {
            {
                for (int i = 0; i < 3; i++)
                {
                    var wc = new Ellipse
                    {
                        Width = size * (1.2 + _rand.NextDouble() * 0.3),
                        Height = size * (0.8 + _rand.NextDouble() * 0.3),
                        Fill = new SolidColorBrush(Color.FromArgb(
                            (byte)_rand.Next(40, 80), color.R, color.G, color.B))
                    };
                    Canvas.SetLeft(wc, pos.X - wc.Width / 2);
                    Canvas.SetTop(wc, pos.Y - wc.Height / 2);
                    Shapes.Add(wc);
                }
            }
        }
    }
}
