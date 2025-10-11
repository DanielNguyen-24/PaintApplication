using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaintApplication.Models
{
    public enum BrushType
    {
        Brush,
        CalligraphyBrush,
        CalligraphyPen,
        Airbrush,
        OilBrush,
        Crayon,
        Marker,
        NaturalPencil,
        WatercolorBrush
    }
    public class BrushItem
    {
        public string Name { get; set; }
        public BrushType Type { get; set; }
        public string IconPath { get; set; } // có thể lưu path đến icon/ảnh minh họa
    }

}
