using System;
using System.Globalization;
using System.Windows.Data;
using PaintApplication.Models;

namespace PaintApplication.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string checkValue = value.ToString();
            string targetValue = parameter.ToString();
            return checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter == null)
                return null;

            if ((bool)value)
            {
                return Enum.Parse(typeof(ToolType), parameter.ToString());
            }

            return Binding.DoNothing; // không đổi khi unchecked
        }
    }
}
