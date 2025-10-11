using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PaintApplication.Helpers;
using PaintApplication.Models;

namespace PaintApplication.ViewModels
{
  public class LayerViewModel : ViewModelBase
    {
        private LayerModel _model;
        public LayerViewModel(LayerModel model) { _model = model; }

        public string Name
        {
            get => _model.Name;
            set
            {
                _model.Name = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ShapeModel> Shapes
        {
            get => _model.Shapes;
            set
            {
                _model.Shapes = value;
                OnPropertyChanged();
            }
        }

        public bool IsVisible
        {
            get => _model.IsVisible;
            set
            {
                _model.IsVisible = value;
                OnPropertyChanged();
            }
        }
        public double Opacity
        {
            get => _model.Opacity;
            set
            {
                _model.Opacity = value;
                OnPropertyChanged();
            }
        }
    }
}
