using System.ComponentModel;
using AgvDispatcher.Core.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.SystemConfigModule.Editor
{
    /// <summary>
    /// 地图可视化编辑器的充电桩画布项。
    /// <para>
    /// 包裹领域模型 <see cref="ChargeStation"/>，绘制位置跟随其所绑定的 Charge 节点画布项
    /// <see cref="EditorNodeVm"/>（订阅节点坐标变化随动）。充电桩以小方块叠加在节点旁，便于区分。
    /// </para>
    /// </summary>
    public class EditorStationVm : BindableBase, IDisposable
    {
        /// <summary>充电桩标记边长（画布像素）。</summary>
        public const double Size = 14;

        /// <summary>被包裹的领域充电桩模型（保存时直接落库此对象）。</summary>
        public ChargeStation Model { get; }

        private EditorNodeVm? _node;

        /// <summary>构造充电桩画布项；可选绑定到一个 Charge 节点画布项以随其坐标移动。</summary>
        /// <param name="model">底层领域充电桩。</param>
        /// <param name="node">绑定的 Charge 节点画布项（可为空，空时使用模型自带 Position）。</param>
        public EditorStationVm(ChargeStation model, EditorNodeVm? node)
        {
            Model = model;
            Attach(node);
        }

        /// <summary>重新绑定到指定 Charge 节点画布项（先解绑旧的，再订阅新的坐标变化）。</summary>
        /// <param name="node">新的绑定节点画布项。</param>
        public void Attach(EditorNodeVm? node)
        {
            if (_node is not null)
            {
                _node.PropertyChanged -= OnNodeChanged;
            }
            _node = node;
            if (_node is not null)
            {
                _node.PropertyChanged += OnNodeChanged;
            }
            RaiseGeometryChanged();
        }

        private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorNodeVm.X) || e.PropertyName == nameof(EditorNodeVm.Y))
            {
                RaiseGeometryChanged();
            }
        }

        /// <summary>解除对绑定节点的订阅。</summary>
        public void Dispose()
        {
            if (_node is not null)
            {
                _node.PropertyChanged -= OnNodeChanged;
            }
        }

        /// <summary>充电桩编号（只读展示）。</summary>
        public string StationId => Model.StationId;

        /// <summary>绑定节点编号（修改通过 ViewModel 重新 Attach）。</summary>
        public string NodeId => Model.NodeId;

        /// <summary>充电桩中心 X（跟随绑定节点；无绑定时用模型 Position）。</summary>
        private double CenterX => _node?.X ?? Model.Position.X;

        /// <summary>充电桩中心 Y（跟随绑定节点；无绑定时用模型 Position）。</summary>
        private double CenterY => _node?.Y ?? Model.Position.Y;

        /// <summary>方块绘制左上角 X（偏向节点右上角，避免完全盖住节点圆点）。</summary>
        public double CanvasLeft => CenterX + 8;

        /// <summary>方块绘制左上角 Y。</summary>
        public double CanvasTop => CenterY - 16;

        private bool _isSelected;
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

        /// <summary>填充色（启用紫色调，禁用灰）。</summary>
        public string Fill => Model.IsEnabled ? "#7B5BD6" : "#556070";

        /// <summary>描边颜色：选中=金色。</summary>
        public string Stroke => IsSelected ? "#FFD700" : "#D8C8FF";

        /// <summary>描边粗细：选中加粗。</summary>
        public double StrokeThickness => IsSelected ? 2.5 : 1;

        private void RaiseGeometryChanged()
        {
            RaisePropertyChanged(nameof(CanvasLeft));
            RaisePropertyChanged(nameof(CanvasTop));
        }
    }
}
