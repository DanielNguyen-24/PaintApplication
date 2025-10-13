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
using SelectionMode = PaintApplication.Models.SelectionMode;

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

        public event Action<double>? ZoomRequested;
        private TextBox? _activeTextBox;
        private bool _isSelecting;
        private bool _shouldPushStateOnMouseUp = true;
        private Point _selectionStart;
        private Rect _selectionRect = new Rect(0, 0, 0, 0);
        private bool _hasSelection;
        private bool _cropAfterSelection;
        private readonly List<Point> _freeformSelectionPoints = new();
        private Geometry? _selectionGeometry;
        private bool _isRectangleSelectionActive;
        private bool _isFreeformSelectionActive;
        private bool _isDraggingSelection;
        private Point _selectionDragStart;
        private Rect _selectionOriginalRect;
        private readonly List<Point> _selectionOriginalFreeformPoints = new();
        private SelectionMode? _dragSelectionType;
        private Image? _floatingSelectionImage;
        private bool _selectionIsFloating;
        private Rect _floatingSelectionRect;
        private bool _selectionMovedDuringDrag;

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


        private int _canvasWidth = 1000;
        public int CanvasWidth
        {
            get => _canvasWidth;
            private set => SetProperty(ref _canvasWidth, value);
        }

        private int _canvasHeight = 600;
        public int CanvasHeight
        {
            get => _canvasHeight;
            private set => SetProperty(ref _canvasHeight, value);
        }

        public Rect SelectionRect
        {
            get => _selectionRect;
            private set => SetProperty(ref _selectionRect, value);
        }

        public bool HasSelection
        {
            get => _hasSelection;
            private set => SetProperty(ref _hasSelection, value);
        }

        public Geometry? SelectionGeometry
        {
            get => _selectionGeometry;
            private set => SetProperty(ref _selectionGeometry, value);
        }

        public bool IsRectangleSelectionActive
        {
            get => _isRectangleSelectionActive;
            private set => SetProperty(ref _isRectangleSelectionActive, value);
        }

        public bool IsFreeformSelectionActive
        {
            get => _isFreeformSelectionActive;
            private set => SetProperty(ref _isFreeformSelectionActive, value);
        }

        public bool CanUndo => _undoStack.Count > 1;
        public bool CanRedo => _redoStack.Count > 0;

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
            CommitActiveTextBox();
            _shouldPushStateOnMouseUp = true;

            var color = _toolbox.SelectedColor;
            var thickness = (BrushSize > 0) ? BrushSize : _toolbox.Thickness;

            switch (_toolbox.SelectedTool)
            {
                case ToolType.Select:
                    if (TryBeginDragSelection(pos))
                        return;

                    if (HasSelection)
                        ClearSelection();

                    BeginSelection(pos);
                    return;

                case ToolType.Text:
                    StartTextInput(pos);
                    return;

                case ToolType.Fill:
                    ClearSelection();
                    DoFloodFill(pos, _toolbox.SelectedColor);
                    PushUndoState();
                    StateChanged?.Invoke();
                    _shouldPushStateOnMouseUp = false;
                    return;

                case ToolType.Magnifier:
                    double zoomDelta = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -10 : 10;
                    ZoomRequested?.Invoke(zoomDelta);
                    _shouldPushStateOnMouseUp = false;
                    return;
            }

            ClearSelection();

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

                case ToolType.Brush:
                    switch (_toolbox.SelectedBrush)
                    {
                        case BrushType.Brush:
                            StartPolyline(color, thickness, ShapeType.None);
                            break;

                        case BrushType.CalligraphyBrush:
                            _currentLine = new Polyline
                            {
                                Stroke = new SolidColorBrush(color),
                                StrokeThickness = thickness * 1.5,
                                StrokeStartLineCap = PenLineCap.Triangle,
                                StrokeEndLineCap = PenLineCap.Triangle,
                                StrokeLineJoin = PenLineJoin.Bevel
                            };
                            Shapes.Add(_currentLine);
                            break;

                        case BrushType.CalligraphyPen:
                            StartPolyline(color, thickness * 0.7, ShapeType.None);
                            break;

                        case BrushType.Airbrush:
                            StartAirbrush(pos, color, thickness);
                            break;

                        case BrushType.OilBrush:
                            _currentLine = new Polyline
                            {
                                Stroke = new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
                                StrokeThickness = thickness * 2,
                                StrokeLineJoin = PenLineJoin.Round
                            };
                            Shapes.Add(_currentLine);
                            break;

                        case BrushType.Crayon:
                            StartCrayon(pos, color, thickness);
                            break;

                        case BrushType.Marker:
                            _currentLine = new Polyline
                            {
                                Stroke = new SolidColorBrush(Color.FromArgb(100, color.R, color.G, color.B)),
                                StrokeThickness = thickness * 3,
                                StrokeLineJoin = PenLineJoin.Round
                            };
                            Shapes.Add(_currentLine);
                            break;

                        case BrushType.NaturalPencil:
                            StartPencil(pos, color, thickness);
                            break;

                        case BrushType.WatercolorBrush:
                            var dot = new Ellipse
                            {
                                Width = thickness * 2,
                                Height = thickness,
                                Fill = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B))
                            };
                            Canvas.SetLeft(dot, pos.X - thickness);
                            Canvas.SetTop(dot, pos.Y - thickness / 2);
                            Shapes.Add(dot);
                            break;
                    }
                    break;
            }
        }

        private void OnMouseMove(Point pos)
        {
            MousePosition = pos;
            if (_isDraggingSelection)
            {
                DragSelection(pos);
                return;
            }
            var color = _toolbox.SelectedColor;

            switch (_toolbox.SelectedTool)
            {
                case ToolType.Select:
                    UpdateSelection(pos);
                    break;

                case ToolType.Pencil:
                case ToolType.Eraser:
                    if (_currentLine != null)
                        _currentLine.Points.Add(pos);
                    break;

                case ToolType.Shape:
                    HandleShapeMouseMove(pos);
                    break;

                case ToolType.Brush:
                    switch (_toolbox.SelectedBrush)
                    {
                        case BrushType.Brush:
                            StartBrush(pos, color, BrushSize);
                            break;

                        case BrushType.CalligraphyBrush:
                            StartCalligraphyBrush(pos, color, BrushSize);
                            break;

                        case BrushType.CalligraphyPen:
                            StartCalligraphyPen(pos, color, BrushSize);
                            break;

                        case BrushType.Airbrush:
                            StartAirbrush(pos, color, BrushSize);
                            break;

                        case BrushType.OilBrush:
                            StartOilBrush(pos, color);
                            break;

                        case BrushType.Crayon:
                            StartCrayon(pos, color, BrushSize);
                            break;

                        case BrushType.Marker:
                            StartMarker(pos, color, BrushSize);
                            break;

                        case BrushType.NaturalPencil:
                            StartNaturalPencil(pos, color, BrushSize);
                            break;

                        case BrushType.WatercolorBrush:
                            StartWatercolor(pos, color, BrushSize);
                            break;


                    }
                    break;
            }
        }


        private void OnMouseUp()
        {
            if (_isDraggingSelection)
            {
                FinishDragSelection();
                return;
            }
            if (_isSelecting)
            {
                EndSelection();
                return;
            }

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

            if (_shouldPushStateOnMouseUp)
            {
                PushUndoState();
                StateChanged?.Invoke();
            }
        }

        private void CommitActiveTextBox()
        {
            if (_activeTextBox != null)
            {
                CommitText(_activeTextBox);
            }
        }

        private void StartTextInput(Point pos)
        {
            ClearSelection();
            _shouldPushStateOnMouseUp = false;

            var brush = new SolidColorBrush(_toolbox.SelectedColor);
            var textBox = new TextBox
            {
                MinWidth = 120,
                MinHeight = 30,
                Background = Brushes.Transparent,
                BorderBrush = brush,
                Foreground = brush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily(_toolbox.SelectedFontFamily),
                FontSize = _toolbox.FontSize,
                Tag = "CanvasTextInput"
            };

            Canvas.SetLeft(textBox, pos.X);
            Canvas.SetTop(textBox, pos.Y);

            textBox.LostFocus += TextBox_LostFocus;
            textBox.KeyDown += TextBox_KeyDown;

            _activeTextBox = textBox;
            Shapes.Add(textBox);
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
                {
                    e.Handled = true;
                    CommitText(textBox);
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    CancelText(textBox);
                }
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                CommitText(textBox);
            }
        }

        private void CommitText(TextBox textBox)
        {
            CleanupTextHandlers(textBox);

            if (!Shapes.Contains(textBox))
                return;

            _activeTextBox = null;

            var left = Canvas.GetLeft(textBox);
            if (double.IsNaN(left)) left = 0;
            var top = Canvas.GetTop(textBox);
            if (double.IsNaN(top)) top = 0;

            Shapes.Remove(textBox);

            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                var brush = textBox.Foreground is SolidColorBrush solid
                    ? (Brush)solid.CloneCurrentValue()
                    : textBox.Foreground?.CloneCurrentValue() ?? Brushes.Black;

                var textBlock = new TextBlock
                {
                    Text = textBox.Text,
                    Foreground = brush,
                    FontFamily = textBox.FontFamily,
                    FontSize = textBox.FontSize,
                    TextWrapping = TextWrapping.Wrap,
                    Background = Brushes.Transparent,
                    Padding = textBox.Padding,
                    Tag = "CanvasText"
                };

                Canvas.SetLeft(textBlock, left);
                Canvas.SetTop(textBlock, top);

                Shapes.Add(textBlock);
                PushUndoState();
                StateChanged?.Invoke();
            }
        }

        private void CancelText(TextBox textBox)
        {
            CleanupTextHandlers(textBox);

            if (Shapes.Contains(textBox))
            {
                Shapes.Remove(textBox);
            }

            _activeTextBox = null;
        }

        private void CleanupTextHandlers(TextBox textBox)
        {
            textBox.LostFocus -= TextBox_LostFocus;
            textBox.KeyDown -= TextBox_KeyDown;
        }

        private void BeginSelection(Point pos)
        {
            _isSelecting = true;
            _shouldPushStateOnMouseUp = false;
            _selectionStart = pos;
            _isDraggingSelection = false;
            _dragSelectionType = null;
            if (_toolbox.SelectionMode == SelectionMode.Freeform)
            {
                _freeformSelectionPoints.Clear();
                _freeformSelectionPoints.Add(pos);
                IsFreeformSelectionActive = true;
                IsRectangleSelectionActive = false;
                RecalculateFreeformSelection(closeFigure: false);
            }
            else
            {
                _freeformSelectionPoints.Clear();
                SelectionGeometry = null;
                SelectionRect = new Rect(pos, new Size(0, 0));
                HasSelection = false;
                IsRectangleSelectionActive = false;
                IsFreeformSelectionActive = false;
            }
        }

        private void UpdateSelection(Point pos)
        {
            if (!_isSelecting)
                return;
            if (_toolbox.SelectionMode == SelectionMode.Freeform)
            {
                AddFreeformPoint(pos);
            }
            else
            {
                double x = Math.Min(_selectionStart.X, pos.X);
                double y = Math.Min(_selectionStart.Y, pos.Y);
                double width = Math.Abs(pos.X - _selectionStart.X);
                double height = Math.Abs(pos.Y - _selectionStart.Y);

                SelectionRect = new Rect(x, y, width, height);
                HasSelection = width >= 1 && height >= 1;
                IsRectangleSelectionActive = width > 0 || height > 0;
            }
        }

        private void EndSelection()
        {
            _isSelecting = false;
            if (_toolbox.SelectionMode == SelectionMode.Freeform)
            {
                AddFreeformPoint(MousePosition);
                RecalculateFreeformSelection(closeFigure: true);
            }
            else
            {
                IsRectangleSelectionActive = HasSelection;
            }

            if (!HasSelection)
            {
                if (_cropAfterSelection)
                {
                    SelectionRect = new Rect(0, 0, 0, 0);
                    HasSelection = false;
                    if (_toolbox.SelectionMode == SelectionMode.Freeform)
                    {
                        SelectionGeometry = null;
                        IsFreeformSelectionActive = false;
                        _freeformSelectionPoints.Clear();
                    }
                }
                else
                {
                    ClearSelection();
                }

                return;
            }

            if (_toolbox.SelectionMode == SelectionMode.Freeform)
            {
                IsFreeformSelectionActive = true;
            }

            if (_cropAfterSelection)
            {
                _cropAfterSelection = false;
                CropSelection();
            }
        }

        public void ClearSelection(bool cancelCropMode = true)
        {
            _isSelecting = false;
            SelectionRect = new Rect(0, 0, 0, 0);
            HasSelection = false;
            SelectionGeometry = null;
            _freeformSelectionPoints.Clear();
            IsRectangleSelectionActive = false;
            IsFreeformSelectionActive = false;
            _isDraggingSelection = false;
            _dragSelectionType = null;
            _selectionOriginalFreeformPoints.Clear();
            _floatingSelectionImage = null;
            _selectionIsFloating = false;
            _floatingSelectionRect = new Rect(0, 0, 0, 0);
            _selectionMovedDuringDrag = false;
            if (cancelCropMode)
            {
                _cropAfterSelection = false;
            }
        }

        private bool TryBeginDragSelection(Point pos)
        {
            if (!HasSelection)
                return false;

            if (IsFreeformSelectionActive && SelectionGeometry != null && SelectionGeometry.FillContains(pos))
            {
                _isDraggingSelection = true;
                _selectionDragStart = pos;
                _selectionOriginalRect = _selectionIsFloating ? _floatingSelectionRect : SelectionRect;
                _selectionOriginalFreeformPoints.Clear();
                _selectionOriginalFreeformPoints.AddRange(_freeformSelectionPoints);
                _dragSelectionType = SelectionMode.Freeform;
                _shouldPushStateOnMouseUp = false;
                _selectionMovedDuringDrag = false;
                return true;
            }

            if (IsRectangleSelectionActive && SelectionRect.Contains(pos))
            {
                _isDraggingSelection = true;
                _selectionDragStart = pos;
                _selectionOriginalRect = _selectionIsFloating ? _floatingSelectionRect : SelectionRect;
                _selectionOriginalFreeformPoints.Clear();
                _dragSelectionType = SelectionMode.Rectangle;
                _shouldPushStateOnMouseUp = false;
                _selectionMovedDuringDrag = false;
                return true;
            }

            return false;
        }

        private void DragSelection(Point pos)
        {
            if (!_isDraggingSelection || _dragSelectionType == null)
                return;

            Vector delta = pos - _selectionDragStart;

            if (!_selectionIsFloating)
            {
                if (delta.X == 0 && delta.Y == 0)
                    return;

                if (!CreateFloatingSelectionVisual())
                {
                    _isDraggingSelection = false;
                    _dragSelectionType = null;
                    return;
                }

                _selectionOriginalRect = _floatingSelectionRect;
                if (_dragSelectionType == SelectionMode.Freeform)
                {
                    _selectionOriginalFreeformPoints.Clear();
                    _selectionOriginalFreeformPoints.AddRange(_freeformSelectionPoints);
                }
            }

            _selectionMovedDuringDrag |= delta.X != 0 || delta.Y != 0;

            if (_floatingSelectionImage != null)
            {
                Canvas.SetLeft(_floatingSelectionImage, _floatingSelectionRect.X + delta.X);
                Canvas.SetTop(_floatingSelectionImage, _floatingSelectionRect.Y + delta.Y);
            }

            if (_dragSelectionType == SelectionMode.Rectangle)
            {
                var movedRect = new Rect(
                    _selectionOriginalRect.X + delta.X,
                    _selectionOriginalRect.Y + delta.Y,
                    _selectionOriginalRect.Width,
                    _selectionOriginalRect.Height);

                SelectionRect = movedRect;
                HasSelection = movedRect.Width >= 1 && movedRect.Height >= 1;
                IsRectangleSelectionActive = HasSelection;
                return;
            }

            _freeformSelectionPoints.Clear();
            foreach (var point in _selectionOriginalFreeformPoints)
            {
                _freeformSelectionPoints.Add(new Point(point.X + delta.X, point.Y + delta.Y));
            }

            RecalculateFreeformSelection(closeFigure: true);
        }

        private void FinishDragSelection()
        {
            if (!_isDraggingSelection)
                return;

            _isDraggingSelection = false;

            if (_dragSelectionType == SelectionMode.Freeform)
            {
                if (_freeformSelectionPoints.Count == 0)
                {
                    ClearSelection();
                }
                else
                {
                    RecalculateFreeformSelection(closeFigure: true);
                }
            }
            else if (_dragSelectionType == SelectionMode.Rectangle)
            {
                HasSelection = SelectionRect.Width >= 1 && SelectionRect.Height >= 1;
                IsRectangleSelectionActive = HasSelection;
                if (!HasSelection)
                {
                    ClearSelection();
                }
            }

            if (_floatingSelectionImage != null)
            {
                double left = Canvas.GetLeft(_floatingSelectionImage);
                double top = Canvas.GetTop(_floatingSelectionImage);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                double width = _floatingSelectionRect.Width;
                double height = _floatingSelectionRect.Height;
                if (HasSelection)
                {
                    width = SelectionRect.Width;
                    height = SelectionRect.Height;
                }

                _floatingSelectionRect = new Rect(left, top, width, height);
            }

            _dragSelectionType = null;
            _selectionOriginalFreeformPoints.Clear();

            if (_selectionMovedDuringDrag)
            {
                PushUndoState();
                StateChanged?.Invoke();
            }

            _selectionMovedDuringDrag = false;
        }

        private bool CreateFloatingSelectionVisual()
        {
            var bitmap = CaptureSelectionBitmap(out var pixelRect);
            if (bitmap == null || pixelRect.Width <= 0 || pixelRect.Height <= 0)
                return false;

            if (_dragSelectionType == SelectionMode.Freeform && _selectionOriginalFreeformPoints.Count > 1)
            {
                var polygon = new Polygon
                {
                    Fill = Brushes.White,
                    Stroke = Brushes.Transparent,
                    IsHitTestVisible = false
                };

                foreach (var point in _selectionOriginalFreeformPoints)
                    polygon.Points.Add(point);

                if (_selectionOriginalFreeformPoints.Count > 0 && _selectionOriginalFreeformPoints[0] != _selectionOriginalFreeformPoints[^1])
                    polygon.Points.Add(_selectionOriginalFreeformPoints[0]);

                Panel.SetZIndex(polygon, 0);
                Shapes.Add(polygon);
            }
            else
            {
                var rect = new Rectangle
                {
                    Width = pixelRect.Width,
                    Height = pixelRect.Height,
                    Fill = Brushes.White,
                    Stroke = Brushes.Transparent,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(rect, pixelRect.X);
                Canvas.SetTop(rect, pixelRect.Y);
                Panel.SetZIndex(rect, 0);
                Shapes.Add(rect);
            }

            var image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.None,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(image, pixelRect.X);
            Canvas.SetTop(image, pixelRect.Y);
            Panel.SetZIndex(image, int.MaxValue);
            Shapes.Add(image);

            _floatingSelectionImage = image;
            _selectionIsFloating = true;
            _floatingSelectionRect = new Rect(pixelRect.X, pixelRect.Y, pixelRect.Width, pixelRect.Height);

            if (_dragSelectionType == SelectionMode.Rectangle)
            {
                SelectionRect = new Rect(pixelRect.X, pixelRect.Y, pixelRect.Width, pixelRect.Height);
                HasSelection = pixelRect.Width >= 1 && pixelRect.Height >= 1;
                IsRectangleSelectionActive = HasSelection;
            }

            return true;
        }

        private BitmapSource? CaptureSelectionBitmap(out Int32Rect pixelRect)
        {
            pixelRect = new Int32Rect(0, 0, 0, 0);

            if (CanvasWidth <= 0 || CanvasHeight <= 0)
                return null;

            var sourceRect = _selectionOriginalRect;
            int x = (int)Math.Floor(sourceRect.X);
            int y = (int)Math.Floor(sourceRect.Y);
            int width = (int)Math.Ceiling(sourceRect.Width);
            int height = (int)Math.Ceiling(sourceRect.Height);

            if (width <= 0 || height <= 0)
                return null;

            x = Math.Max(0, x);
            y = Math.Max(0, y);

            if (x >= CanvasWidth || y >= CanvasHeight)
                return null;

            if (x + width > CanvasWidth)
                width = CanvasWidth - x;
            if (y + height > CanvasHeight)
                height = CanvasHeight - y;

            if (width <= 0 || height <= 0)
                return null;

            pixelRect = new Int32Rect(x, y, width, height);

            var bitmap = RenderToBitmap(CanvasWidth, CanvasHeight);
            bitmap.Freeze();

            if (_dragSelectionType == SelectionMode.Freeform && SelectionGeometry != null)
                return ExtractFreeformSelectionBitmap(bitmap, pixelRect);

            var cropped = new CroppedBitmap(bitmap, pixelRect);
            cropped.Freeze();
            return cropped;
        }

        private BitmapSource? ExtractFreeformSelectionBitmap(BitmapSource source, Int32Rect pixelRect)
        {
            if (SelectionGeometry == null)
                return null;

            int stride = pixelRect.Width * 4;
            byte[] pixels = new byte[stride * pixelRect.Height];
            source.CopyPixels(pixelRect, pixels, stride, 0);

            var geometry = SelectionGeometry.Clone();
            geometry.Freeze();

            for (int y = 0; y < pixelRect.Height; y++)
            {
                double canvasY = pixelRect.Y + y + 0.5;
                for (int x = 0; x < pixelRect.Width; x++)
                {
                    double canvasX = pixelRect.X + x + 0.5;
                    if (!geometry.FillContains(new Point(canvasX, canvasY)))
                    {
                        int index = y * stride + x * 4;
                        pixels[index] = 0;
                        pixels[index + 1] = 0;
                        pixels[index + 2] = 0;
                        pixels[index + 3] = 0;
                    }
                }
            }

            var writeable = new WriteableBitmap(pixelRect.Width, pixelRect.Height, 96, 96, PixelFormats.Pbgra32, null);
            writeable.WritePixels(new Int32Rect(0, 0, pixelRect.Width, pixelRect.Height), pixels, stride, 0);
            writeable.Freeze();
            return writeable;
        }

        private static byte[] EncodeBitmapSource(BitmapSource source)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private void AddFreeformPoint(Point pos)
        {
            if (_freeformSelectionPoints.Count > 0)
            {
                var last = _freeformSelectionPoints[^1];
                if (last == pos)
                    return;
            }

            _freeformSelectionPoints.Add(pos);
            RecalculateFreeformSelection(closeFigure: false);
        }

        private void RecalculateFreeformSelection(bool closeFigure)
        {
            if (_freeformSelectionPoints.Count == 0)
            {
                SelectionRect = new Rect(_selectionStart, new Size(0, 0));
                HasSelection = false;
                SelectionGeometry = null;
                IsFreeformSelectionActive = false;
                return;
            }

            IsFreeformSelectionActive = true;

            double minX = _freeformSelectionPoints[0].X;
            double minY = _freeformSelectionPoints[0].Y;
            double maxX = minX;
            double maxY = minY;

            for (int i = 1; i < _freeformSelectionPoints.Count; i++)
            {
                var point = _freeformSelectionPoints[i];
                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;
            }

            double width = Math.Max(0, maxX - minX);
            double height = Math.Max(0, maxY - minY);

            SelectionRect = new Rect(new Point(minX, minY), new Size(width, height));
            HasSelection = _freeformSelectionPoints.Count > 2 && width >= 1 && height >= 1;

            if (_freeformSelectionPoints.Count < 2)
            {
                SelectionGeometry = null;
                return;
            }

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                bool isClosed = closeFigure && HasSelection;
                bool isFilled = closeFigure && HasSelection;
                ctx.BeginFigure(_freeformSelectionPoints[0], isFilled, isClosed);
                if (_freeformSelectionPoints.Count > 1)
                {
                    var segmentPoints = new Point[_freeformSelectionPoints.Count - 1];
                    for (int i = 1; i < _freeformSelectionPoints.Count; i++)
                    {
                        segmentPoints[i - 1] = _freeformSelectionPoints[i];
                    }
                    ctx.PolyLineTo(segmentPoints, true, true);
                }
            }
            geometry.Freeze();
            SelectionGeometry = geometry;
        }

        public void BeginCropSelectionMode()
        {
            _cropAfterSelection = true;
            if (HasSelection)
            {
                ClearSelection(cancelCropMode: false);
            }
        }

        public void CancelCropMode()
        {
            _cropAfterSelection = false;
        }

        // ========== Undo/Redo ==========
        public void PushUndoState()
        {
            var snapshot = CloneShapes(ExportShapes());
            _undoStack.Push(snapshot);
            _redoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        public void Undo()
        {
            if (_undoStack.Count > 1)
            {
                var current = _undoStack.Pop();
                _redoStack.Push(CloneShapes(current));
                var prev = _undoStack.Peek();
                ImportShapes(CloneShapes(prev));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        }

        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var state = _redoStack.Pop();
                _undoStack.Push(CloneShapes(state));
                ImportShapes(CloneShapes(state));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        }

        // ========== Hỗ trợ vẽ ==========
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
                    if (poly.Fill is SolidColorBrush solidFill)
                    {
                        shape.IsFilled = solidFill.Color.A != 0;
                        shape.FillColor = solidFill.Color.ToString();
                    }
                    else
                    {
                        shape.IsFilled = false;
                        shape.FillColor = "#00000000";
                    }
                    foreach (var p in poly.Points)
                        shape.Points.Add(new Point(p.X, p.Y));

                    models.Add(shape);
                }
                else if (element is TextBlock textBlock)
                {
                    models.Add(new ShapeModel
                    {
                        ShapeType = ShapeType.Text,
                        X = double.IsNaN(Canvas.GetLeft(textBlock)) ? 0 : Canvas.GetLeft(textBlock),
                        Y = double.IsNaN(Canvas.GetTop(textBlock)) ? 0 : Canvas.GetTop(textBlock),
                        Text = textBlock.Text,
                        FontSize = textBlock.FontSize,
                        FontFamilyName = textBlock.FontFamily?.Source,
                        ForegroundColor = textBlock.Foreground is SolidColorBrush solid
                            ? solid.Color.ToString()
                            : textBlock.Foreground?.ToString() ?? "#FF000000"
                    });
                }
                else if (element is Image img && img.Source is BitmapSource bitmapSource)
                {
                    var model = new ShapeModel
                    {
                        ShapeType = ShapeType.Image,
                        X = double.IsNaN(Canvas.GetLeft(img)) ? 0 : Canvas.GetLeft(img),
                        Y = double.IsNaN(Canvas.GetTop(img)) ? 0 : Canvas.GetTop(img),
                        Width = double.IsNaN(img.Width) ? bitmapSource.PixelWidth : img.Width,
                        Height = double.IsNaN(img.Height) ? bitmapSource.PixelHeight : img.Height,
                        ImageData = EncodeBitmapSource(bitmapSource)
                    };

                    models.Add(model);
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
                            Stroke = ParseBrush(m.StrokeColor, Brushes.Black),
                            StrokeThickness = m.Thickness
                        };
                        Shapes.Add(line);
                        break;

                    case ShapeType.Rectangle:
                        var rect = new Rectangle
                        {
                            Width = m.Width,
                            Height = m.Height,
                            Stroke = ParseBrush(m.StrokeColor, Brushes.Black),
                            StrokeThickness = m.Thickness,
                            Fill = m.IsFilled
                                ? ParseBrush(m.FillColor, Brushes.Transparent)
                                : CloneBrush(Brushes.Transparent)
                        };
                        Canvas.SetLeft(rect, m.X);
                        Canvas.SetTop(rect, m.Y);
                        Shapes.Add(rect);
                        break;

                    case ShapeType.Ellipse:
                        var ell = new Ellipse
                        {
                            Width = m.Width,
                            Height = m.Height,
                            Stroke = ParseBrush(m.StrokeColor, Brushes.Black),
                            StrokeThickness = m.Thickness,
                            Fill = m.IsFilled
                                ? ParseBrush(m.FillColor, Brushes.Transparent)
                                : CloneBrush(Brushes.Transparent)
                        };
                        Canvas.SetLeft(ell, m.X);
                        Canvas.SetTop(ell, m.Y);
                        Shapes.Add(ell);
                        break;

                    case ShapeType.Freeform:
                        var poly = new Polyline
                        {
                            Stroke = ParseBrush(m.StrokeColor, Brushes.Black),
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
                            Stroke = ParseBrush(m.StrokeColor, Brushes.Black),
                            StrokeThickness = m.Thickness,
                            Fill = m.IsFilled
                                ? ParseBrush(m.FillColor, Brushes.Transparent)
                                : CloneBrush(Brushes.Transparent)
                        };
                        foreach (var p in m.Points)
                            polygon.Points.Add(p);
                        Shapes.Add(polygon);
                        break;

                    case ShapeType.Text:
                        var textBlock = new TextBlock
                        {
                            Text = m.Text ?? string.Empty,
                            FontSize = m.FontSize > 0 ? m.FontSize : _toolbox.FontSize,
                            TextWrapping = TextWrapping.Wrap,
                            Background = Brushes.Transparent
                        };

                        if (!string.IsNullOrWhiteSpace(m.FontFamilyName))
                        {
                            textBlock.FontFamily = new FontFamily(m.FontFamilyName);
                        }

                        textBlock.Foreground = ParseBrush(m.ForegroundColor, Brushes.Black);

                        Canvas.SetLeft(textBlock, m.X);
                        Canvas.SetTop(textBlock, m.Y);
                        Shapes.Add(textBlock);
                        break;

                    case ShapeType.Image:
                        if (m.ImageData != null && m.ImageData.Length > 0)
                        {
                            using var ms = new MemoryStream(m.ImageData);
                            var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            var frame = decoder.Frames[0];
                            frame.Freeze();

                            var image = new Image
                            {
                                Source = frame,
                                Stretch = Stretch.None
                            };

                            if (m.Width > 0)
                                image.Width = m.Width;
                            if (m.Height > 0)
                                image.Height = m.Height;

                            Canvas.SetLeft(image, m.X);
                            Canvas.SetTop(image, m.Y);
                            Shapes.Add(image);
                        }
                        break;
                }
            }
        }

        private static Brush ParseBrush(string? brushValue, Brush fallback)
        {
            if (!string.IsNullOrWhiteSpace(brushValue))
            {
                var converter = new BrushConverter();
                if (converter.ConvertFromString(brushValue) is Brush parsed)
                {
                    return CloneBrush(parsed);
                }
            }

            return CloneBrush(fallback);
        }

        private static Brush CloneBrush(Brush brush)
        {
            return brush.CloneCurrentValue();
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

        private Rect GetElementBounds(UIElement element)
        {
            if (element is Shape shape)
            {
                return GetShapeBounds(shape);
            }

            double left = Canvas.GetLeft(element);
            if (double.IsNaN(left)) left = 0;
            double top = Canvas.GetTop(element);
            if (double.IsNaN(top)) top = 0;

            if (element is Image image)
            {
                double width = image.Width;
                double height = image.Height;

                if ((width <= 0 || double.IsNaN(width)) && image.Source != null)
                    width = image.Source.Width;
                if ((height <= 0 || double.IsNaN(height)) && image.Source != null)
                    height = image.Source.Height;

                if (width <= 0) width = CanvasWidth;
                if (height <= 0) height = CanvasHeight;

                return new Rect(left, top, width, height);
            }

            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = element.DesiredSize;

            double elementWidth = Math.Max(1, size.Width);
            double elementHeight = Math.Max(1, size.Height);

            return new Rect(left, top, elementWidth, elementHeight);
        }

        private void RenderElements(DrawingContext dc)
        {
            foreach (var element in Shapes)
            {
                if (element is not UIElement uiElement)
                    continue;

                if (uiElement is TextBox textBox && Equals(textBox.Tag, "CanvasTextInput"))
                    continue;

                var bounds = GetElementBounds(uiElement);
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    continue;

                uiElement.Measure(new Size(bounds.Width, bounds.Height));
                uiElement.Arrange(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));

                if (uiElement is Image image && image.Source != null)
                {
                    dc.DrawImage(image.Source, bounds);
                }
                else
                {
                    dc.DrawRectangle(new VisualBrush(uiElement), null, bounds);
                }
            }
        }

        private RenderTargetBitmap RenderToBitmap(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                RenderElements(dc);
            }
            rtb.Render(dv);
            return rtb;
        }

        public void SaveCanvas(string filePath, int width, int height)
        {
            CommitActiveTextBox();
            var rtb = RenderToBitmap(CanvasWidth, CanvasHeight);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var fs = new FileStream(filePath, FileMode.Create);
            encoder.Save(fs);
        }

        public void OpenImage(string filePath, int width, int height)
        {
            var bitmap = new BitmapImage(new Uri(filePath, UriKind.Absolute));

            CommitActiveTextBox();
            Shapes.Clear();
            var imageWidth = bitmap.PixelWidth > 0 ? bitmap.PixelWidth : width;
            var imageHeight = bitmap.PixelHeight > 0 ? bitmap.PixelHeight : height;

            var image = new Image
            {
                Source = bitmap,
                Width = imageWidth,
                Height = imageHeight
            };
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            Shapes.Add(image);

            CanvasWidth = imageWidth;
            CanvasHeight = imageHeight;
            ClearSelection();

            PushUndoState();
        }

        public void CropSelection()
        {
            CommitActiveTextBox();
            _cropAfterSelection = false;
            if (!HasSelection)
                return;

            int x = (int)Math.Floor(SelectionRect.X);
            int y = (int)Math.Floor(SelectionRect.Y);
            int width = (int)Math.Ceiling(SelectionRect.Width);
            int height = (int)Math.Ceiling(SelectionRect.Height);

            if (width <= 0 || height <= 0)
                return;

            x = Math.Max(0, x);
            y = Math.Max(0, y);

            if (x >= CanvasWidth || y >= CanvasHeight)
                return;

            if (x + width > CanvasWidth)
                width = CanvasWidth - x;
            if (y + height > CanvasHeight)
                height = CanvasHeight - y;

            if (width <= 0 || height <= 0)
                return;

            var bitmap = RenderToBitmap(CanvasWidth, CanvasHeight);
            bitmap.Freeze();

            var cropRect = new Int32Rect(x, y, width, height);
            var cropped = new CroppedBitmap(bitmap, cropRect);
            cropped.Freeze();

            Shapes.Clear();
            var image = new Image
            {
                Source = cropped,
                Width = width,
                Height = height
            };
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            Shapes.Add(image);

            CanvasWidth = width;
            CanvasHeight = height;
            ClearSelection();
            PushUndoState();
            StateChanged?.Invoke();
        }

        public void RotateCanvas90()
        {
            CommitActiveTextBox();
            if (CanvasWidth <= 0 || CanvasHeight <= 0)
                return;

            var bitmap = RenderToBitmap(CanvasWidth, CanvasHeight);
            bitmap.Freeze();

            var rotated = new TransformedBitmap(bitmap, new RotateTransform(90));
            rotated.Freeze();

            Shapes.Clear();
            var image = new Image
            {
                Source = rotated,
                Width = rotated.PixelWidth,
                Height = rotated.PixelHeight
            };
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            Shapes.Add(image);

            CanvasWidth = rotated.PixelWidth;
            CanvasHeight = rotated.PixelHeight;
            ClearSelection();
            PushUndoState();
            StateChanged?.Invoke();
        }

        // ========== Flood Fill ==========
        private void DoFloodFill(Point startPoint, Color newColor)
        {
            int width = CanvasWidth;
            int height = CanvasHeight;
            if (width <= 0 || height <= 0) return;

            CommitActiveTextBox();

            // Render toàn bộ Shapes thành bitmap
            var rtb = RenderToBitmap(width, height);

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
            var image = new Image
            {
                Source = wb,
                Width = width,
                Height = height
            };
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            Shapes.Add(image);
            ClearSelection();
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
        private void StartBrush(Point pos, Color color, double size)
        {
            for (int i = 0; i < 3; i++) // vài lớp chồng
            {
                var ell = new Ellipse
                {
                    Width = size - i,
                    Height = size - i,
                    Fill = new SolidColorBrush(Color.FromArgb(
                        (byte)(255 - i * 60), color.R, color.G, color.B))
                };
                Canvas.SetLeft(ell, pos.X - (size - i) / 2);
                Canvas.SetTop(ell, pos.Y - (size - i) / 2);
                Shapes.Add(ell);
            }
        }
        private void StartCalligraphyBrush(Point pos, Color color, double size)
        {
            // to bản, nghiêng
            var rect = new Rectangle
            {
                Width = size * 2,
                Height = size / 2,
                Fill = new SolidColorBrush(color),
                RenderTransform = new RotateTransform(-30, size, size / 4)
            };
            Canvas.SetLeft(rect, pos.X - size);
            Canvas.SetTop(rect, pos.Y - size / 4);
            Shapes.Add(rect);
        }

        private void StartCalligraphyPen(Point pos, Color color, double size)
        {
            // mảnh hơn, nghiêng nhẹ
            var rect = new Rectangle
            {
                Width = size * 1.5,
                Height = size / 4,
                Fill = new SolidColorBrush(color),
                RenderTransform = new RotateTransform(-20, size, size / 8)
            };
            Canvas.SetLeft(rect, pos.X - size * 0.75);
            Canvas.SetTop(rect, pos.Y - size / 8);
            Shapes.Add(rect);
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

        private void StartOilBrush(Point pos, Color color)
        {
            if (_currentLine == null)
            {
                return;
            }

            if (_currentLine.Stroke is SolidColorBrush strokeBrush)
            {
                byte alpha = (byte)_rand.Next(150, 220);
                strokeBrush = strokeBrush.CloneCurrentValue();
                strokeBrush.Color = Color.FromArgb(alpha, color.R, color.G, color.B);
                _currentLine.Stroke = strokeBrush;
            }

            _currentLine.Points.Add(pos);
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


        private void StartMarker(Point pos, Color color, double size)
        {
            var marker = new Rectangle
            {
                Width = size * 2,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(100, color.R, color.G, color.B))
            };
            Canvas.SetLeft(marker, pos.X - size);
            Canvas.SetTop(marker, pos.Y - size / 2);
            Shapes.Add(marker);
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


        private void StartPencil(Point pos, Color color, double size)
        {
            // many tiny jittered strokes to simulate pencil
            for (int i = 0; i < 3; i++)
            {
                var line = new Line
                {
                    X1 = pos.X + _rand.NextDouble() * 1.5 - 0.75,
                    Y1 = pos.Y + _rand.NextDouble() * 1.5 - 0.75,
                    X2 = pos.X + _rand.NextDouble() * 1.5 - 0.75,
                    Y2 = pos.Y + _rand.NextDouble() * 1.5 - 0.75,
                    Stroke = new SolidColorBrush(Color.FromArgb((byte)_rand.Next(90, 220), color.R, color.G, color.B)),
                    StrokeThickness = Math.Max(0.5, size * 0.6)
                };
                Shapes.Add(line);
            }
        }
        
    }
}
