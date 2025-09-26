using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PaintApplication.ViewModels;

namespace PaintApplication.Views
{
    public partial class CanvasView : UserControl
    {
        private CanvasViewModel VM => DataContext as CanvasViewModel;

        public CanvasView()
        {
            InitializeComponent();
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            VM?.MouseDownCommand.Execute(e.GetPosition(PART_Canvas));
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                VM?.MouseMoveCommand.Execute(e.GetPosition(PART_Canvas));
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            VM?.MouseUpCommand.Execute(null);
        }
    }
}
