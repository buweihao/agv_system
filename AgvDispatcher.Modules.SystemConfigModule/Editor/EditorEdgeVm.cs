using System.ComponentModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.SystemConfigModule.Editor
{
    /// <summary>
    /// 地图可视化编辑器的边画布项。
    /// <para>
    /// 包裹领域模型 <see cref="MapEdge"/> 并持有两端节点画布项 <see cref="EditorNodeVm"/> 引用，
    /// 订阅其坐标变化，使边线段（<see cref="X1"/>..<see cref="Y2"/>）与方向箭头折点随节点拖拽实时联动。
    /// </para>
    /// <para>配色规则与监控页一致：禁用/封闭=红，正常=绿。</para>
    /// </summary>
    public class EditorEdgeVm : BindableBase, IDisposable
    {
        /// <summary>被包裹的领域边模型（保存时直接落库此对象）。</summary>
        public MapEdge Model { get; }

        private EditorNodeVm _from;
        private EditorNodeVm _to;

        /// <summary>构造画布边项并订阅两端节点坐标变化以联动重绘。</summary>
        /// <param name="model">底层领域边。</param>
        /// <param name="from">起点节点画布项。</param>
        /// <param name="to">终点节点画布项。</param>
        public EditorEdgeVm(MapEdge model, EditorNodeVm from, EditorNodeVm to)
        {
            Model = model;
            _from = from;
            _to = to;
            _from.PropertyChanged += OnEndpointChanged;
            _to.PropertyChanged += OnEndpointChanged;
        }

        private void OnEndpointChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorNodeVm.X) || e.PropertyName == nameof(EditorNodeVm.Y))
            {
                RaiseGeometryChanged();
            }
        }

        /// <summary>解除对两端节点的事件订阅，避免边被移除后仍被节点持有。</summary>
        public void Dispose()
        {
            _from.PropertyChanged -= OnEndpointChanged;
            _to.PropertyChanged -= OnEndpointChanged;
        }

        /// <summary>边编号（只读展示）。</summary>
        public string EdgeId => Model.EdgeId;

        public string FromNodeId => Model.FromNodeId;

        public string ToNodeId => Model.ToNodeId;

        /// <summary>起点 X 画布坐标。</summary>
        public double X1 => _from.X;
        /// <summary>起点 Y 画布坐标。</summary>
        public double Y1 => _from.Y;
        /// <summary>终点 X 画布坐标。</summary>
        public double X2 => _to.X;
        /// <summary>终点 Y 画布坐标。</summary>
        public double Y2 => _to.Y;

        public void ReverseEndpoints()
        {
            _from.PropertyChanged -= OnEndpointChanged;
            _to.PropertyChanged -= OnEndpointChanged;
            (_from, _to) = (_to, _from);
            (Model.FromNodeId, Model.ToNodeId) = (Model.ToNodeId, Model.FromNodeId);
            _from.PropertyChanged += OnEndpointChanged;
            _to.PropertyChanged += OnEndpointChanged;
            RaisePropertyChanged(nameof(FromNodeId));
            RaisePropertyChanged(nameof(ToNodeId));
            RaiseGeometryChanged();
        }

        /// <summary>边方向（修改即写回模型并刷新配色/箭头）。</summary>
        public EdgeDirection Direction
        {
            get => Model.Direction;
            set
            {
                if (Model.Direction != value)
                {
                    Model.Direction = value;
                    RaisePropertyChanged();
                    RaiseStyleChanged();
                    RaisePropertyChanged(nameof(ArrowPoints));
                    RaisePropertyChanged(nameof(ReverseArrowPoints));
                }
            }
        }

        /// <summary>是否启用（修改即写回模型并刷新配色）。</summary>
        public bool IsEnabled
        {
            get => Model.IsEnabled;
            set
            {
                if (Model.IsEnabled != value)
                {
                    Model.IsEnabled = value;
                    RaisePropertyChanged();
                    RaiseStyleChanged();
                }
            }
        }

        private bool _isSelected;
        private bool _isBulkHighlighted;
        /// <summary>是否处于选中态（加粗高亮）。</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    RaisePropertyChanged(nameof(StrokeThickness));
                    RaisePropertyChanged(nameof(Stroke));
                }
            }
        }

        public bool IsBulkHighlighted
        {
            get => _isBulkHighlighted;
            set
            {
                if (SetProperty(ref _isBulkHighlighted, value))
                {
                    RaisePropertyChanged(nameof(Stroke));
                    RaisePropertyChanged(nameof(StrokeThickness));
                }
            }
        }

        /// <summary>线条颜色：禁用/封闭=红，正常=绿；选中时统一金色。</summary>
        public string Stroke
        {
            get
            {
                if (IsSelected || IsBulkHighlighted) return "#FFD700";
                if (!Model.IsEnabled || Model.Direction == EdgeDirection.Closed) return "#FF4500";
                return "#00FF7F";
            }
        }

        /// <summary>线条粗细：选中加粗。</summary>
        public double StrokeThickness => IsSelected || IsBulkHighlighted ? 4 : 2;

        /// <summary>线条透明度。</summary>
        public double Opacity => 0.85;

        /// <summary>
        /// 方向箭头折线点串（"＞"形）：双向取靠终点侧一个正向箭头表意即可，
        /// 仅正向指向终点，仅反向指向起点，封闭不画箭头。点串供 Polyline.Points 绑定。
        /// </summary>
        public string? ArrowPoints
        {
            get
            {
                if (Model.Direction == EdgeDirection.Closed) return null;

                double ax = X1, ay = Y1, bx = X2, by = Y2;
                double dx = bx - ax, dy = by - ay;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-6) return null;

                double ux = dx / len, uy = dy / len;     // 单位方向
                double nx = -uy, ny = ux;                 // 单位法向
                double mx = (ax + bx) / 2, my = (ay + by) / 2; // 中点
                const double wing = 8, half = 6;
                if (Model.Direction == EdgeDirection.Bidirectional)
                {
                    mx = ax + dx * 0.6;
                    my = ay + dy * 0.6;
                }

                // 反向边：箭头指向起点
                if (Model.Direction == EdgeDirection.ReverseOnly)
                {
                    ux = -ux; uy = -uy;
                }

                double tipX = mx + ux * 4, tipY = my + uy * 4;
                double baseX = tipX - ux * wing, baseY = tipY - uy * wing;
                double w1x = baseX + nx * half, w1y = baseY + ny * half;
                double w2x = baseX - nx * half, w2y = baseY - ny * half;
                return $"{w1x:0.##},{w1y:0.##} {tipX:0.##},{tipY:0.##} {w2x:0.##},{w2y:0.##}";
            }
        }

        public string? ReverseArrowPoints
        {
            get
            {
                if (Model.Direction != EdgeDirection.Bidirectional) return null;

                double ax = X1, ay = Y1, bx = X2, by = Y2;
                double dx = bx - ax, dy = by - ay;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-6) return null;

                double ux = -dx / len, uy = -dy / len;
                double nx = -uy, ny = ux;
                double mx = (ax + bx) / 2, my = (ay + by) / 2;
                const double wing = 8, half = 6;
                mx = ax + dx * 0.4;
                my = ay + dy * 0.4;

                double tipX = mx + ux * 4, tipY = my + uy * 4;
                double baseX = tipX - ux * wing, baseY = tipY - uy * wing;
                double w1x = baseX + nx * half, w1y = baseY + ny * half;
                double w2x = baseX - nx * half, w2y = baseY - ny * half;
                return $"{w1x:0.##},{w1y:0.##} {tipX:0.##},{tipY:0.##} {w2x:0.##},{w2y:0.##}";
            }
        }

        private void RaiseGeometryChanged()
        {
            RaisePropertyChanged(nameof(X1));
            RaisePropertyChanged(nameof(Y1));
            RaisePropertyChanged(nameof(X2));
            RaisePropertyChanged(nameof(Y2));
            RaisePropertyChanged(nameof(ArrowPoints));
            RaisePropertyChanged(nameof(ReverseArrowPoints));
        }

        private void RaiseStyleChanged()
        {
            RaisePropertyChanged(nameof(Stroke));
        }
    }
}
