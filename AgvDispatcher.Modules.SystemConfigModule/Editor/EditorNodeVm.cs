using System.Windows.Media;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.SystemConfigModule.Editor
{
    /// <summary>
    /// 地图可视化编辑器的节点画布项。
    /// <para>
    /// 包裹领域模型 <see cref="MapNode"/>，对外暴露可观察的画布坐标（<see cref="CanvasLeft"/>/<see cref="CanvasTop"/>）、
    /// 标签偏移、配色与选中态。拖拽时直接修改 <see cref="X"/>/<see cref="Y"/>，会同步回写底层
    /// <see cref="MapNode.Position"/> 并触发依赖属性刷新，使绑定到该节点的边端点随动。
    /// </para>
    /// <para>渲染样式（配色/描边）参考运行监控页 MapViewModel.CreateNodeItem 的约定，保持视觉一致。</para>
    /// </summary>
    public class EditorNodeVm : BindableBase
    {
        /// <summary>节点圆点直径（画布像素），拖拽偏移与标签位置均以此为基准。</summary>
        public const double NodeDiameter = 18;
        private const double Radius = NodeDiameter / 2;

        /// <summary>被包裹的领域节点模型（保存时直接落库此对象）。</summary>
        public MapNode Model { get; }

        /// <summary>构造画布节点项并按模型初始化坐标与样式。</summary>
        /// <param name="model">底层领域节点。</param>
        public EditorNodeVm(MapNode model)
        {
            Model = model;
        }

        /// <summary>节点编号（只读展示，取自模型）。</summary>
        public string NodeId => Model.NodeId;

        /// <summary>节点编码（缺省回退为节点编号）。</summary>
        public string NodeCode => string.IsNullOrWhiteSpace(Model.NodeCode) ? Model.NodeId : Model.NodeCode;

        /// <summary>节点逻辑 X 坐标（画布像素）。修改即写回模型并刷新绘制坐标。</summary>
        public double X
        {
            get => Model.Position.X;
            set
            {
                if (Model.Position.X != value)
                {
                    Model.Position.X = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(CanvasLeft));
                    RaisePropertyChanged(nameof(LabelLeft));
                }
            }
        }

        /// <summary>节点逻辑 Y 坐标（画布像素）。修改即写回模型并刷新绘制坐标。</summary>
        public double Y
        {
            get => Model.Position.Y;
            set
            {
                if (Model.Position.Y != value)
                {
                    Model.Position.Y = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(CanvasTop));
                    RaisePropertyChanged(nameof(LabelTop));
                }
            }
        }

        /// <summary>圆点绘制左上角 X（已按半径偏移使坐标落在圆心）。</summary>
        public double CanvasLeft => X - Radius;

        /// <summary>圆点绘制左上角 Y（已按半径偏移使坐标落在圆心）。</summary>
        public double CanvasTop => Y - Radius;

        /// <summary>标签左侧 X 偏移。</summary>
        public double LabelLeft => X + 12;

        /// <summary>标签顶部 Y 偏移。</summary>
        public double LabelTop => Y - 12;

        /// <summary>节点类型（修改即写回模型并刷新填充色）。</summary>
        public MapNodeType NodeType
        {
            get => Model.NodeType;
            set
            {
                if (Model.NodeType != value)
                {
                    Model.NodeType = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(Fill));
                }
            }
        }

        public string AreaCode
        {
            get => Model.AreaCode;
            set
            {
                if (Model.AreaCode != value)
                {
                    Model.AreaCode = value ?? string.Empty;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>是否启用（修改即写回模型并刷新配色/透明度）。</summary>
        public bool IsEnabled
        {
            get => Model.IsEnabled;
            set
            {
                if (Model.IsEnabled != value)
                {
                    Model.IsEnabled = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(Fill));
                    RaisePropertyChanged(nameof(Opacity));
                }
            }
        }

        private bool _isSelected;
        private bool _isBulkHighlighted;
        /// <summary>是否处于选中态（描边高亮）。</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    RaisePropertyChanged(nameof(Stroke));
                    RaisePropertyChanged(nameof(StrokeThickness));
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

        /// <summary>按节点类型/启用态着色（与监控页一致：取货蓝、放货青、充电紫、路口绿、禁用灰）。</summary>
        public string Fill => !Model.IsEnabled
            ? "#556070"
            : Model.NodeType switch
            {
                MapNodeType.Pickup => "#1E90FF",
                MapNodeType.Dropoff => "#00BFFF",
                MapNodeType.Charge => "#9370DB",
                MapNodeType.Intersection => "#00FF7F",
                _ => "#9FB7CC"
            };

        /// <summary>描边颜色：选中=金色，否则常规。</summary>
        public string Stroke => IsSelected || IsBulkHighlighted ? "#FFD700" : (Model.IsEnabled ? "#D8F3FF" : "#3A4452");

        /// <summary>描边粗细：选中加粗。</summary>
        public double StrokeThickness => IsSelected || IsBulkHighlighted ? 3 : 1;

        /// <summary>整体透明度（禁用节点半透明）。</summary>
        public double Opacity => Model.IsEnabled ? 1.0 : 0.5;
    }
}
