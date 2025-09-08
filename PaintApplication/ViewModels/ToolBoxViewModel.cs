using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using PaintApplication.Models;

namespace PaintApplication.ViewModels
{
    public class ToolboxViewModel : INotifyPropertyChanged
    {
        private Color _selectedColor = Colors.Black;
        private double _thickness = 2.0;
        private ToolType _selectedTool = ToolType.Pencil; // mặc định là bút chì

        public ToolboxViewModel()
        {
            // Danh sách màu có sẵn
            ColorsList = new ObservableCollection<Color>
            {
                Colors.Black,
                Colors.Red,
                Colors.Blue,
                Colors.Green,
                Colors.Yellow,
                Colors.Purple
            };

            // Gán lệnh chọn tool
            SelectToolCommand = new RelayCommand<ToolType>(tool => SelectedTool = tool);
        }

        public ObservableCollection<Color> ColorsList { get; }

        public Color SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (_selectedColor != value)
                {
                    _selectedColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Thickness
        {
            get => _thickness;
            set
            {
                if (_thickness != value)
                {
                    _thickness = value;
                    OnPropertyChanged();
                }
            }
        }

        public ToolType SelectedTool
        {
            get => _selectedTool;
            set
            {
                if (_selectedTool != value)
                {
                    _selectedTool = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand SelectToolCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // RelayCommand để bind command
    public class RelayCommand<T> : ICommand
    {
        private readonly System.Action<T> _execute;
        private readonly System.Predicate<T> _canExecute;

        public RelayCommand(System.Action<T> execute, System.Predicate<T> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)parameter);

        public void Execute(object parameter) => _execute((T)parameter);

        public event System.EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
