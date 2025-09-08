using PaintApplication.Helpers;
using PaintApplication.Models;
using PaintApplication.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PaintApplication.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public ToolboxViewModel Toolbox { get; }
        public CanvasViewModel Canvas { get; }
        public ObservableCollection<LayerViewModel> Layers { get; } = new();

        private readonly FileService _fileService = new();
        private readonly UndoRedoService _undoRedo = new();

        public ICommand NewFileCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        private double _zoomLevel = 100;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                SetProperty(ref _zoomLevel, value);
                OnPropertyChanged(nameof(ZoomPercentage));
            }
        }
        public string ZoomPercentage => $"{ZoomLevel}%";

        public MainViewModel()
        {
            Toolbox = new ToolboxViewModel();
            Canvas = new CanvasViewModel();

            var baseLayer = new LayerModel { Name = "Layer 1" };
            Layers.Add(new LayerViewModel(baseLayer));

            // copy shapes từ Layer vào Canvas
            foreach (var s in baseLayer.Shapes)
                Canvas.Shapes.Add(s);

            NewFileCommand = new RelayCommand(_ => NewFile());
            OpenFileCommand = new RelayCommand(_ => OpenFile());
            SaveFileCommand = new RelayCommand(_ => SaveFile());
            ExitCommand = new RelayCommand(_ => System.Windows.Application.Current.Shutdown());
            UndoCommand = new RelayCommand(_ => { /* TODO */ }, _ => true);
            RedoCommand = new RelayCommand(_ => { /* TODO */ }, _ => true);
        }

        private void NewFile()
        {
            Canvas.Shapes.Clear();
            Layers.Clear();

            var layer = new LayerModel { Name = "Layer 1" };
            Layers.Add(new LayerViewModel(layer));

            foreach (var s in layer.Shapes)
                Canvas.Shapes.Add(s);
        }

        private void OpenFile()
        {
            var project = _fileService.OpenProject();
            if (project == null) return;

            Layers.Clear();
            foreach (var l in project.Layers)
                Layers.Add(new LayerViewModel(l));

            Canvas.CanvasWidth = project.CanvasWidth;
            Canvas.CanvasHeight = project.CanvasHeight;

            Canvas.Shapes.Clear();
            if (Layers.Count > 0)
            {
                foreach (var s in Layers[0].Shapes)
                    Canvas.Shapes.Add(s);
            }
        }

        private void SaveFile()
        {
            var proj = new ProjectModel
            {
                CanvasWidth = Canvas.CanvasWidth,
                CanvasHeight = Canvas.CanvasHeight
            };

            foreach (var lv in Layers)
            {
                var lm = new LayerModel
                {
                    Name = lv.Name,
                    Shapes = lv.Shapes,
                    IsVisible = lv.IsVisible,
                    Opacity = lv.Opacity
                };
                proj.Layers.Add(lm);
            }

            _fileService.SaveProject(proj);
        }
    }
}
