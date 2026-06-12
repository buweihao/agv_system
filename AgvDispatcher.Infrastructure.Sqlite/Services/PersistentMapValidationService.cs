using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentMapValidationService : IMapValidationService
    {
        private readonly AgvDispatcherDbContext _db;

        public PersistentMapValidationService(AgvDispatcherDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<MapValidationResult>> ValidateMapAsync()
        {
            var results = new List<MapValidationResult>();

            var nodes = await _db.MapNodes.AsNoTracking().ToListAsync();
            var edges = await _db.MapEdges.AsNoTracking().ToListAsync();
            var vehicles = await _db.Vehicles.AsNoTracking().ToListAsync();
            var stations = await _db.ChargeStations.AsNoTracking().ToListAsync();
            var tasks = await _db.TaskOrders.AsNoTracking().ToListAsync();

            var nodeDict = nodes.ToDictionary(n => n.NodeId);

            // 1. 校验 Edge
            foreach (var edge in edges)
            {
                if (!nodeDict.ContainsKey(edge.FromNodeId))
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Error, ObjectType = "Edge", ObjectId = edge.EdgeId, Message = $"FromNodeId '{edge.FromNodeId}' 不存在" });
                }
                if (!nodeDict.ContainsKey(edge.ToNodeId))
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Error, ObjectType = "Edge", ObjectId = edge.EdgeId, Message = $"ToNodeId '{edge.ToNodeId}' 不存在" });
                }
            }

            // 2. 校验 ChargeStation
            foreach (var station in stations)
            {
                if (!nodeDict.TryGetValue(station.NodeId, out var node))
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Error, ObjectType = "ChargeStation", ObjectId = station.StationId, Message = $"NodeId '{station.NodeId}' 不存在" });
                }
                else if (node.NodeType != Core.Enums.MapNodeType.Charge)
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Warning, ObjectType = "ChargeStation", ObjectId = station.StationId, Message = $"节点 '{station.NodeId}' 的类型不是 Charge" });
                }
            }

            // 3. 校验 Vehicle
            foreach (var vehicle in vehicles)
            {
                if (!string.IsNullOrWhiteSpace(vehicle.HomeNodeId) && !nodeDict.ContainsKey(vehicle.HomeNodeId))
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Error, ObjectType = "Vehicle", ObjectId = vehicle.VehicleId, Message = $"HomeNodeId '{vehicle.HomeNodeId}' 不存在" });
                }
                if (!string.IsNullOrWhiteSpace(vehicle.ChargeNodeId) && !nodeDict.ContainsKey(vehicle.ChargeNodeId))
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Error, ObjectType = "Vehicle", ObjectId = vehicle.VehicleId, Message = $"ChargeNodeId '{vehicle.ChargeNodeId}' 不存在" });
                }
            }

            // 4. 校验 TaskOrder
            foreach (var task in tasks)
            {
                if (!string.IsNullOrWhiteSpace(task.SourceNodeId) && !nodeDict.ContainsKey(task.SourceNodeId))
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Error, ObjectType = "TaskOrder", ObjectId = task.TaskId, Message = $"SourceNodeId '{task.SourceNodeId}' 不存在" });
                }
                if (!string.IsNullOrWhiteSpace(task.TargetNodeId) && !nodeDict.ContainsKey(task.TargetNodeId))
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Error, ObjectType = "TaskOrder", ObjectId = task.TaskId, Message = $"TargetNodeId '{task.TargetNodeId}' 不存在" });
                }
            }

            // 5. 校验禁用节点的引用
            foreach (var node in nodes.Where(n => !n.IsEnabled))
            {
                if (edges.Any(e => e.FromNodeId == node.NodeId || e.ToNodeId == node.NodeId))
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Warning, ObjectType = "MapNode", ObjectId = node.NodeId, Message = $"节点被禁用，但仍有路径与之相连" });
                }
                if (vehicles.Any(v => v.HomeNodeId == node.NodeId || v.ChargeNodeId == node.NodeId))
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Warning, ObjectType = "MapNode", ObjectId = node.NodeId, Message = $"节点被禁用，但被车辆设为待机点或充电点" });
                }
            }

            // 6. 校验孤立节点
            foreach (var node in nodes)
            {
                if (!edges.Any(e => e.FromNodeId == node.NodeId || e.ToNodeId == node.NodeId))
                {
                    results.Add(new MapValidationResult { Level = MapValidationLevel.Info, ObjectType = "MapNode", ObjectId = node.NodeId, Message = $"孤立节点，没有任何路径相连" });
                }
            }

            return results;
        }
    }
}
