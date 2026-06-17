using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    /// <summary>
    /// 运行监控页面"地图视图"的 ViewModel。
    /// <para>
    /// 负责把地图拓扑（节点 <see cref="MapNode"/>、边 <see cref="MapEdge"/>）和车辆实时位置渲染到画布上，
    /// 并在选中某台 AGV 时基于独立路径规划服务高亮展示路径。
    /// 通过订阅 <see cref="SelectedVehicleChangedEvent"/> 与 <see cref="VehicleStateChangedEvent"/> 实现联动与刷新。
    /// 地图元素被点击时在详情区展示节点/路径/车辆的详细信息。
    /// </para>
    /// <para>
    /// 位置匹配支持别名（<see cref="IMapLocationAliasRepository"/>）、节点编号/编码精确匹配，
    /// 以及对形如 "A01" 的位置串做归一化的模糊匹配（见 <see cref="ResolveNode"/>）。
    /// </para>
    /// </summary>
    public class MapViewModel : BindableBase
    {
        private readonly IMapRepository _mapRepository;
        private readonly IPathPlanningService _pathPlanningService;
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly ITaskService _taskService;
        private readonly IMapLocationAliasRepository _aliasRepo;

        private IReadOnlyList<MapLocationAlias> _aliases = new List<MapLocationAlias>();

        private string? _selectedVehicleId;
        private string _pathSummary = "请选择AGV查看规划路径";

        // 路径规划预览：手动选择的起点/终点与结果摘要
        private MapNodeViewItem? _previewStartNode;
        private MapNodeViewItem? _previewEndNode;
        private string _previewSummary = "选择起点与终点后点击\"预览路径\"";

        /// <summary>地图全部边（普通渲染图层）。</summary>
        public ObservableCollection<MapEdgeViewItem> Edges { get; } = new();

        /// <summary>当前选中车辆的规划路径所经过的边（高亮图层）。</summary>
        public ObservableCollection<MapEdgeViewItem> PlannedEdges { get; } = new();

        /// <summary>手动预览路径所经过的边（预览高亮图层，独立于车辆路径）。</summary>
        public ObservableCollection<MapEdgeViewItem> PreviewEdges { get; } = new();

        /// <summary>各边的方向箭头（叠加在普通边图层之上，表达通行方向）。</summary>
        public ObservableCollection<MapEdgeArrowViewItem> EdgeArrows { get; } = new();

        /// <summary>地图全部节点。</summary>
        public ObservableCollection<MapNodeViewItem> Nodes { get; } = new();

        /// <summary>车辆当前位置标记。</summary>
        public ObservableCollection<VehicleMapViewItem> Vehicles { get; } = new();

        /// <summary>路径摘要文本（如起止点、点数、总里程，或不可用提示）。</summary>
        public string PathSummary
        {
            get => _pathSummary;
            set => SetProperty(ref _pathSummary, value);
        }

        private string _detailTitle = "详情";
        /// <summary>详情区标题。</summary>
        public string DetailTitle
        {
            get => _detailTitle;
            set => SetProperty(ref _detailTitle, value);
        }

        private string _detailContent = "点击地图元素查看详情";
        /// <summary>详情区内容。</summary>
        public string DetailContent
        {
            get => _detailContent;
            set => SetProperty(ref _detailContent, value);
        }

        /// <summary>地图元素点击命令，参数为被点击的节点/边/车辆视图项。</summary>
        public DelegateCommand<object> MapItemClickCommand { get; }

        /// <summary>路径预览的起点节点（供界面下拉绑定）。</summary>
        public MapNodeViewItem? PreviewStartNode
        {
            get => _previewStartNode;
            set
            {
                if (SetProperty(ref _previewStartNode, value))
                {
                    PreviewPathCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>路径预览的终点节点（供界面下拉绑定）。</summary>
        public MapNodeViewItem? PreviewEndNode
        {
            get => _previewEndNode;
            set
            {
                if (SetProperty(ref _previewEndNode, value))
                {
                    PreviewPathCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>路径预览结果摘要（起止点、点数、总里程或不可达提示）。</summary>
        public string PreviewSummary
        {
            get => _previewSummary;
            set => SetProperty(ref _previewSummary, value);
        }

        /// <summary>根据所选起点/终点计算并高亮显示预览路径。</summary>
        public DelegateCommand PreviewPathCommand { get; }

        /// <summary>清除当前预览路径与选择。</summary>
        public DelegateCommand ClearPreviewCommand { get; }

        /// <summary>
        /// 构造函数，注入依赖、加载别名并首次绘制地图，同时订阅选中车辆与状态变化事件。
        /// </summary>
        /// <param name="eventAggregator">事件聚合器。</param>
        /// <param name="mapService">地图服务，提供节点/边查询与路径规划。</param>
        /// <param name="vehicleStateStore">车辆状态存储，提供车辆实时位置。</param>
        /// <param name="taskService">任务服务，用于解析车辆当前任务的目标节点。</param>
        /// <param name="aliasRepo">地图别名仓储，用于位置别名到节点的映射。</param>
        public MapViewModel(
            IEventAggregator eventAggregator,
            IMapRepository mapRepository,
            IPathPlanningService pathPlanningService,
            IVehicleStateStore vehicleStateStore,
            ITaskService taskService,
            IMapLocationAliasRepository aliasRepo)
        {
            _mapRepository = mapRepository;
            _pathPlanningService = pathPlanningService;
            _vehicleStateStore = vehicleStateStore;
            _taskService = taskService;
            _aliasRepo = aliasRepo;

            MapItemClickCommand = new DelegateCommand<object>(OnMapItemClicked);
            PreviewPathCommand = new DelegateCommand(OnPreviewPath, CanPreviewPath);
            ClearPreviewCommand = new DelegateCommand(OnClearPreview);

            // 先异步加载别名，完成后在 UI 线程首次绘制地图
            LoadAliasesAsync().ContinueWith(_ => LoadMap(null), TaskScheduler.FromCurrentSynchronizationContext());
            // 选中车辆变化：重绘并高亮其路径
            eventAggregator.GetEvent<SelectedVehicleChangedEvent>().Subscribe(LoadMap, ThreadOption.UIThread);
            // 车辆状态变化：保持当前选中车辆并重绘
            eventAggregator.GetEvent<VehicleStateChangedEvent>().Subscribe(_ => LoadMap(_selectedVehicleId), ThreadOption.UIThread);
        }

        /// <summary>
        /// 地图元素被点击时填充详情区：分别处理节点、边、车辆三类视图项。
        /// 节点的别名信息异步获取后回填。
        /// </summary>
        private void OnMapItemClicked(object item)
        {
            if (item is MapNodeViewItem node)
            {
                DetailTitle = $"节点详情: {node.Name}";
                DetailContent = $"节点ID: {node.NodeId}\n类型: {node.NodeType}\n区域: {node.AreaCode}\n" +
                                $"启用状态: {(node.IsEnabled ? "是" : "否")}\n别名配置: 获取中...";
                
                // Fetch aliases asynchronously and update
                Task.Run(() => 
                {
                    var nodeAliases = _aliases.Where(a => a.NodeId == node.NodeId).Select(a => a.AliasValue);
                    var aliasStr = nodeAliases.Any() ? string.Join(", ", nodeAliases) : "无";
                    System.Windows.Application.Current.Dispatcher.Invoke(() => DetailContent = DetailContent.Replace("获取中...", aliasStr));
                });
            }
            else if (item is MapEdgeViewItem edge)
            {
                DetailTitle = $"路径详情";
                DetailContent = $"起始节点: {edge.EdgeId}\n从: {edge.FromNodeId}  到: {edge.ToNodeId}\n" +
                                $"方向: {(edge.IsBidirectional ? "双向" : "单向")}\n限制速度: {edge.MaxSpeed} m/s";
            }
            else if (item is VehicleMapViewItem vehicle)
            {
                DetailTitle = $"车辆详情: {vehicle.VehicleId}";
                var taskStr = string.IsNullOrWhiteSpace(vehicle.CurrentTaskId) ? "无任务" : vehicle.CurrentTaskId;
                DetailContent = $"状态: {vehicle.State}\n当前位置: {vehicle.Location}\n当前任务: {taskStr}\n电量: {vehicle.BatteryLevel:F1}%";
            }
        }

        /// <summary>异步加载全部地图位置别名到内存缓存。</summary>
        private async Task LoadAliasesAsync()
        {
            _aliases = await _aliasRepo.GetAllAsync();
        }

        /// <summary>
        /// 重新绘制整张地图：构建边/节点/车辆视图项，并对选中车辆的规划路径做高亮叠加。
        /// </summary>
        /// <param name="selectedVehicleId">当前选中的车辆 ID；为空表示不高亮任何路径。</param>
        private void LoadMap(string? selectedVehicleId)
        {
            _selectedVehicleId = selectedVehicleId;

            var nodes = GetNodes();
            var edges = GetEdges();
            var nodeMap = nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            var selectedVehicle = string.IsNullOrWhiteSpace(selectedVehicleId)
                ? null
                : _vehicleStateStore.GetVehicle(selectedVehicleId);
            // 计算选中车辆的规划路径，并取出其边集合用于高亮判定
            var plannedPath = CreateSelectedVehiclePath(selectedVehicle, nodes);
            var plannedEdgeIds = plannedPath?.Edges
                .Select(edge => edge.EdgeId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 普通边图层（仅渲染两端节点都存在的边）
            Edges.Clear();
            EdgeArrows.Clear();
            foreach (var edge in edges)
            {
                if (nodeMap.TryGetValue(edge.FromNodeId, out var fromNode)
                    && nodeMap.TryGetValue(edge.ToNodeId, out var toNode))
                {
                    var edgeItem = CreateEdgeItem(edge, fromNode, toNode, plannedEdgeIds.Contains(edge.EdgeId));
                    Edges.Add(edgeItem);
                    foreach (var arrow in CreateEdgeArrows(edge, fromNode, toNode, edgeItem.Stroke))
                    {
                        EdgeArrows.Add(arrow);
                    }
                }
            }

            // 规划路径高亮图层（金色加粗）
            PlannedEdges.Clear();
            if (plannedPath is not null)
            {
                foreach (var edge in plannedPath.Edges)
                {
                    if (nodeMap.TryGetValue(edge.FromNodeId, out var fromNode)
                        && nodeMap.TryGetValue(edge.ToNodeId, out var toNode))
                    {
                        var item = CreateEdgeItem(edge, fromNode, toNode, true);
                        item.Stroke = "#FFD700";
                        item.StrokeThickness = 5;
                        item.Opacity = 0.95;
                        PlannedEdges.Add(item);
                    }
                }
            }

            // 节点图层（路径经过的节点描边高亮）
            Nodes.Clear();
            foreach (var node in nodes)
            {
                Nodes.Add(CreateNodeItem(node, plannedPath?.Nodes.Any(pathNode =>
                    string.Equals(pathNode.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase)) == true));
            }

            // 车辆位置标记
            Vehicles.Clear();
            foreach (var vehicle in CreateVehiclePositions(nodes, selectedVehicleId))
            {
                Vehicles.Add(vehicle);
            }

            PathSummary = BuildPathSummary(selectedVehicle, plannedPath, nodes);

            // 重建节点集合后，重新解析预览起止点引用并刷新预览路径图层
            RefreshPreviewAfterReload(nodeMap);
        }

        /// <summary>
        /// 地图重载后修复预览起止点对新节点视图项的引用，并据此重绘预览路径图层。
        /// 若原起止点在新地图中不存在则清空预览。
        /// </summary>
        private void RefreshPreviewAfterReload(IReadOnlyDictionary<string, MapNode> nodeMap)
        {
            var startId = _previewStartNode?.NodeId;
            var endId = _previewEndNode?.NodeId;

            // 重新指向 Nodes 集合中的新实例（避免下拉框选中项与列表项不一致）
            _previewStartNode = string.IsNullOrEmpty(startId)
                ? null
                : Nodes.FirstOrDefault(n => string.Equals(n.NodeId, startId, StringComparison.OrdinalIgnoreCase));
            _previewEndNode = string.IsNullOrEmpty(endId)
                ? null
                : Nodes.FirstOrDefault(n => string.Equals(n.NodeId, endId, StringComparison.OrdinalIgnoreCase));
            RaisePropertyChanged(nameof(PreviewStartNode));
            RaisePropertyChanged(nameof(PreviewEndNode));
            PreviewPathCommand.RaiseCanExecuteChanged();

            // 若已有有效预览路径，则用新坐标重绘；起止点失效则清除预览图层
            if (PreviewEdges.Count > 0)
            {
                if (_previewStartNode is not null && _previewEndNode is not null)
                {
                    RenderPreviewPath(nodeMap);
                }
                else
                {
                    PreviewEdges.Clear();
                    PreviewSummary = "选择起点与终点后点击\"预览路径\"";
                }
            }
        }

        /// <summary>是否允许执行预览：已选择起点和终点，且两者不同。</summary>
        private bool CanPreviewPath()
            => _previewStartNode is not null
               && _previewEndNode is not null
               && !string.Equals(_previewStartNode.NodeId, _previewEndNode.NodeId, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 计算所选起点到终点的规划路径，渲染预览高亮图层并更新预览摘要。
        /// </summary>
        private void OnPreviewPath()
        {
            if (_previewStartNode is null || _previewEndNode is null)
            {
                return;
            }

            var nodeMap = GetNodes().ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            RenderPreviewPath(nodeMap);
        }

        /// <summary>
        /// 用当前起止点向地图服务请求规划路径，填充 <see cref="PreviewEdges"/> 高亮图层并设置 <see cref="PreviewSummary"/>。
        /// </summary>
        private void RenderPreviewPath(IReadOnlyDictionary<string, MapNode> nodeMap)
        {
            PreviewEdges.Clear();

            if (_previewStartNode is null || _previewEndNode is null)
            {
                PreviewSummary = "选择起点与终点后点击\"预览路径\"";
                return;
            }

            var startId = _previewStartNode.NodeId;
            var endId = _previewEndNode.NodeId;
            var path = BuildDisplayPath(startId, endId);

            if (path is null || !path.IsAvailable || path.Edges.Count == 0)
            {
                PreviewSummary = $"{startId} → {endId}："
                    + (string.IsNullOrWhiteSpace(path?.Message) ? "暂无可用路径" : path!.Message);
                return;
            }

            foreach (var edge in path.Edges)
            {
                if (nodeMap.TryGetValue(edge.FromNodeId, out var fromNode)
                    && nodeMap.TryGetValue(edge.ToNodeId, out var toNode))
                {
                    var item = CreateEdgeItem(edge, fromNode, toNode, true);
                    item.Stroke = "#00E5FF";   // 青色，区别于车辆金色路径
                    item.StrokeThickness = 4;
                    item.Opacity = 0.95;
                    PreviewEdges.Add(item);
                }
            }

            PreviewSummary = $"{path.StartNodeId} → {path.EndNodeId}，共 {path.Nodes.Count} 个点，{path.TotalLength:F0} m";
        }

        /// <summary>清除预览路径图层、重置起止点选择与摘要。</summary>
        private void OnClearPreview()
        {
            PreviewEdges.Clear();
            PreviewStartNode = null;
            PreviewEndNode = null;
            PreviewSummary = "选择起点与终点后点击\"预览路径\"";
        }

        /// <summary>
        /// 为选中车辆计算规划路径：解析其当前位置为起点、其任务目标为终点后调用地图服务规划。
        /// 起终点缺失或相同则返回 null。
        /// </summary>
        private PlannedPath? CreateSelectedVehiclePath(VehicleStatusSnapshot? vehicle, IReadOnlyList<MapNode> nodes)
        {
            if (vehicle is null)
            {
                return null;
            }

            var startNode = ResolveNode(vehicle.Location, nodes, _aliases);
            var targetNodeId = ResolveTargetNodeId(vehicle);

            if (startNode is null || string.IsNullOrWhiteSpace(targetNodeId))
            {
                return null;
            }

            var targetNode = ResolveNode(targetNodeId, nodes, _aliases);
            if (targetNode is null || string.Equals(startNode.NodeId, targetNode.NodeId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return BuildDisplayPath(startNode.NodeId, targetNode.NodeId);
        }

        private IReadOnlyList<MapNode> GetNodes()
        {
            return _mapRepository.GetNodesAsync().GetAwaiter().GetResult();
        }

        private IReadOnlyList<MapEdge> GetEdges()
        {
            return _mapRepository.GetEdgesAsync().GetAwaiter().GetResult();
        }

        private PlannedPath BuildDisplayPath(string startNodeId, string endNodeId)
        {
            return _pathPlanningService.PlanPath(GetNodes(), GetEdges(), startNodeId, endNodeId);
        }

        /// <summary>
        /// 构建路径摘要文本，覆盖：未选车、位置未匹配、无任务目标、无可用路径、正常路径等多种情形。
        /// </summary>
        private string BuildPathSummary(VehicleStatusSnapshot? vehicle, PlannedPath? plannedPath, IReadOnlyList<MapNode> nodes)
        {
            if (vehicle is null)
            {
                return "请选择AGV查看规划路径";
            }

            var startNode = ResolveNode(vehicle.Location, nodes, _aliases);
            var targetNodeId = ResolveTargetNodeId(vehicle);

            if (startNode is null)
            {
                return $"{vehicle.VehicleId} 当前位置 {vehicle.Location} 未匹配到地图节点";
            }

            if (string.IsNullOrWhiteSpace(targetNodeId))
            {
                return $"{vehicle.VehicleId} 当前无任务目标，仅显示车辆位置";
            }

            if (plannedPath is null)
            {
                return $"{vehicle.VehicleId} {startNode.NodeId} -> {targetNodeId} 暂无可用路径";
            }

            return plannedPath.IsAvailable
                ? $"{vehicle.VehicleId} {plannedPath.StartNodeId} -> {plannedPath.EndNodeId}，共 {plannedPath.Nodes.Count} 个点，{plannedPath.TotalLength:F0} m"
                : $"{vehicle.VehicleId} {plannedPath.Message}";
        }

        /// <summary>
        /// 解析车辆的目标节点：优先取其当前任务的目标节点；否则查找分配给该车且处于待执行/执行中的任务目标。
        /// </summary>
        private string? ResolveTargetNodeId(VehicleStatusSnapshot vehicle)
        {
            if (!string.IsNullOrWhiteSpace(vehicle.CurrentTaskId))
            {
                var task = _taskService.GetTask(vehicle.CurrentTaskId);
                if (!string.IsNullOrWhiteSpace(task?.TargetNodeId))
                {
                    return task.TargetNodeId;
                }
            }

            var activeTask = _taskService.GetTasks()
                .FirstOrDefault(task =>
                    string.Equals(task.AssignedVehicleId, vehicle.VehicleId, StringComparison.OrdinalIgnoreCase)
                    && task.State is TaskState.Pending or TaskState.Running);

            return activeTask?.TargetNodeId;
        }

        /// <summary>
        /// 由领域边模型构建画布边视图项：计算两端坐标，按状态区分着色
        /// （正常=绿、锁定=橙、禁用/封闭=红），规划路径上的边加粗。
        /// </summary>
        private static MapEdgeViewItem CreateEdgeItem(
            MapEdge edge,
            MapNode fromNode,
            MapNode toNode,
            bool isPlanned)
        {
            // 状态着色：禁用或封闭→红（物理阻断），锁定→橙（临时管控），正常→绿
            var isBlocked = !edge.IsEnabled || edge.Direction == EdgeDirection.Closed;
            string stroke;
            double opacity;
            if (isBlocked)
            {
                stroke = "#FF4500";
                opacity = 0.9;
            }
            else
            {
                stroke = "#00FF7F";
                opacity = 0.55;
            }

            return new MapEdgeViewItem
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                IsBidirectional = edge.Direction == EdgeDirection.Bidirectional,
                Direction = edge.Direction,
                IsEnabled = edge.IsEnabled,
                IsLocked = false,
                MaxSpeed = edge.MaxSpeed,
                X1 = fromNode.Position.X,
                Y1 = fromNode.Position.Y,
                X2 = toNode.Position.X,
                Y2 = toNode.Position.Y,
                Stroke = stroke,
                StrokeThickness = isPlanned ? 3 : 2,
                Opacity = opacity
            };
        }

        /// <summary>
        /// 为一条边生成方向箭头（"＞"形折线）：
        /// 双向→在中点两侧各一个反向箭头；仅正向→一个指向终点的箭头；
        /// 仅反向→一个指向起点的箭头；封闭→不画箭头。箭头颜色跟随边的状态色。
        /// </summary>
        private static IEnumerable<MapEdgeArrowViewItem> CreateEdgeArrows(
            MapEdge edge,
            MapNode fromNode,
            MapNode toNode,
            string stroke)
        {
            if (edge.Direction == EdgeDirection.Closed)
            {
                yield break;
            }

            double ax = fromNode.Position.X, ay = fromNode.Position.Y;
            double bx = toNode.Position.X, by = toNode.Position.Y;
            double dx = bx - ax, dy = by - ay;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6)
            {
                yield break; // 自环或重合点，无方向可言
            }

            // 单位方向与单位法向
            double ux = dx / len, uy = dy / len;
            double nx = -uy, ny = ux;
            double mx = (ax + bx) / 2, my = (ay + by) / 2; // 边中点

            const double wing = 7;   // 箭翼沿边方向回退长度
            const double half = 5;   // 箭翼横向半宽
            const double gap = 6;    // 双向箭头错开间距

            if (edge.Direction == EdgeDirection.Bidirectional)
            {
                // 指向终点的箭头（略偏向终点一侧）
                yield return BuildArrow(mx + ux * gap, my + uy * gap, ux, uy, nx, ny, wing, half, stroke);
                // 指向起点的箭头（反方向，略偏向起点一侧）
                yield return BuildArrow(mx - ux * gap, my - uy * gap, -ux, -uy, nx, ny, wing, half, stroke);
            }
            else if (edge.Direction == EdgeDirection.ReverseOnly)
            {
                yield return BuildArrow(mx, my, -ux, -uy, nx, ny, wing, half, stroke);
            }
            else // ForwardOnly
            {
                yield return BuildArrow(mx, my, ux, uy, nx, ny, wing, half, stroke);
            }
        }

        /// <summary>
        /// 在给定箭尖位置 (tipX,tipY) 和指向 (ux,uy) 处构建一个 "＞" 形箭头：
        /// 两翼端点 = 箭尖沿反方向回退 wing 后，分别沿法向 (nx,ny) 偏移 ±half。
        /// </summary>
        private static MapEdgeArrowViewItem BuildArrow(
            double tipX, double tipY,
            double ux, double uy,
            double nx, double ny,
            double wing, double half,
            string stroke)
        {
            double baseX = tipX - ux * wing, baseY = tipY - uy * wing;
            return new MapEdgeArrowViewItem
            {
                X1 = baseX + nx * half,
                Y1 = baseY + ny * half,
                Xc = tipX,
                Yc = tipY,
                X2 = baseX - nx * half,
                Y2 = baseY - ny * half,
                Stroke = stroke
            };
        }

        /// <summary>
        /// 由领域节点模型构建画布节点视图项：计算绘制坐标与标签偏移，按节点类型填色，
        /// 禁用节点统一灰显，规划路径上的节点描边高亮。
        /// </summary>
        private static MapNodeViewItem CreateNodeItem(MapNode node, bool isOnPlannedPath)
        {
            // 禁用节点灰显，明显区别于启用节点；启用节点按类型着色
            var fill = !node.IsEnabled
                ? "#556070"
                : node.NodeType switch
                {
                    MapNodeType.Pickup => "#1E90FF",
                    MapNodeType.Dropoff => "#00BFFF",
                    MapNodeType.Charge => "#9370DB",
                    MapNodeType.Intersection => "#00FF7F",
                    _ => "#9FB7CC"
                };

            string stroke;
            double strokeThickness;
            if (isOnPlannedPath)
            {
                stroke = "#FFD700";
                strokeThickness = 3;
            }
            else if (!node.IsEnabled)
            {
                stroke = "#3A4452";
                strokeThickness = 1;
            }
            else
            {
                stroke = "#D8F3FF";
                strokeThickness = 1;
            }

            return new MapNodeViewItem
            {
                NodeId = node.NodeId,
                Name = node.Name,
                NodeCode = string.IsNullOrWhiteSpace(node.NodeCode) ? node.NodeId : node.NodeCode,
                NodeType = node.NodeType.ToString(),
                AreaCode = node.AreaCode,
                IsEnabled = node.IsEnabled,
                X = node.Position.X,
                Y = node.Position.Y,
                CanvasLeft = node.Position.X - 9,
                CanvasTop = node.Position.Y - 9,
                LabelLeft = node.Position.X + 12,
                LabelTop = node.Position.Y - 12,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = strokeThickness,
                Opacity = node.IsEnabled ? 1.0 : 0.5
            };
        }

        /// <summary>
        /// 遍历所有车辆，将能匹配到地图节点的车辆生成位置标记；选中车辆用金色突出，其余按状态着色。
        /// </summary>
        private IEnumerable<VehicleMapViewItem> CreateVehiclePositions(IReadOnlyList<MapNode> nodes, string? selectedVehicleId)
        {
            foreach (var snapshot in _vehicleStateStore.GetAllVehicles())
            {
                var node = ResolveNode(snapshot.Location, nodes, _aliases);
                if (node is null)
                {
                    continue;
                }

                var isSelected = string.Equals(snapshot.VehicleId, selectedVehicleId, StringComparison.OrdinalIgnoreCase);
                yield return new VehicleMapViewItem
                {
                    VehicleId = snapshot.VehicleId,
                    CurrentTaskId = snapshot.CurrentTaskId ?? string.Empty,
                    State = snapshot.State.ToString(),
                    Location = snapshot.Location,
                    X = node.Position.X,
                    Y = node.Position.Y,
                    CanvasLeft = node.Position.X - 15,
                    CanvasTop = node.Position.Y - 28,
                    BatteryLevel = snapshot.BatteryLevel,
                    StateText = snapshot.State.ToString(),
                    Fill = isSelected
                        ? "#FFD700"
                        : snapshot.State switch
                        {
                            RobotState.Running => "#32CD32",
                            RobotState.Fault => "#FF4500",
                            RobotState.Idle => "#00BFFF",
                            RobotState.Offline => "#D3D3D3",
                            _ => "#FFD700"
                        }
                };
            }
        }

        /// <summary>
        /// 将一个位置字符串解析为地图节点，按以下优先级匹配：
        /// ① 启用的位置别名 → ② 节点 ID/编码精确匹配 → ③ 含 "Charge" 时回退到任一充电节点 →
        /// ④ 对形如 "A01" 的串归一化后匹配（去前导零并大写）→ ⑤ 按首字母作为区域/前缀兜底匹配。
        /// 无法解析时返回 null。
        /// </summary>
        private static MapNode? ResolveNode(string? location, IReadOnlyList<MapNode> nodes, IReadOnlyList<MapLocationAlias> aliases)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            var alias = aliases.FirstOrDefault(a => 
                a.IsEnabled && string.Equals(a.AliasValue, location, StringComparison.OrdinalIgnoreCase));
            if (alias != null)
            {
                var mappedNode = nodes.FirstOrDefault(n => n.NodeId == alias.NodeId);
                if (mappedNode != null) return mappedNode;
            }

            var exact = nodes.FirstOrDefault(node =>
                string.Equals(node.NodeId, location, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.NodeCode, location, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }

            if (location.Contains("Charge", StringComparison.OrdinalIgnoreCase))
            {
                return nodes.FirstOrDefault(node => node.NodeType == MapNodeType.Charge);
            }

            var normalized = Regex.Match(location, @"^([A-Za-z]+)0*(\d+)");
            if (normalized.Success)
            {
                var nodeId = $"{normalized.Groups[1].Value.ToUpperInvariant()}{normalized.Groups[2].Value}";
                var node = nodes.FirstOrDefault(item =>
                    string.Equals(item.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.NodeCode, nodeId, StringComparison.OrdinalIgnoreCase));
                if (node is not null)
                {
                    return node;
                }
            }

            var areaCode = location[..1].ToUpperInvariant();
            return nodes.FirstOrDefault(node =>
                string.Equals(node.AreaCode, areaCode, StringComparison.OrdinalIgnoreCase)
                || node.NodeId.StartsWith(areaCode, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>地图边的画布渲染视图项：携带两端坐标与样式（颜色/粗细/透明度）。</summary>
    public class MapEdgeViewItem
    {
        /// <summary>边编号。</summary>
        public string EdgeId { get; set; } = string.Empty;

        /// <summary>起始节点编号。</summary>
        public string FromNodeId { get; set; } = string.Empty;

        /// <summary>终止节点编号。</summary>
        public string ToNodeId { get; set; } = string.Empty;

        /// <summary>是否双向通行。</summary>
        public bool IsBidirectional { get; set; }

        /// <summary>边方向。</summary>
        public EdgeDirection Direction { get; set; }

        /// <summary>是否启用（禁用＝物理阻断）。</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>是否被锁定（临时占用/管控）。</summary>
        public bool IsLocked { get; set; }

        /// <summary>限速（m/s）。</summary>
        public double MaxSpeed { get; set; }

        /// <summary>起点 X 画布坐标。</summary>
        public double X1 { get; set; }

        /// <summary>起点 Y 画布坐标。</summary>
        public double Y1 { get; set; }

        /// <summary>终点 X 画布坐标。</summary>
        public double X2 { get; set; }

        /// <summary>终点 Y 画布坐标。</summary>
        public double Y2 { get; set; }

        /// <summary>线条颜色（十六进制色值）。</summary>
        public string Stroke { get; set; } = "#00FF7F";

        /// <summary>线条粗细。</summary>
        public double StrokeThickness { get; set; } = 2;

        /// <summary>线条透明度。</summary>
        public double Opacity { get; set; } = 0.6;
    }

    /// <summary>
    /// 边方向箭头的画布渲染项：用一段三点折线（"＞"形）表示通行方向，置于边中点附近。
    /// </summary>
    public class MapEdgeArrowViewItem
    {
        /// <summary>箭头一翼端点 X。</summary>
        public double X1 { get; set; }

        /// <summary>箭头一翼端点 Y。</summary>
        public double Y1 { get; set; }

        /// <summary>箭尖 X（指向通行方向）。</summary>
        public double Xc { get; set; }

        /// <summary>箭尖 Y（指向通行方向）。</summary>
        public double Yc { get; set; }

        /// <summary>箭头另一翼端点 X。</summary>
        public double X2 { get; set; }

        /// <summary>箭头另一翼端点 Y。</summary>
        public double Y2 { get; set; }

        /// <summary>箭头颜色（跟随所属边的状态色）。</summary>
        public string Stroke { get; set; } = "#00FF7F";

        /// <summary>箭头线宽。</summary>
        public double StrokeThickness { get; set; } = 2;

        /// <summary>箭头透明度。</summary>
        public double Opacity { get; set; } = 0.85;

        /// <summary>供 Polyline.Points 绑定的三点字符串。</summary>
        public string PointsText => $"{X1},{Y1} {Xc},{Yc} {X2},{Y2}";
    }

    /// <summary>地图节点的画布渲染视图项：携带绘制坐标、标签偏移与样式。</summary>
    public class MapNodeViewItem
    {
        /// <summary>节点编号。</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>节点名称。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>节点编码（缺省时回退为节点编号）。</summary>
        public string NodeCode { get; set; } = string.Empty;

        /// <summary>节点类型文本（取自 <c>MapNodeType</c>）。</summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>所属区域编码。</summary>
        public string AreaCode { get; set; } = string.Empty;

        /// <summary>是否启用。</summary>
        public bool IsEnabled { get; set; }

        /// <summary>节点逻辑 X 坐标。</summary>
        public double X { get; set; }

        /// <summary>节点逻辑 Y 坐标。</summary>
        public double Y { get; set; }

        /// <summary>圆点绘制左上角 X（已按半径偏移）。</summary>
        public double CanvasLeft { get; set; }

        /// <summary>圆点绘制左上角 Y（已按半径偏移）。</summary>
        public double CanvasTop { get; set; }

        /// <summary>标签左侧 X 偏移。</summary>
        public double LabelLeft { get; set; }

        /// <summary>标签顶部 Y 偏移。</summary>
        public double LabelTop { get; set; }

        /// <summary>填充颜色（按节点类型区分）。</summary>
        public string Fill { get; set; } = "#00BFFF";

        /// <summary>描边颜色（路径上节点高亮为金色）。</summary>
        public string Stroke { get; set; } = "#D8F3FF";

        /// <summary>描边粗细。</summary>
        public double StrokeThickness { get; set; } = 1;

        /// <summary>整体透明度（禁用节点半透明）。</summary>
        public double Opacity { get; set; } = 1.0;
    }

    /// <summary>车辆在地图上的位置标记视图项：携带坐标、状态与样式。</summary>
    public class VehicleMapViewItem
    {
        /// <summary>车辆编号。</summary>
        public string VehicleId { get; set; } = string.Empty;

        /// <summary>当前任务编号。</summary>
        public string CurrentTaskId { get; set; } = string.Empty;

        /// <summary>状态文本。</summary>
        public string State { get; set; } = string.Empty;

        /// <summary>当前位置（原始位置串）。</summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>车辆逻辑 X 坐标。</summary>
        public double X { get; set; }

        /// <summary>车辆逻辑 Y 坐标。</summary>
        public double Y { get; set; }

        /// <summary>标记绘制左上角 X（已偏移）。</summary>
        public double CanvasLeft { get; set; }

        /// <summary>标记绘制左上角 Y（已偏移）。</summary>
        public double CanvasTop { get; set; }

        /// <summary>电量百分比。</summary>
        public double BatteryLevel { get; set; }

        /// <summary>状态显示文本。</summary>
        public string StateText { get; set; } = string.Empty;

        /// <summary>填充颜色（选中为金色，否则按状态着色）。</summary>
        public string Fill { get; set; } = "#00BFFF";
    }
}
