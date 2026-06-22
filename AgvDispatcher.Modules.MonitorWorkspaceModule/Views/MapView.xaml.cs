using System.Windows.Controls;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.Views
{
    public partial class MapView : UserControl
    {
        private System.Windows.Point _dragStartPoint;
        private bool _isDragging;
        private bool _isPanMode;

        public MapView()
        {
            InitializeComponent();
        }

        private void MapContainer_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var element = sender as System.Windows.UIElement;
            if (element == null) return;

            var position = e.GetPosition(element);
            ZoomAt(position, e.Delta > 0 ? 1.1 : 1 / 1.1);
            
            e.Handled = true;
        }

        private void MapContainer_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isPanMode) return;
            BeginPan(sender as System.Windows.UIElement, e);
        }

        private void MapContainer_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isPanMode) return;
            EndPan(sender as System.Windows.UIElement, e);
        }

        private void MapContainer_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = sender as System.Windows.UIElement;
            if (element == null) return;

            // Optional: double right click to reset zoom/pan
            if (e.ClickCount == 2)
            {
                MapScaleTransform.ScaleX = 1;
                MapScaleTransform.ScaleY = 1;
                MapTranslateTransform.X = 0;
                MapTranslateTransform.Y = 0;
                return;
            }

            BeginPan(element, e);
        }

        private void MapContainer_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EndPan(sender as System.Windows.UIElement, e);
        }

        private void MapContainer_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging)
            {
                var position = e.GetPosition(this);
                // The amount moved by mouse is directly applied to the translation
                MapTranslateTransform.X += position.X - _dragStartPoint.X;
                MapTranslateTransform.Y += position.Y - _dragStartPoint.Y;
                _dragStartPoint = position;
                e.Handled = true;
            }
        }

        private void TogglePanMode_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _isPanMode = !_isPanMode;
            if (sender is Button button)
            {
                button.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_isPanMode ? "#1E90FF" : "#12304A"));
                button.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void ZoomIn_Click(object sender, System.Windows.RoutedEventArgs e)
            => ZoomAt(new System.Windows.Point(ActualWidth / 2, ActualHeight / 2), 1.15);

        private void ZoomOut_Click(object sender, System.Windows.RoutedEventArgs e)
            => ZoomAt(new System.Windows.Point(ActualWidth / 2, ActualHeight / 2), 1 / 1.15);

        private void CenterMap_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            MapScaleTransform.ScaleX = 1;
            MapScaleTransform.ScaleY = 1;
            MapTranslateTransform.X = 0;
            MapTranslateTransform.Y = 0;
        }

        private void BeginPan(System.Windows.UIElement? element, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (element == null) return;

            _dragStartPoint = e.GetPosition(this);
            element.CaptureMouse();
            _isDragging = true;
            e.Handled = true;
        }

        private void EndPan(System.Windows.UIElement? element, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (element == null) return;

            element.ReleaseMouseCapture();
            _isDragging = false;
            e.Handled = true;
        }

        private void ZoomAt(System.Windows.Point position, double zoomFactor)
        {
            if (MapScaleTransform.ScaleX * zoomFactor < 0.2) zoomFactor = 0.2 / MapScaleTransform.ScaleX;
            if (MapScaleTransform.ScaleX * zoomFactor > 10.0) zoomFactor = 10.0 / MapScaleTransform.ScaleX;

            MapScaleTransform.ScaleX *= zoomFactor;
            MapScaleTransform.ScaleY *= zoomFactor;
            MapTranslateTransform.X = (MapTranslateTransform.X - position.X) * zoomFactor + position.X;
            MapTranslateTransform.Y = (MapTranslateTransform.Y - position.Y) * zoomFactor + position.Y;
        }
    }
}
