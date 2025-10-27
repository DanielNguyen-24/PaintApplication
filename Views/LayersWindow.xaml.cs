using System.Windows;
using System.Windows.Controls;
using PaintApplication.ViewModels;

namespace PaintApplication.Views
{
    public partial class LayersWindow : Window
    {
        private MainViewModel? VM => DataContext as MainViewModel;

        public LayersWindow()
        {
            InitializeComponent();
        }

        private void ShapesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VM == null || ShapesListBox.SelectedItem == null) return;
            
            // Highlight selected shape on canvas
            var selectedElement = ShapesListBox.SelectedItem as UIElement;
            // Could add visual feedback here
        }

        private void DeleteShape_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || ShapesListBox.SelectedItem == null) return;
            
            var selectedElement = ShapesListBox.SelectedItem as UIElement;
            if (selectedElement != null)
            {
                VM.Canvas.Shapes.Remove(selectedElement);
                VM.Canvas.PushUndoState();
            }
        }

        private void BringToFront_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || ShapesListBox.SelectedItem == null) return;
            
            var selectedElement = ShapesListBox.SelectedItem as UIElement;
            if (selectedElement != null)
            {
                int currentIndex = VM.Canvas.Shapes.IndexOf(selectedElement);
                if (currentIndex >= 0 && currentIndex < VM.Canvas.Shapes.Count - 1)
                {
                    VM.Canvas.Shapes.Move(currentIndex, VM.Canvas.Shapes.Count - 1);
                    VM.Canvas.PushUndoState();
                }
            }
        }

        private void SendToBack_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || ShapesListBox.SelectedItem == null) return;
            
            var selectedElement = ShapesListBox.SelectedItem as UIElement;
            if (selectedElement != null)
            {
                int currentIndex = VM.Canvas.Shapes.IndexOf(selectedElement);
                if (currentIndex > 0)
                {
                    VM.Canvas.Shapes.Move(currentIndex, 0);
                    VM.Canvas.PushUndoState();
                }
            }
        }
    }
}
