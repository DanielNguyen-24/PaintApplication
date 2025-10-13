using System.Collections.ObjectModel;
using PaintApplication.Helpers;
using PaintApplication.Services;
using System.Windows.Input;
using PaintApplication.Models;

namespace PaintApplication.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public ToolboxViewModel Toolbox { get; }
        public CanvasViewModel Canvas { get; }
        public ObservableCollection<LayerViewModel> Layers { get; } = new();

        // Zoom
        private double _zoomLevel = 100;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set => SetProperty(ref _zoomLevel, value);
        }

        // Commands
        public ICommand NewFileCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand ToggleUndoRedoCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetZoomCommand { get; }

        public MainViewModel()
        {
            Toolbox = new ToolboxViewModel();
            Canvas = new CanvasViewModel(Toolbox);
            Toolbox.Canvas = Canvas;
            Canvas.ZoomRequested += delta => ChangeZoom(delta);

            // Menu commands
            NewFileCommand = new RelayCommand(_ => DoNewFile());
            OpenFileCommand = new RelayCommand(_ => DoOpenFile());
            SaveFileCommand = new RelayCommand(_ => DoSaveFile());
            ExitCommand = new RelayCommand(_ => DoExit());

            // Undo/Redo → gọi sang CanvasViewModel
            UndoCommand = new RelayCommand(_ => Canvas.Undo());
            RedoCommand = new RelayCommand(_ => Canvas.Redo());
            ToggleUndoRedoCommand = new RelayCommand(_ => Canvas.UndoOrRedo());

            ZoomInCommand = new RelayCommand(_ => ChangeZoom(10));
            ZoomOutCommand = new RelayCommand(_ => ChangeZoom(-10));
            ResetZoomCommand = new RelayCommand(_ => ZoomLevel = 100);
        }

        private void DoNewFile()
        {
            Canvas.Shapes.Clear();
            Layers.Clear();
            Canvas.ClearSelection();
            ZoomLevel = 100;
        }

        private void DoOpenFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (dlg.ShowDialog() == true)
            {
                Canvas.OpenImage(dlg.FileName, Canvas.CanvasWidth, Canvas.CanvasHeight);
            }
        }

        private void DoSaveFile()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image|*.png|JPEG Image|*.jpg"
            };

            if (dlg.ShowDialog() == true)
            {
                Canvas.SaveCanvas(dlg.FileName, Canvas.CanvasWidth, Canvas.CanvasHeight);
            }
        }

        private void DoExit()
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void ChangeZoom(double delta)
        {
            double newZoom = ZoomLevel + delta;
            if (newZoom < 10) newZoom = 10;
            if (newZoom > 400) newZoom = 400;
            ZoomLevel = newZoom;
        }
    }
}
