using PaintApplication.Helpers;
using System.Windows.Input;
using System.Windows.Media;
using PaintApplication.Models; 
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.ComponentModel;


namespace PaintApplication.ViewModels
{
    public class ToolboxViewModel : ViewModelBase
    {
        // === State ===

        private ToolType _selectedTool = ToolType.Pencil;
        public ToolType SelectedTool
        {
            get => _selectedTool;
            set => SetProperty(ref _selectedTool, value);
        }
        private ShapeType _selectedShape = ShapeType.None;
        public ShapeType SelectedShape
        {
            get => _selectedShape;
            set
            {
                if (SetProperty(ref _selectedShape, value))
                {
                    if (value != ShapeType.None)
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
                if (SetProperty(ref _selectedBrush, value))
                {
                    if (value != BrushType.Brush)
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
        private double _fontSize = 16;

        public string SelectedFontFamily
        {
            get => _selectedFontFamily;
            set { _selectedFontFamily = value; OnPropertyChanged(); }
        }

        public double FontSize
        {
            get => _fontSize;
            set { _fontSize = value; OnPropertyChanged(); }
        }

        public List<string> FontFamilies { get; } =
            Fonts.SystemFontFamilies.Select(f => f.Source).ToList();

        public List<double> FontSizes { get; } = new List<double>
    { 8, 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72 };

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        public ObservableCollection<BrushItem> Brushes { get; set; }


        // === Commands ===

        // Selection
        public ICommand SelectCommand { get; }

        // Image
        public ICommand CropCommand { get; }
        public ICommand RotateCommand { get; }

        // Tools
        public ICommand PencilCommand { get; }
        public ICommand EraserCommand { get; }
        public ICommand FillCommand { get; }
        public ICommand TextCommand { get; }
        public ICommand MagnifierCommand { get; }

        // Brushes
        public ICommand BrushCommand { get; }

        // Shapes
        public ICommand DrawCircleCommand { get; }

        // Colors
        public ICommand OpenColorPickerCommand { get; }

        // Copilot
        public ICommand AiCommand { get; }

        // Layers
        public ICommand LayersCommand { get; }
        public ICommand SelectColorCommand { get; }

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
            SelectCommand = new RelayCommand(_ => SelectedTool = ToolType.Select);
            BrushCommand = new RelayCommand(_ =>
            {
                if (_ is BrushItem brush)
                    _selectedBrush = brush.Type;
                SelectedTool = ToolType.Brush;
            });
            Brushes = new ObservableCollection<BrushItem>
{
    new BrushItem { Name = "Brush", Type = BrushType.Brush, IconPath = "pack://application:,,,/Resources/Icons/brush.png" },
    //new BrushItem { Name = "Calligraphy brush", Type = BrushType.CalligraphyBrush, IconPath = "pack://application:,,,/Resources/Icons/calligraphy_brush.png" },
    new BrushItem { Name = "Calligraphy pen", Type = BrushType.CalligraphyPen, IconPath = "pack://application:,,,/Resources/Icons/calligraphy_pen.png" },
    new BrushItem { Name = "Airbrush", Type = BrushType.Airbrush, IconPath = "pack://application:,,,/Resources/Icons/airbrush.png" },
    new BrushItem { Name = "Oil brush", Type = BrushType.OilBrush, IconPath = "pack://application:,,,/Resources/Icons/oil_brush.png" },
    new BrushItem { Name = "Crayon", Type = BrushType.Crayon, IconPath = "pack://application:,,,/Resources/Icons/crayon.png" },
    new BrushItem { Name = "Marker", Type = BrushType.Marker, IconPath = "pack://application:,,,/Resources/Icons/marker.png" },
    new BrushItem { Name = "Natural pencil", Type = BrushType.NaturalPencil, IconPath = "pack://application:,,,/Resources/Icons/natural_pencil.png" },
    new BrushItem { Name = "Watercolor brush", Type = BrushType.WatercolorBrush, IconPath = "pack://application:,,,/Resources/Icons/watercolor.png" }
};

            _selectedBrush = Brushes[0].Type;

        }

        public void DoCrop() { /* logic cắt ảnh */ }
        public void DoRotate() { /* logic xoay ảnh */ }
        public void DoOpenColorPicker() { /* mở popup chọn màu */ }
        public void DoAI() { /* gọi AI */ }
        public void DoLayers() { /* quản lý layers */ }

        public ObservableCollection<Color> AvailableColors { get; } =
            new ObservableCollection<Color>
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

       
    }
}
