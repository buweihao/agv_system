using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    public class MapViewModel : BindableBase
    {
        private readonly IMapService _mapService;
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly ITaskService _taskService;

        private string? _selectedVehicleId;
        private string _pathSummary = "请选择AGV查看规划路径";

        public ObservableCollection<MapEdgeViewItem> Edges { get; } = new();

        public ObservableCollection<MapEdgeViewItem> PlannedEdges { get; } = new();

        public ObservableCollection<MapNodeViewItem> Nodes { get; } = new();

        public ObservableCollection<VehicleMapViewItem> Vehicles { get; } = new();

        public string PathSummary
        {
            get => _pathSummary;
            set => SetProperty(ref _pathSummary, value);
        }

        public MapViewModel(
            IEventAggregator eventAggregator,
            IMapService mapService,
            IVehicleStateStore vehicleStateStore,
            ITaskService taskService)
        {
            _mapService = mapService;
            _vehicleStateStore = vehicleStateStore;
            _taskService = taskService;

            LoadMap(null);
            eventAggregator.GetEvent<SelectedVehicleChangedEvent>().Subscribe(LoadMap, ThreadOption.UIThread);
            eventAggregator.GetEvent<VehicleStateChangedEvent>().Subscribe(_ => LoadMap(_selectedVehicleId), ThreadOption.UIThread);
        }

        private void LoadMap(string? selectedVehicleId)
        {
            _selectedVehicleId = selectedVehicleId;

            var nodes = _mapService.GetNodes();
            var edges = _mapService.GetEdges();
            var nodeMap = nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            var selectedVehicle = string.IsNullOrWhiteSpace(selectedVehicleId)
                ? null
                : _vehicleStateStore.GetVehicle(selectedVehicleId);
            var plannedPath = CreateSelectedVehiclePath(selectedVehicle, nodes);
            var plannedEdgeIds = plannedPath?.Edges
                .Select(edge => edge.EdgeId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Edges.Clear();
            foreach (var edge in edges)
            {
                if (nodeMap.TryGetValue(edge.FromNodeId, out var fromNode)
                    && nodeMap.TryGetValue(edge.ToNodeId, out var toNode))
                {
                    Edges.Add(CreateEdgeItem(edge, fromNode, toNode, plannedEdgeIds.Contains(edge.EdgeId)));
                }
            }

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

            Nodes.Clear();
            foreach (var node in nodes)
            {
                Nodes.Add(CreateNodeItem(node, plannedPath?.Nodes.Any(pathNode =>
                    string.Equals(pathNode.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase)) == true));
            }

            Vehicles.Clear();
            foreach (var vehicle in CreateVehiclePositions(nodes, selectedVehicleId))
            {
                Vehicles.Add(vehicle);
            }

            PathSummary = BuildPathSummary(selectedVehicle, plannedPath, nodes);
        }

        private PlannedPath? CreateSelectedVehiclePath(VehicleStatusSnapshot? vehicle, IReadOnlyList<MapNode> nodes)
        {
            if (vehicle is null)
            {
                return null;
            }

            var startNode = ResolveNode(vehicle.Location, nodes);
            var targetNodeId = ResolveTargetNodeId(vehicle);

            if (startNode is null || string.IsNullOrWhiteSpace(targetNodeId))
            {
                return null;
            }

            var targetNode = ResolveNode(targetNodeId, nodes);
            if (targetNode is null || string.Equals(startNode.NodeId, targetNode.NodeId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return _mapService.FindPlannedPath(startNode.NodeId, targetNode.NodeId);
        }

        private string BuildPathSummary(VehicleStatusSnapshot? vehicle, PlannedPath? plannedPath, IReadOnlyList<MapNode> nodes)
        {
            if (vehicle is null)
            {
                return "请选择AGV查看规划路径";
            }

            var startNode = ResolveNode(vehicle.Location, nodes);
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

        private static MapEdgeViewItem CreateEdgeItem(
            MapEdge edge,
            MapNode fromNode,
            MapNode toNode,
            bool isPlanned)
        {
            return new MapEdgeViewItem
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                X1 = fromNode.Position.X,
                Y1 = fromNode.Position.Y,
                X2 = toNode.Position.X,
                Y2 = toNode.Position.Y,
                Stroke = edge.IsEnabled && !edge.IsLocked ? "#00FF7F" : "#FF4500",
                StrokeThickness = isPlanned ? 3 : 2,
                Opacity = edge.IsEnabled && !edge.IsLocked ? 0.55 : 0.9
            };
        }

        private static MapNodeViewItem CreateNodeItem(MapNode node, bool isOnPlannedPath)
        {
            return new MapNodeViewItem
            {
                NodeId = node.NodeId,
                Name = node.Name,
                NodeCode = string.IsNullOrWhiteSpace(node.NodeCode) ? node.NodeId : node.NodeCode,
                NodeType = node.NodeType.ToString(),
                X = node.Position.X,
                Y = node.Position.Y,
                CanvasLeft = node.Position.X - 9,
                CanvasTop = node.Position.Y - 9,
                LabelLeft = node.Position.X + 12,
                LabelTop = node.Position.Y - 12,
                Fill = node.NodeType switch
                {
                    MapNodeType.Pickup => "#1E90FF",
                    MapNodeType.Dropoff => "#00BFFF",
                    MapNodeType.Charge => "#9370DB",
                    MapNodeType.Intersection => "#00FF7F",
                    _ => "#9FB7CC"
                },
                Stroke = isOnPlannedPath ? "#FFD700" : "#D8F3FF",
                StrokeThickness = isOnPlannedPath ? 3 : 1
            };
        }

        private IEnumerable<VehicleMapViewItem> CreateVehiclePositions(IReadOnlyList<MapNode> nodes, string? selectedVehicleId)
        {
            foreach (var snapshot in _vehicleStateStore.GetAllVehicles())
            {
                var node = ResolveNode(snapshot.Location, nodes);
                if (node is null)
                {
                    continue;
                }

                var isSelected = string.Equals(snapshot.VehicleId, selectedVehicleId, StringComparison.OrdinalIgnoreCase);
                yield return new VehicleMapViewItem
                {
                    VehicleId = snapshot.VehicleId,
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

        private static MapNode? ResolveNode(string? location, IReadOnlyList<MapNode> nodes)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
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

    public class MapEdgeViewItem
    {
        public string EdgeId { get; set; } = string.Empty;

        public string FromNodeId { get; set; } = string.Empty;

        public string ToNodeId { get; set; } = string.Empty;

        public double X1 { get; set; }

        public double Y1 { get; set; }

        public double X2 { get; set; }

        public double Y2 { get; set; }

        public string Stroke { get; set; } = "#00FF7F";

        public double StrokeThickness { get; set; } = 2;

        public double Opacity { get; set; } = 0.6;
    }

    public class MapNodeViewItem
    {
        public string NodeId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string NodeCode { get; set; } = string.Empty;

        public string NodeType { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double CanvasLeft { get; set; }

        public double CanvasTop { get; set; }

        public double LabelLeft { get; set; }

        public double LabelTop { get; set; }

        public string Fill { get; set; } = "#00BFFF";

        public string Stroke { get; set; } = "#D8F3FF";

        public double StrokeThickness { get; set; } = 1;
    }

    public class VehicleMapViewItem
    {
        public string VehicleId { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double CanvasLeft { get; set; }

        public double CanvasTop { get; set; }

        public double BatteryLevel { get; set; }

        public string StateText { get; set; } = string.Empty;

        public string Fill { get; set; } = "#00BFFF";
    }
}
