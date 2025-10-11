using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PaintApplication.ViewModels;

namespace PaintApplication.Views
{
    public partial class CanvasView : UserControl
    {
        private CanvasViewModel? VM => DataContext as CanvasViewModel;

        public CanvasView()
        {
            InitializeComponent();
            DataContextChanged += CanvasView_DataContextChanged;
            Loaded += CanvasView_Loaded;
        }

        private void CanvasView_Loaded(object sender, RoutedEventArgs e)
        {
            HookupCollection();

            if (VM != null)
            {
                VM.UpdateCanvasSize((int)PART_Canvas.ActualWidth, (int)PART_Canvas.ActualHeight);
            }

            PART_Canvas.SizeChanged += (s, ev) =>
            {
                if (VM != null)
                    VM.UpdateCanvasSize((int)PART_Canvas.ActualWidth, (int)PART_Canvas.ActualHeight);
            };
        }


        private void CanvasView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UnhookOld(e.OldValue as CanvasViewModel);
            HookupCollection();
        }

        private void UnhookOld(CanvasViewModel? oldVm)
        {
            if (oldVm == null) return;
            oldVm.Shapes.CollectionChanged -= Shapes_CollectionChanged;
        }

        private void HookupCollection()
        {
            if (VM == null) return;

            // Clear current children and add all shapes in VM.Shapes
            PART_Canvas.Children.Clear();
            foreach (var el in VM.Shapes)
                PART_Canvas.Children.Add(el);

            // subscribe for future changes
            VM.Shapes.CollectionChanged -= Shapes_CollectionChanged;
            VM.Shapes.CollectionChanged += Shapes_CollectionChanged;
        }

        private void Shapes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Always update PART_Canvas children to match collection changes.
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (UIElement el in e.NewItems)
                {
                    PART_Canvas.Children.Add(el);

                    if (el is TextBox textBox && Equals(textBox.Tag, "CanvasTextInput"))
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            textBox.Focus();
                            textBox.CaretIndex = textBox.Text.Length;
                        }), DispatcherPriority.Background);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (UIElement el in e.OldItems)
                    PART_Canvas.Children.Remove(el);
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                PART_Canvas.Children.Clear();
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                if (e.OldItems != null)
                    foreach (UIElement el in e.OldItems)
                        PART_Canvas.Children.Remove(el);
                if (e.NewItems != null)
                    foreach (UIElement el in e.NewItems)
                        PART_Canvas.Children.Add(el);
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (VM == null) return;

            var position = e.GetPosition(PART_Canvas);
            VM.MousePosition = position;
            VM.MouseDownCommand.Execute(position);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (VM == null) return;

            var position = e.GetPosition(PART_Canvas);
            VM.MousePosition = position;
            VM.MouseMoveCommand.Execute(position);
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            VM?.MouseUpCommand.Execute(null);
        }

    }
}
 