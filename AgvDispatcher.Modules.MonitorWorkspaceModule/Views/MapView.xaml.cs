using System.Windows.Controls;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.Views
{
    public partial class MapView : UserControl
    {
        private System.Windows.Point _dragStartPoint;
        private bool _isDragging;

        public MapView()
        {
            InitializeComponent();
        }

        private void MapContainer_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var element = sender as System.Windows.UIElement;
            if (element == null) return;

            var position = e.GetPosition(element);
            
            // Zoom speed
            double zoomFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            
            // Limit zooming out too much or zooming in too much
            if (MapScaleTransform.ScaleX * zoomFactor < 0.2) zoomFactor = 0.2 / MapScaleTransform.ScaleX;
            if (MapScaleTransform.ScaleX * zoomFactor > 10.0) zoomFactor = 10.0 / MapScaleTransform.ScaleX;

            MapScaleTransform.ScaleX *= zoomFactor;
            MapScaleTransform.ScaleY *= zoomFactor;
            
            // Adjust translation to keep the point under the cursor fixed
            MapTranslateTransform.X = (MapTranslateTransform.X - position.X) * zoomFactor + position.X;
            MapTranslateTransform.Y = (MapTranslateTransform.Y - position.Y) * zoomFactor + position.Y;
            
            e.Handled = true;
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

            _dragStartPoint = e.GetPosition(this); // Position relative to UserControl
            element.CaptureMouse();
            _isDragging = true;
            e.Handled = true;
        }

        private void MapContainer_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = sender as System.Windows.UIElement;
            if (element == null) return;

            element.ReleaseMouseCapture();
            _isDragging = false;
            e.Handled = true;
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
    }
}
