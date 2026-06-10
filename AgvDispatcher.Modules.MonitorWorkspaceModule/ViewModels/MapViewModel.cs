using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    public class MapViewModel : BindableBase
    {
        private const string DefaultStartNodeId = "A1";
        private const string DefaultEndNodeId = "Charge-1";

        private readonly IMapService _mapService;
        private readonly IVehicleStateStore _vehicleStateStore;

        private string _pathSummary = string.Empty;

        public ObservableCollection<MapEdgeViewItem> Edges { get; } = new();

        public ObservableCollection<MapEdgeViewItem> PlannedEdges { get; } = new();

        public ObservableCollection<MapNodeViewItem> Nodes { get; } = new();

        public ObservableCollection<VehicleMapViewItem> Vehicles { get; } = new();

        public string PathSummary
        {
            get => _pathSummary;
            set => SetProperty(ref _pathSummary, value);
        }

        public MapViewModel(IMapService mapService, IVehicleStateStore vehicleStateStore)
        {
            _mapService = mapService;
            _vehicleStateStore = vehicleStateStore;

            LoadMap();
        }

        private void LoadMap()
        {
            var nodes = _mapService.GetNodes();
            var edges = _mapService.GetEdges();
            var nodeMap = nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            var plannedPath = _mapService.FindPlannedPath(DefaultStartNodeId, DefaultEndNodeId);
            var plannedEdgeIds = plannedPath.Edges
                .Select(edge => edge.EdgeId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

            Nodes.Clear();
            foreach (var node in nodes)
            {
                Nodes.Add(CreateNodeItem(node, plannedPath.Nodes.Any(pathNode =>
                    string.Equals(pathNode.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase))));
            }

            Vehicles.Clear();
            foreach (var vehicle in CreateVehiclePositions(nodes))
            {
                Vehicles.Add(vehicle);
            }

            PathSummary = plannedPath.IsAvailable
                ? $"示例路径 {DefaultStartNodeId} -> {DefaultEndNodeId}，共 {plannedPath.Nodes.Count} 个点，{plannedPath.TotalLength:F0} m"
                : plannedPath.Message;
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

        private IEnumerable<VehicleMapViewItem> CreateVehiclePositions(IReadOnlyList<MapNode> nodes)
        {
            var nodeMap = nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            var fallbackNodes = nodes.ToArray();
            var fallbackIndex = 0;

            foreach (var snapshot in _vehicleStateStore.GetAllVehicles())
            {
                if (!nodeMap.TryGetValue(snapshot.Location, out var node))
                {
                    node = fallbackNodes.Length == 0
                        ? null
                        : fallbackNodes[fallbackIndex++ % fallbackNodes.Length];
                }

                if (node is null)
                {
                    continue;
                }

                yield return new VehicleMapViewItem
                {
                    VehicleId = snapshot.VehicleId,
                    X = node.Position.X,
                    Y = node.Position.Y,
                    CanvasLeft = node.Position.X - 15,
                    CanvasTop = node.Position.Y - 28,
                    BatteryLevel = snapshot.BatteryLevel,
                    StateText = snapshot.State.ToString(),
                    Fill = snapshot.State switch
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
