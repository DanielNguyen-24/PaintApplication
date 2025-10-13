using PaintApplication.Helpers;
using PaintApplication.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using SelectionMode = PaintApplication.Models.SelectionMode;

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
                if (SetProperty(ref _selectedTool, value))
                {
                    if (value != ToolType.Select)
                    {
                        Canvas?.ClearSelection();
                        Canvas?.CancelCropMode();
                    }

                    if (value != ToolType.Shape && SelectedShape != ShapeType.None)
                    {
                        SelectedShape = ShapeType.None;
                    }

                    if (value != ToolType.Brush && SelectedBrushOption != null)
                    {
                        foreach (var brush in Brushes)
                        {
                            brush.IsSelected = brush.Type == SelectedBrush;
                        }
                        SelectedBrushOption = Brushes.FirstOrDefault(b => b.Type == SelectedBrush);
                        OnPropertyChanged(nameof(SelectedBrushDisplayName));
                    }
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
            set => SetProperty(ref _selectedBrush, value);
        }

        private BrushOptionViewModel? _selectedBrushOption;
        public BrushOptionViewModel? SelectedBrushOption
        {
            get => _selectedBrushOption;
            private set => SetProperty(ref _selectedBrushOption, value);
        }

        public string SelectedBrushDisplayName => SelectedBrushOption?.Name ?? "Brushes";

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

        public ObservableCollection<BrushOptionViewModel> Brushes { get; }

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
        private SelectionMode _selectionMode = SelectionMode.Rectangle;
        public SelectionMode SelectionMode
        {
            get => _selectionMode;
            set
            {
                if (SetProperty(ref _selectionMode, value))
                {
                    SelectedTool = ToolType.Select;
                    Canvas?.ClearSelection();
                }
            }
        }
        public ICommand SelectRectangleSelectionCommand { get; }
        public ICommand SelectFreeformSelectionCommand { get; }
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
            SelectRectangleSelectionCommand = new RelayCommand(_ => SelectionMode = SelectionMode.Rectangle);
            SelectFreeformSelectionCommand = new RelayCommand(_ => SelectionMode = SelectionMode.Freeform);
            CropCommand = new RelayCommand(_ => DoCrop());
            RotateCommand = new RelayCommand(_ => DoRotate());

            PencilCommand = new RelayCommand(_ => SelectedTool = ToolType.Pencil);
            EraserCommand = new RelayCommand(_ => SelectedTool = ToolType.Eraser);
            FillCommand = new RelayCommand(_ => SelectedTool = ToolType.Fill);
            TextCommand = new RelayCommand(_ => SelectedTool = ToolType.Text);
            MagnifierCommand = new RelayCommand(_ => SelectedTool = ToolType.Magnifier);

            BrushCommand = new RelayCommand(_ => SelectBrush(SelectedBrush));
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
                if (obj is BrushOptionViewModel option)
                {
                    SelectBrush(option.Type);
                }
                else if (obj is BrushType brushType)
                {
                    SelectBrush(brushType);
                }
            });

            SelectColorCommand = new RelayCommand(obj =>
            {
                if (obj is Color color)
                {
                    SelectedColor = color;
                }
            });

            Brushes = new ObservableCollection<BrushOptionViewModel>
            {
                new BrushOptionViewModel(this, "Brush", BrushType.Brush, "pack://application:,,,/Resources/Icons/brush.png"),
                new BrushOptionViewModel(this, "Calligraphy pen", BrushType.CalligraphyPen, "pack://application:,,,/Resources/Icons/calligraphy_pen.png"),
                new BrushOptionViewModel(this, "Airbrush", BrushType.Airbrush, "pack://application:,,,/Resources/Icons/airbrush.png"),
                new BrushOptionViewModel(this, "Oil brush", BrushType.OilBrush, "pack://application:,,,/Resources/Icons/oil_brush.png"),
                new BrushOptionViewModel(this, "Crayon", BrushType.Crayon, "pack://application:,,,/Resources/Icons/crayon.png"),
                new BrushOptionViewModel(this, "Marker", BrushType.Marker, "pack://application:,,,/Resources/Icons/marker.png"),
                new BrushOptionViewModel(this, "Natural pencil", BrushType.NaturalPencil, "pack://application:,,,/Resources/Icons/natural_pencil.png"),
                new BrushOptionViewModel(this, "Watercolor brush", BrushType.WatercolorBrush, "pack://application:,,,/Resources/Icons/watercolor.png")
            };

            SelectBrush(Brushes.First().Type);
        }

        private void SelectBrush(BrushType brushType)
        {
            SelectedBrush = brushType;
            SelectedTool = ToolType.Brush;

            foreach (var brush in Brushes)
            {
                brush.IsSelected = brush.Type == brushType;
            }

            SelectedBrushOption = Brushes.FirstOrDefault(b => b.Type == brushType);
            OnPropertyChanged(nameof(SelectedBrushDisplayName));
        }

        public void DoCrop()
        {
            if (Canvas == null)
                return;

            if (Canvas.HasSelection)
            {
                Canvas.CropSelection();
                return;
            }

            SelectedTool = ToolType.Select;
            Canvas.BeginCropSelectionMode();
        }

        public void DoRotate()
        {
            Canvas?.RotateCanvas90();
        }
        public void DoOpenColorPicker() { /* mở popup chọn màu */ }
        public void DoAI() { /* gọi AI */ }
        public void DoLayers() { /* quản lý layers */ }
    }

    public class BrushOptionViewModel : ViewModelBase
    {
        public BrushOptionViewModel(ToolboxViewModel owner, string name, BrushType type, string iconPath)
        {
            Name = name;
            Type = type;
            IconPath = iconPath;
            SelectCommand = new RelayCommand(_ => owner.SelectBrushCommand.Execute(this));
        }

        public string Name { get; }
        public BrushType Type { get; }
        public string IconPath { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public ICommand SelectCommand { get; }
    }
}
