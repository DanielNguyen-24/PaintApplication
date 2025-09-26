using PaintApplication.Helpers;
using System.Windows.Input;
using System.Windows.Media;
using PaintApplication.Models; 
using System.Collections.ObjectModel;

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
            DrawCircleCommand = new RelayCommand(_ => SelectedTool = ToolType.Circle);

            OpenColorPickerCommand = new RelayCommand(_ => DoOpenColorPicker());
            AiCommand = new RelayCommand(_ => DoAI());
            LayersCommand = new RelayCommand(_ => DoLayers());
            SelectCommand = new RelayCommand(_ => SelectedTool = ToolType.Select);

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
