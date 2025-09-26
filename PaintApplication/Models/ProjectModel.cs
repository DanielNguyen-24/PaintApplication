using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Drawing;

namespace PaintApplication.Models
{
  public class ProjectModel
    {
        public ObservableCollection<LayerModel> Layers { get; set; } = new ObservableCollection<LayerModel>();
        public double CanvasWidth { get; set; } = 1200;
        public double CanvasHeight { get; set; } = 800;
    }
}
