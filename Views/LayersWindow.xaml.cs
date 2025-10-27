using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using PaintApplication.Models;
using PaintApplication.ViewModels;

namespace PaintApplication.Views
{
    public partial class LayersWindow : Window
    {
        private MainViewModel? VM => DataContext as MainViewModel;
        public ObservableCollection<ShapeItemWrapper> ShapeItems { get; } = new();
        
        private ShapeItemWrapper? _draggedItem;
        private int _draggedIndex = -1;

        public LayersWindow()
        {
            InitializeComponent();
            Loaded += LayersWindow_Loaded;
            Closing += LayersWindow_Closing;
        }

        private void LayersWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (VM?.Canvas == null) return;

            RefreshShapesList();
            VM.Canvas.Shapes.CollectionChanged += Shapes_CollectionChanged;
        }

        private void LayersWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (VM?.Canvas != null)
            {
                VM.Canvas.Shapes.CollectionChanged -= Shapes_CollectionChanged;
            }
        }

        private void Shapes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshShapesList();
        }

        private void RefreshShapesList()
        {
            if (VM?.Canvas == null) return;

            ShapeItems.Clear();
            for (int i = 0; i < VM.Canvas.Shapes.Count; i++)
            {
                var element = VM.Canvas.Shapes[i];
                var typeName = GetShapeTypeName(element);
                ShapeItems.Add(new ShapeItemWrapper(element, i, typeName));
            }
        }

        private string GetShapeTypeName(UIElement element)
        {
            return element switch
            {
                Line => "Line",
                Rectangle => "Rectangle",
                Ellipse => "Ellipse / Circle",
                Polygon polygon => polygon.Points.Count == 3 ? "Triangle" :
                                  polygon.Points.Count == 10 ? "Star" :
                                  polygon.Points.Count == 5 ? "Pentagon" :
                                  polygon.Points.Count == 6 ? "Hexagon" :
                                  polygon.Points.Count == 4 ? "Diamond" : "Polygon",
                Polyline => "Pencil / Brush Stroke",
                Path => "Custom Shape",
                TextBlock tb => $"Text: {tb.Text?.Substring(0, System.Math.Min(15, tb.Text?.Length ?? 0)) ?? ""}",
                Image => "Image",
                _ => element.GetType().Name
            };
        }

        private void ShapesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Could add visual feedback here
        }

        private void DeleteShape_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || ShapesListBox.SelectedItem is not ShapeItemWrapper wrapper) return;

            VM.Canvas.Shapes.Remove(wrapper.Element);
            VM.Canvas.PushUndoState();
        }

        private void BringToFront_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || ShapesListBox.SelectedItem is not ShapeItemWrapper wrapper) return;

            int currentIndex = VM.Canvas.Shapes.IndexOf(wrapper.Element);
            if (currentIndex >= 0 && currentIndex < VM.Canvas.Shapes.Count - 1)
            {
                VM.Canvas.Shapes.Move(currentIndex, VM.Canvas.Shapes.Count - 1);
                VM.Canvas.PushUndoState();
            }
        }

        private void SendToBack_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || ShapesListBox.SelectedItem is not ShapeItemWrapper wrapper) return;

            int currentIndex = VM.Canvas.Shapes.IndexOf(wrapper.Element);
            if (currentIndex > 0)
            {
                VM.Canvas.Shapes.Move(currentIndex, 0);
                VM.Canvas.PushUndoState();
            }
        }

        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.Content is ShapeItemWrapper wrapper)
            {
                _draggedItem = wrapper;
                _draggedIndex = ShapeItems.IndexOf(wrapper);
                DragDrop.DoDragDrop(item, wrapper, DragDropEffects.Move);
            }
        }

        private void ListBoxItem_Drop(object sender, DragEventArgs e)
        {
            if (_draggedItem == null || VM == null) return;

            if (sender is ListBoxItem targetItem && targetItem.Content is ShapeItemWrapper targetWrapper)
            {
                // Get actual indices in Canvas.Shapes
                int oldCanvasIndex = VM.Canvas.Shapes.IndexOf(_draggedItem.Element);
                int newCanvasIndex = VM.Canvas.Shapes.IndexOf(targetWrapper.Element);
                
                if (oldCanvasIndex >= 0 && newCanvasIndex >= 0 && oldCanvasIndex != newCanvasIndex)
                {
                    // Remove and insert at new position
                    VM.Canvas.Shapes.RemoveAt(oldCanvasIndex);
                    
                    // Adjust index if needed
                    if (oldCanvasIndex < newCanvasIndex)
                    {
                        newCanvasIndex--;
                    }
                    
                    VM.Canvas.Shapes.Insert(newCanvasIndex, _draggedItem.Element);
                    VM.Canvas.PushUndoState();
                }
            }

            _draggedItem = null;
            _draggedIndex = -1;
        }

        private void ListBoxItem_DragOver(object sender, DragEventArgs e)
        {
            if (_draggedItem != null)
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
    }
}
