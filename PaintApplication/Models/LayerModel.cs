using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace PaintApplication.Models
{
    public class LayerModel
    {
        public string Name { get; set; } = "Layer";
        public ObservableCollection<ShapeModel> Shapes { get; set; } = new ();
        public bool IsVisible { get; set; } = true;
        public double Opacity { get; set; } = 1.0;
    }
}
