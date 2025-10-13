using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PaintApplication.ViewModels;

namespace PaintApplication.Views
{
    public partial class CanvasView : UserControl
    {
        private CanvasViewModel? VM => DataContext as CanvasViewModel;

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register(
                nameof(ZoomLevel),
                typeof(double),
                typeof(CanvasView),
                new FrameworkPropertyMetadata(
                    100d,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnZoomLevelChanged));

        public double ZoomLevel
        {
            get => (double)GetValue(ZoomLevelProperty);
            set => SetValue(ZoomLevelProperty, value);
        }

        public CanvasView()
        {
            InitializeComponent();
            DataContextChanged += CanvasView_DataContextChanged;
            Loaded += CanvasView_Loaded;
            PreviewMouseWheel += CanvasView_PreviewMouseWheel;
            UpdateZoom();
        }

        private void CanvasView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                return;

            e.Handled = true;

            double delta = e.Delta > 0 ? 10 : -10;
            double newZoom = ZoomLevel + delta;
            if (newZoom < 10) newZoom = 10;
            if (newZoom > 400) newZoom = 400;

            ZoomLevel = newZoom;
        }

        private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CanvasView view)
            {
                view.UpdateZoom();
            }
        }

        private void UpdateZoom()
        {
            if (PART_ScaleTransform != null)
            {
                double scale = Math.Max(0.1, ZoomLevel / 100.0);
                PART_ScaleTransform.ScaleX = scale;
                PART_ScaleTransform.ScaleY = scale;
            }
        }

        private void CanvasView_Loaded(object sender, RoutedEventArgs e)
        {
            HookupCollection();

            if (VM != null)
            {
                VM.UpdateCanvasSize((int)PART_Canvas.ActualWidth, (int)PART_Canvas.ActualHeight);
            }

            PART_Canvas.SizeChanged -= PartCanvas_SizeChanged;
            PART_Canvas.SizeChanged += PartCanvas_SizeChanged;

            UpdateZoom();
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

            PART_Canvas.Children.Clear();
            foreach (var el in VM.Shapes)
                PART_Canvas.Children.Add(el);

            VM.Shapes.CollectionChanged -= Shapes_CollectionChanged;
            VM.Shapes.CollectionChanged += Shapes_CollectionChanged;
        }

        private void Shapes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
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

            if (e.LeftButton == MouseButtonState.Pressed)
                VM.MouseMoveCommand.Execute(position);
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            VM?.MouseUpCommand.Execute(null);
        }

        private void PartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (VM != null)
            {
                VM.UpdateCanvasSize((int)PART_Canvas.ActualWidth, (int)PART_Canvas.ActualHeight);
            }
        }
    }
}
