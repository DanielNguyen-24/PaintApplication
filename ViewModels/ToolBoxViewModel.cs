using PaintApplication.Helpers;
using PaintApplication.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace PaintApplication.ViewModels
{
    public class ToolboxViewModel : ViewModelBase
    {
        private ToolType _selectedTool = ToolType.Pencil;
        public ToolType SelectedTool
        {
            get => _selectedTool;
            set
            {
                if (SetProperty(ref _selectedTool, value) && value != ToolType.Select)
                {
                    Canvas?.ClearSelection();
                }
            }
        }

        private ShapeType _selectedShape = ShapeType.None;
        public ShapeType SelectedShape
        {
            get => _selectedShape;
            set
            {
                if (SetProperty(ref _selectedShape, value) && value != ShapeType.None)
                {
                    SelectedTool = ToolType.Shape;
                }
            }
        }

        private BrushType _selectedBrush = BrushType.Brush;
        public BrushType SelectedBrush
        {
            get => _selectedBrush;
            set
            {
                if (SetProperty(ref _selectedBrush, value) && value != BrushType.Brush)
                {
                    SelectedTool = ToolType.Brush;
                }
            }
        }

        private double _thickness = 2.0;
        public double Thickness
        {
            get => _thickness;
            set => SetProperty(ref _thickness, value);
        }

        private Color _selectedColor = Colors.Black;
        public Color SelectedColor
        {
            get => _selectedColor;
            set => SetProperty(ref _selectedColor, value);
        }

        private string _selectedFontFamily = "Arial";
        public string SelectedFontFamily
        {
            get => _selectedFontFamily;
            set => SetProperty(ref _selectedFontFamily, value);
        }

        private double _fontSize = 16;
        public double FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public List<string> FontFamilies { get; } = Fonts.SystemFontFamilies.Select(f => f.Source).ToList();

        public List<double> FontSizes { get; } = new()
        {
            8, 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72
        };

        public ObservableCollection<BrushItem> Brushes { get; }

        public ObservableCollection<Color> AvailableColors { get; } = new()
        {
            Colors.Black,
            Colors.White,
            Colors.Red,
            Colors.Green,
            Colors.Blue,
            Colors.Yellow,
            Colors.Orange,
            Colors.Purple,
            Colors.Brown,
            Colors.Gray,
            Colors.Pink,
            Colors.Cyan,
            Colors.Magenta,
            Colors.Lime,
            Colors.Gold,
            Colors.Silver,
            Colors.Navy,
            Colors.Teal,
            Colors.Maroon,
            Colors.Olive
        };

        public ICommand SelectCommand { get; }
        public ICommand CropCommand { get; }
        public ICommand RotateCommand { get; }
        public ICommand PencilCommand { get; }
        public ICommand EraserCommand { get; }
        public ICommand FillCommand { get; }
        public ICommand TextCommand { get; }
        public ICommand MagnifierCommand { get; }
        public ICommand BrushCommand { get; }
        public ICommand DrawCircleCommand { get; }
        public ICommand OpenColorPickerCommand { get; }
        public ICommand AiCommand { get; }
        public ICommand LayersCommand { get; }
        public ICommand SelectBrushCommand { get; }
        public ICommand SelectColorCommand { get; }

        internal CanvasViewModel? Canvas { get; set; }

        public ToolboxViewModel()
        {
            SelectCommand = new RelayCommand(_ => SelectedTool = ToolType.Select);
            CropCommand = new RelayCommand(_ => DoCrop());
            RotateCommand = new RelayCommand(_ => DoRotate());

            PencilCommand = new RelayCommand(_ => SelectedTool = ToolType.Pencil);
            EraserCommand = new RelayCommand(_ => SelectedTool = ToolType.Eraser);
            FillCommand = new RelayCommand(_ => SelectedTool = ToolType.Fill);
            TextCommand = new RelayCommand(_ => SelectedTool = ToolType.Text);
            MagnifierCommand = new RelayCommand(_ => SelectedTool = ToolType.Magnifier);
            BrushCommand = new RelayCommand(_ => SelectedTool = ToolType.Brush);
            DrawCircleCommand = new RelayCommand(_ =>
            {
                SelectedTool = ToolType.Shape;
                SelectedShape = ShapeType.Ellipse;
            });
            OpenColorPickerCommand = new RelayCommand(_ => DoOpenColorPicker());
            AiCommand = new RelayCommand(_ => DoAI());
            LayersCommand = new RelayCommand(_ => DoLayers());

            SelectBrushCommand = new RelayCommand(obj =>
            {
                if (obj is BrushItem brush)
                {
                    SelectedBrush = brush.Type;
                    SelectedTool = ToolType.Brush;
                }
            });

            SelectColorCommand = new RelayCommand(obj =>
            {
                if (obj is Color color)
                {
                    SelectedColor = color;
                }
            });

            Brushes = new ObservableCollection<BrushItem>
            {
                new BrushItem { Name = "Brush", Type = BrushType.Brush, IconPath = "pack://application:,,,/Resources/Icons/brush.png" },
                new BrushItem { Name = "Calligraphy pen", Type = BrushType.CalligraphyPen, IconPath = "pack://application:,,,/Resources/Icons/calligraphy_pen.png" },
                new BrushItem { Name = "Airbrush", Type = BrushType.Airbrush, IconPath = "pack://application:,,,/Resources/Icons/airbrush.png" },
                new BrushItem { Name = "Oil brush", Type = BrushType.OilBrush, IconPath = "pack://application:,,,/Resources/Icons/oil_brush.png" },
                new BrushItem { Name = "Crayon", Type = BrushType.Crayon, IconPath = "pack://application:,,,/Resources/Icons/crayon.png" },
                new BrushItem { Name = "Marker", Type = BrushType.Marker, IconPath = "pack://application:,,,/Resources/Icons/marker.png" },
                new BrushItem { Name = "Natural pencil", Type = BrushType.NaturalPencil, IconPath = "pack://application:,,,/Resources/Icons/natural_pencil.png" },
                new BrushItem { Name = "Watercolor brush", Type = BrushType.WatercolorBrush, IconPath = "pack://application:,,,/Resources/Icons/watercolor.png" }
            };

            if (Brushes.Count > 0)
            {
                SelectedBrush = Brushes[0].Type;
            }
        }

        public void DoCrop()
        {
            if (Canvas == null)
                return;

            if (!Canvas.HasSelection)
            {
                SelectedTool = ToolType.Select;
                return;
            }

            Canvas.CropSelection();
        }

        public void DoRotate()
        {
            Canvas?.RotateCanvas90();
        }
        public void DoOpenColorPicker() { /* mở popup chọn màu */ }
        public void DoAI() { /* gọi AI */ }
        public void DoLayers() { /* quản lý layers */ }
    }
}
