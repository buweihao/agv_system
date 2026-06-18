using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AgvDispatcher.Modules.SystemConfigModule.Editor;
using AgvDispatcher.Modules.SystemConfigModule.ViewModels;

namespace AgvDispatcher.Modules.SystemConfigModule.Views
{
    /// <summary>
    /// 地图可视化编辑器视图。
    /// <para>
    /// 画布交互在代码后置处理：节点拖拽（左键）、连线建边（连线模式下左键拖）、
    /// 框选多个节点（空白处左键拖）、缩放（滚轮）、平移（右键拖/空格+左键）。
    /// 坐标统一以 <see cref="_canvas"/> 自身坐标系为准（已含缩放变换），换算简单可靠。
    /// </para>
    /// </summary>
    public partial class MapEditorView : UserControl
    {
        private MapConfigViewModel? Vm => DataContext as MapConfigViewModel;

        // 拖拽节点状态
        private EditorNodeVm? _dragNode;
        private Point _dragStartCanvas;
        private double _dragNodeOrigX, _dragNodeOrigY;

        // 连线状态
        private EditorNodeVm? _connectFrom;

        // 框选状态
        private bool _isBoxSelecting;
        private Point _boxStart;

        // 平移状态（右键拖）
        private bool _isPanning;
        private Point _panStart;

        public MapEditorView()
        {
            InitializeComponent();
        }

        // ====== 缩放 / 平移 ======

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(_root);
            double factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            double next = _canvasScale.ScaleX * factor;
            if (next < 0.2) factor = 0.2 / _canvasScale.ScaleX;
            if (next > 6.0) factor = 6.0 / _canvasScale.ScaleX;

            _canvasScale.ScaleX *= factor;
            _canvasScale.ScaleY *= factor;
            _canvasTranslate.X = (_canvasTranslate.X - pos.X) * factor + pos.X;
            _canvasTranslate.Y = (_canvasTranslate.Y - pos.Y) * factor + pos.Y;
            e.Handled = true;
        }

        private void Root_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _canvasScale.ScaleX = _canvasScale.ScaleY = 1;
                _canvasTranslate.X = _canvasTranslate.Y = 0;
                return;
            }
            _isPanning = true;
            _panStart = e.GetPosition(_root);
            _root.CaptureMouse();
            e.Handled = true;
        }

        private void Root_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            _root.ReleaseMouseCapture();
            e.Handled = true;
        }

        // ====== 画布空白区：框选 / 取消选中 ======

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            // 点击空白：开始框选（非连线/放桩模式）
            if (!Vm.IsConnectMode && !Vm.IsPlaceStationMode)
            {
                Vm.SelectedEditorNode = null;
                Vm.SelectedEditorEdge = null;
                Vm.SelectedEditorStation = null;

                _isBoxSelecting = true;
                _boxStart = e.GetPosition(_canvas);
                Canvas.SetLeft(_selectionBox, _boxStart.X);
                Canvas.SetTop(_selectionBox, _boxStart.Y);
                _selectionBox.Width = 0;
                _selectionBox.Height = 0;
                _selectionBox.Visibility = Visibility.Visible;
                _canvas.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (Vm == null) return;
            var p = e.GetPosition(_canvas);
            Vm.UpdateReadout(p.X, p.Y);

            if (_isPanning)
            {
                var rp = e.GetPosition(_root);
                _canvasTranslate.X += rp.X - _panStart.X;
                _canvasTranslate.Y += rp.Y - _panStart.Y;
                _panStart = rp;
                return;
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
                double x = Math.Min(p.X, _boxStart.X), y = Math.Min(p.Y, _boxStart.Y);
                double w = Math.Abs(p.X - _boxStart.X), h = Math.Abs(p.Y - _boxStart.Y);
                Canvas.SetLeft(_selectionBox, x);
                Canvas.SetTop(_selectionBox, y);
                _selectionBox.Width = w;
                _selectionBox.Height = h;
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
                // 命中矩形内第一个节点作为选中（多选高亮可后续扩展；此处选中代表项）
                double x = Canvas.GetLeft(_selectionBox), y = Canvas.GetTop(_selectionBox);
                var rect = new Rect(x, y, _selectionBox.Width, _selectionBox.Height);
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

        // ====== 节点：拖拽 / 选中 / 连线起点 / 放桩 ======

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            if (sender is not FrameworkElement fe || fe.DataContext is not EditorNodeVm node) return;

            if (Vm.IsPlaceStationMode)
            {
                Vm.PlaceStationOnNode(node);
                e.Handled = true;
                return;
            }

            if (Vm.IsConnectMode)
            {
                _connectFrom = node;
                Vm.SelectedEditorNode = node;
                e.Handled = true;
                return;
            }

            // 普通模式：选中并准备拖拽
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
            ((UIElement)sender).ReleaseMouseCapture();

            if (_dragNode != null)
            {
                Vm.RecordMove(_dragNode, _dragNodeOrigX, _dragNodeOrigY, _dragNode.X, _dragNode.Y);
                _dragNode = null;
                e.Handled = true;
                return;
            }

            if (Vm.IsConnectMode && _connectFrom != null
                && sender is FrameworkElement fe && fe.DataContext is EditorNodeVm target)
            {
                Vm.ConnectNodes(_connectFrom, target);
                _connectFrom = null;
                e.Handled = true;
            }
        }

        // ====== 边 / 充电桩：点击选中 ======

        private void Edge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorEdgeVm edge && !Vm.IsConnectMode && !Vm.IsPlaceStationMode)
            {
                Vm.SelectedEditorEdge = edge;
                e.Handled = true;
            }
        }

        private void Station_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorStationVm station && !Vm.IsConnectMode && !Vm.IsPlaceStationMode)
            {
                Vm.SelectedEditorStation = station;
                e.Handled = true;
            }
        }

        // ====== 键盘：Delete 删除 / Ctrl+Z 撤销 / Ctrl+Y 重做 ======

        private void Area_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            if (sender is FrameworkElement fe && fe.DataContext is EditorAreaVm area && !Vm.IsConnectMode && !Vm.IsPlaceStationMode)
            {
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
    }
}
