using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PaintApplication.Services
{
    public class ExportService
    {
        public void ExportToPng(FrameworkElement element, string filePath)
        {
            var rtb = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.OpenWrite(filePath);
            encoder.Save(fs);
        }
    }
}
