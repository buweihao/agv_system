using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Modules.SystemConfigModule.Editor;
using AgvDispatcher.Modules.SystemConfigModule.ViewModels;

namespace AgvDispatcher.Modules.SystemConfigModule.Views
{
    public partial class MapEditorView : UserControl
    {
        private MapConfigViewModel? Vm => DataContext as MapConfigViewModel;

        private EditorNodeVm? _dragNode;
        private Point _dragStartCanvas;
        private double _dragNodeOrigX, _dragNodeOrigY;

        private EditorNodeVm? _connectFrom;

        private bool _isBoxSelecting;
        private Point _boxStart;

        private bool _isPanning;
        private bool _isRightButtonDown;
        private Point _rightButtonDownAt;
        private Point _panStart;
        private DateTime _lastReadoutAt = DateTime.MinValue;
        private DateTime _lastSelectionPreviewAt = DateTime.MinValue;

        private readonly List<Point> _polygonPoints = new();

        public MapEditorView()
        {
            InitializeComponent();
        }

        private void Viewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(_viewport);
            ZoomAt(pos, e.Delta > 0 ? 1.1 : 1 / 1.1);
            e.Handled = true;
        }

        private void Viewport_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _viewport.Focus();

            if (Vm?.IsAddNodeMode == true)
            {
                var point = e.GetPosition(_canvas);
                Vm.AddNodeAt(point.X, point.Y);
                e.Handled = true;
            }
        }

        private void Viewport_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _viewport.Focus();
            _isRightButtonDown = true;
            _isPanning = false;
            _rightButtonDownAt = e.GetPosition(_viewport);
            _panStart = _rightButtonDownAt;
            _viewport.CaptureMouse();
            e.Handled = true;
        }

        private void Viewport_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var wasPanning = _isPanning;
            EndRightButtonInteraction(releaseCapture: true);

            if (!wasPanning && Vm?.AreaDrawMode == AreaDrawMode.Polygon && _polygonPoints.Count >= 3)
            {
                FinishPolygonArea();
            }

            e.Handled = true;
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isRightButtonDown) return;

            if (e.RightButton != MouseButtonState.Pressed)
            {
                EndRightButtonInteraction(releaseCapture: true);
                return;
            }

            var current = e.GetPosition(_viewport);
            if (!_isPanning)
            {
                var delta = current - _rightButtonDownAt;
                if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
                {
                    return;
                }

                _isPanning = true;
                _viewport.Cursor = Cursors.ScrollAll;
            }

            _canvasTranslate.X += current.X - _panStart.X;
            _canvasTranslate.Y += current.Y - _panStart.Y;
            _panStart = current;
            e.Handled = true;
        }

        private void Viewport_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isRightButtonDown)
            {
                EndRightButtonInteraction(releaseCapture: false);
            }
        }

        private void EndRightButtonInteraction(bool releaseCapture)
        {
            _isRightButtonDown = false;
            _isPanning = false;
            _viewport.Cursor = null;
            if (releaseCapture && _viewport.IsMouseCaptured)
            {
                _viewport.ReleaseMouseCapture();
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;

            if (Vm.AreaDrawMode == AreaDrawMode.Polygon)
            {
                var point = e.GetPosition(_canvas);
                if (e.ClickCount >= 2 && _polygonPoints.Count >= 2)
                {
                    _polygonPoints.Add(point);
                    FinishPolygonArea();
                }
                else
                {
                    _polygonPoints.Add(point);
                    UpdatePolygonPreview(point);
                }

                e.Handled = true;
                return;
            }

            if (Vm.AreaDrawMode is AreaDrawMode.Rectangle or AreaDrawMode.EncloseElements or AreaDrawMode.DeleteElements)
            {
                Vm.ClearBulkHighlights();
                Vm.SelectedEditorNode = null;
                Vm.SelectedEditorEdge = null;
                Vm.SelectedEditorStation = null;
                if (Vm.AreaDrawMode == AreaDrawMode.Rectangle)
                {
                    Vm.SelectedEditorArea = null;
                }

                BeginSelectionBox(e.GetPosition(_canvas));
                e.Handled = true;
                return;
            }

            if (!Vm.IsConnectMode && !Vm.IsPlaceStationMode)
            {
                Vm.ClearBulkHighlights();
                Vm.SelectedEditorNode = null;
                Vm.SelectedEditorEdge = null;
                Vm.SelectedEditorStation = null;

                BeginSelectionBox(e.GetPosition(_canvas));
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (Vm == null) return;
            var p = e.GetPosition(_canvas);
            if (ShouldRunThrottled(ref _lastReadoutAt, 33))
            {
                Vm.UpdateReadout(p.X, p.Y);
            }

            if (_dragNode != null)
            {
                double nx = Vm.Snap(_dragNodeOrigX + (p.X - _dragStartCanvas.X));
                double ny = Vm.Snap(_dragNodeOrigY + (p.Y - _dragStartCanvas.Y));
                _dragNode.X = nx;
                _dragNode.Y = ny;
                return;
            }

            if (_isBoxSelecting)
            {
                UpdateSelectionBox(p);
                if (!ShouldRunThrottled(ref _lastSelectionPreviewAt, 33))
                {
                    return;
                }

                if (Vm.AreaDrawMode == AreaDrawMode.EncloseElements)
                {
                    Vm.PreviewEnclosedElements(_boxStart.X, _boxStart.Y, p.X, p.Y);
                }
                else if (Vm.AreaDrawMode == AreaDrawMode.DeleteElements)
                {
                    Vm.PreviewDeleteElements(_boxStart.X, _boxStart.Y, p.X, p.Y);
                }
                else if (!Vm.IsConnectMode && !Vm.IsPlaceStationMode)
                {
                    Vm.PreviewEnclosedElements(_boxStart.X, _boxStart.Y, p.X, p.Y);
                }
            }

            if (Vm.AreaDrawMode == AreaDrawMode.Polygon && _polygonPoints.Count > 0)
            {
                UpdatePolygonPreview(p);
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;

            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                _canvas.ReleaseMouseCapture();
                _selectionBox.Visibility = Visibility.Collapsed;
                var end = e.GetPosition(_canvas);

                if (Vm.AreaDrawMode == AreaDrawMode.Rectangle)
                {
                    Vm.CreateAreaFromRectangle(_boxStart.X, _boxStart.Y, end.X, end.Y);
                    Vm.AreaDrawMode = AreaDrawMode.None;
                    e.Handled = true;
                    return;
                }

                if (Vm.AreaDrawMode == AreaDrawMode.EncloseElements)
                {
                    Vm.AssignEnclosedElementsToArea(_boxStart.X, _boxStart.Y, end.X, end.Y);
                    Vm.AreaDrawMode = AreaDrawMode.None;
                    e.Handled = true;
                    return;
                }

                if (Vm.AreaDrawMode == AreaDrawMode.DeleteElements)
                {
                    Vm.DeleteElementsInRectangle(_boxStart.X, _boxStart.Y, end.X, end.Y);
                    Vm.AreaDrawMode = AreaDrawMode.None;
                    e.Handled = true;
                    return;
                }

                var rect = CurrentSelectionRect();
                Vm.PreviewEnclosedElements(rect.Left, rect.Top, rect.Right, rect.Bottom);
                foreach (var n in Vm.EditorNodes)
                {
                    if (rect.Contains(new Point(n.X, n.Y)))
                    {
                        Vm.SelectedEditorNode = n;
                        break;
                    }
                }
            }
        }

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            if (sender is not FrameworkElement fe || fe.DataContext is not EditorNodeVm node) return;

            if (Vm.AreaDrawMode != AreaDrawMode.None)
            {
                return;
            }

            if (Vm.IsPlaceStationMode)
            {
                Vm.PlaceStationOnNode(node);
                e.Handled = true;
                return;
            }

            if (Vm.IsConnectMode)
            {
                if (_connectFrom is not null && !ReferenceEquals(_connectFrom, node))
                {
                    Vm.ConnectNodes(_connectFrom, node);
                    _connectFrom = null;
                }
                else
                {
                    _connectFrom = node;
                    Vm.SelectedEditorNode = node;
                }
                e.Handled = true;
                return;
            }

            Vm.ClearBulkHighlights();
            Vm.SelectedEditorNode = node;
            _dragNode = node;
            _dragStartCanvas = e.GetPosition(_canvas);
            _dragNodeOrigX = node.X;
            _dragNodeOrigY = node.Y;
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;

            if (_dragNode != null)
            {
                Vm.RecordMove(_dragNode, _dragNodeOrigX, _dragNodeOrigY, _dragNode.X, _dragNode.Y);
                _dragNode = null;
                ((UIElement)sender).ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            ((UIElement)sender).ReleaseMouseCapture();

            if (Vm.IsConnectMode && _connectFrom != null
                && sender is FrameworkElement fe && fe.DataContext is EditorNodeVm target)
            {
                if (!ReferenceEquals(_connectFrom, target))
                {
                    Vm.ConnectNodes(_connectFrom, target);
                    _connectFrom = null;
                }
                e.Handled = true;
            }
        }

        private void Node_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_dragNode is null || Vm is null) return;
            if (sender is not FrameworkElement element || !ReferenceEquals(element.DataContext, _dragNode)) return;

            Vm.RecordMove(_dragNode, _dragNodeOrigX, _dragNodeOrigY, _dragNode.X, _dragNode.Y);
            _dragNode = null;
        }

        private void Canvas_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (!_isBoxSelecting) return;

            _isBoxSelecting = false;
            _selectionBox.Visibility = Visibility.Collapsed;
            Vm?.ClearBulkHighlights();
        }

        private void Edge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorEdgeVm edge && !Vm.IsConnectMode && !Vm.IsPlaceStationMode && Vm.AreaDrawMode == AreaDrawMode.None)
            {
                Vm.ClearBulkHighlights();
                Vm.SelectedEditorEdge = edge;
                e.Handled = true;
            }
        }

        private void Station_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorStationVm station && !Vm.IsConnectMode && !Vm.IsPlaceStationMode && Vm.AreaDrawMode == AreaDrawMode.None)
            {
                Vm.ClearBulkHighlights();
                Vm.SelectedEditorStation = station;
                e.Handled = true;
            }
        }

        private void Area_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorAreaVm area && !Vm.IsConnectMode && !Vm.IsPlaceStationMode && Vm.AreaDrawMode == AreaDrawMode.None)
            {
                Vm.ClearBulkHighlights();
                Vm.SelectedEditorArea = area;
                e.Handled = true;
            }
        }

        private void Root_KeyDown(object sender, KeyEventArgs e)
        {
            if (Vm == null) return;
            if (e.Key == Key.Delete)
            {
                if (Vm.DeleteSelectionCommand.CanExecute()) Vm.DeleteSelectionCommand.Execute();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelAreaDrawing();
                e.Handled = true;
            }
            else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (Vm.UndoCommand.CanExecute()) Vm.UndoCommand.Execute();
                e.Handled = true;
            }
            else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (Vm.RedoCommand.CanExecute()) Vm.RedoCommand.Execute();
                e.Handled = true;
            }
        }

        private void BeginSelectionBox(Point start)
        {
            _isBoxSelecting = true;
            _boxStart = start;
            _lastSelectionPreviewAt = DateTime.MinValue;
            Canvas.SetLeft(_selectionBox, _boxStart.X);
            Canvas.SetTop(_selectionBox, _boxStart.Y);
            _selectionBox.Width = 0;
            _selectionBox.Height = 0;
            _selectionBox.Visibility = Visibility.Visible;
            _canvas.CaptureMouse();
        }

        private void UpdateSelectionBox(Point current)
        {
            double x = Math.Min(current.X, _boxStart.X), y = Math.Min(current.Y, _boxStart.Y);
            double w = Math.Abs(current.X - _boxStart.X), h = Math.Abs(current.Y - _boxStart.Y);
            Canvas.SetLeft(_selectionBox, x);
            Canvas.SetTop(_selectionBox, y);
            _selectionBox.Width = w;
            _selectionBox.Height = h;
        }

        private Rect CurrentSelectionRect()
            => new(Canvas.GetLeft(_selectionBox), Canvas.GetTop(_selectionBox), _selectionBox.Width, _selectionBox.Height);

        private void FinishPolygonArea()
        {
            if (Vm == null) return;
            Vm.CreateAreaFromPolygon(_polygonPoints.Select(point => new MapPointDto { X = point.X, Y = point.Y }));
            CancelAreaDrawing();
        }

        private void CancelAreaDrawing()
        {
            _isBoxSelecting = false;
            _canvas.ReleaseMouseCapture();
            _selectionBox.Visibility = Visibility.Collapsed;
            _polygonPoints.Clear();
            _polygonPreview.Points.Clear();
            _polygonPreview.Visibility = Visibility.Collapsed;
            if (Vm != null)
            {
                Vm.AreaDrawMode = AreaDrawMode.None;
                Vm.ClearBulkHighlights();
            }
        }

        private void UpdatePolygonPreview(Point current)
        {
            _polygonPreview.Points.Clear();
            foreach (var point in _polygonPoints)
            {
                _polygonPreview.Points.Add(point);
            }

            if (_polygonPoints.Count > 0)
            {
                _polygonPreview.Points.Add(current);
            }

            _polygonPreview.Visibility = _polygonPreview.Points.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ZoomAt(Point pos, double factor)
        {
            double next = _canvasScale.ScaleX * factor;
            if (next < 0.2) factor = 0.2 / _canvasScale.ScaleX;
            if (next > 6.0) factor = 6.0 / _canvasScale.ScaleX;

            _canvasScale.ScaleX *= factor;
            _canvasScale.ScaleY *= factor;
            _canvasTranslate.X = (_canvasTranslate.X - pos.X) * factor + pos.X;
            _canvasTranslate.Y = (_canvasTranslate.Y - pos.Y) * factor + pos.Y;
        }

        private static bool ShouldRunThrottled(ref DateTime lastRunAt, int intervalMilliseconds)
        {
            var now = DateTime.UtcNow;
            if ((now - lastRunAt).TotalMilliseconds < intervalMilliseconds)
            {
                return false;
            }

            lastRunAt = now;
            return true;
        }
    }
}
