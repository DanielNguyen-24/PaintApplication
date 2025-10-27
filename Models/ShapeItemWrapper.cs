using System.Windows;

namespace PaintApplication.Models
{
    public class ShapeItemWrapper
    {
        public UIElement Element { get; set; }
        public int Index { get; set; }
        public string TypeName { get; set; }

        public ShapeItemWrapper(UIElement element, int index, string typeName)
        {
            Element = element;
            Index = index;
            TypeName = typeName;
        }

        public string DisplayName => $"Shape #{Index + 1}";
    }
}
