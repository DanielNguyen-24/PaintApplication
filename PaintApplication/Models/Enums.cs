using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PaintApplication.Models
{
    public enum ToolType
    {
        None,
        Pencil,
        Brush,
        Eraser,
        Fill,
        Shape,   // Khi chọn Shape thì sẽ cần ShapeType để biết vẽ gì
        Text,
        Magnifier,
        Select
    }

    public enum ShapeType
    {
        None,
        Rectangle,
        Ellipse,
        Line,
        Triangle,
        Star,
        Diamond,
        Pentagon,
        Hexagon,
        Heart,
        Cloud,
        Star4,
        Star6,
        ArrowUp,
        ArrowDown,
        ArrowLeft,
        ArrowRight,
        Polygon,
        Freeform,
        Text,
        Lightning
    }

    public enum SelectionMode
    {
        Rectangle,
        Freeform
    }


}

