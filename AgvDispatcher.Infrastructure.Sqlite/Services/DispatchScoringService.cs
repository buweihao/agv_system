using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class DispatchScoringService : IDispatchScoringService
    {
        private readonly IMapRepository _mapRepository;
        private readonly ISystemParameterRepository _parameterRepository;
        private IReadOnlyList<MapNode>? _nodesCache;
        private IReadOnlyList<ParameterConfig>? _paramsCache;
        private IReadOnlyList<MapEdge>? _edgesCache;

        public DispatchScoringService(IMapRepository mapRepository, ISystemParameterRepository parameterRepository)
        {
            _mapRepository = mapRepository;
            _parameterRepository = parameterRepository;
        }

        public DispatchScoringResult ScoreAndSelectVehicle(TaskOrder task, IEnumerable<(Vehicle Vehicle, VehicleStatus Status)> availableVehicles)
        {
            if (_nodesCache == null)
            {
                _nodesCache = _mapRepository.GetNodesAsync().GetAwaiter().GetResult();
            }
            if (_paramsCache == null)
            {
                _paramsCache = _parameterRepository.GetAllAsync().GetAwaiter().GetResult();
            }
            if (_edgesCache == null)
            {
                _edgesCache = _mapRepository.GetEdgesAsync().GetAwaiter().GetResult();
            }

            var result = new DispatchScoringResult();
            
            foreach (var (vehicle, status) in availableVehicles)
            {
                // Hard Veto: Vehicle Disabled
                if (!vehicle.IsEnabled)
                {
                    result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = "Vehicle is disabled" });
                    continue;
                }

                // Hard Veto: Status
                if (status.State != RobotState.Idle)
                {
                    result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = "State is not Idle" });
                    continue;
                }
                if (!status.IsOnline)
                {
                    result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = "Vehicle is Offline" });
                    continue;
                }
                if (status.HasAlarm)
                {
                    result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = "Vehicle has Alarm" });
                    continue;
                }

                // Hard Veto: Must support AssignTask command
                if (vehicle.SupportedCommandFlags != VehicleCommandCapability.None &&
                    !vehicle.SupportedCommandFlags.HasFlag(VehicleCommandCapability.AssignTask))
                {
                    result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = "Vehicle does not support AssignTask command" });
                    continue;
                }

                // Hard Veto: Battery
                double minBattery = (task.MinBatteryRequired ?? 0) > 0 ? task.MinBatteryRequired.Value : GetParamValue("MIN_DISPATCH_BATTERY", 20.0);
                if (status.BatteryLevel < minBattery)
                {
                    result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = $"Battery {status.BatteryLevel:0.#}% < required {minBattery:0.#}%" });
                    continue;
                }

                // Hard Veto: Capability
                if (task.RequiredCapabilities != VehicleCapability.None)
                {
                    if ((vehicle.CapabilityFlags & task.RequiredCapabilities) != task.RequiredCapabilities)
                    {
                        result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = $"Missing required capability {task.RequiredCapabilities}" });
                        continue;
                    }
                }

                // Hard Veto: Load Capacity
                if (task.CargoWeight > 0 && vehicle.RatedLoad > 0 && vehicle.RatedLoad < task.CargoWeight)
                {
                    result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = $"RatedLoad {vehicle.RatedLoad} kg < required {task.CargoWeight} kg" });
                    continue;
                }

                // Hard Veto: Forbidden Brands
                if (!string.IsNullOrWhiteSpace(task.ForbiddenBrands))
                {
                    var forbidden = task.ForbiddenBrands.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (forbidden.Any(b => string.Equals(b.Trim(), vehicle.Brand, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = $"Brand '{vehicle.Brand}' is forbidden" });
                        continue;
                    }
                }

                // Hard Veto: Allowed Brands
                if (!string.IsNullOrWhiteSpace(task.AllowedBrands))
                {
                    var allowed = task.AllowedBrands.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!allowed.Any(b => string.Equals(b.Trim(), vehicle.Brand, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = $"Brand '{vehicle.Brand}' is not in allowed list" });
                        continue;
                    }
                }

                // Hard Veto: Map Source/Target Node Constraints
                bool mapConstrained = false;
                if (!string.IsNullOrWhiteSpace(task.SourceNodeId))
                {
                    var sourceNode = _nodesCache?.FirstOrDefault(n => n.NodeId == task.SourceNodeId);
                    if (sourceNode != null)
                    {
                        if (sourceNode.RequiredCapabilities != VehicleCapability.None && 
                            (vehicle.CapabilityFlags & sourceNode.RequiredCapabilities) != sourceNode.RequiredCapabilities)
                        {
                            result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = $"Missing capability {sourceNode.RequiredCapabilities} for Source Node" });
                            mapConstrained = true;
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(sourceNode.AllowedBrands))
                        {
                            var allowedBrands = sourceNode.AllowedBrands.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            if (!allowedBrands.Any(b => string.Equals(b.Trim(), vehicle.Brand, StringComparison.OrdinalIgnoreCase)))
                            {
                                result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = "Brand not allowed at Source Node" });
                                mapConstrained = true;
                                continue;
                            }
                        }
                    }
                }

                if (mapConstrained) continue;

                if (!string.IsNullOrWhiteSpace(task.TargetNodeId))
                {
                    var targetNode = _nodesCache?.FirstOrDefault(n => n.NodeId == task.TargetNodeId);
                    if (targetNode != null)
                    {
                        if (targetNode.RequiredCapabilities != VehicleCapability.None && 
                            (vehicle.CapabilityFlags & targetNode.RequiredCapabilities) != targetNode.RequiredCapabilities)
                        {
                            result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = $"Missing capability {targetNode.RequiredCapabilities} for Target Node" });
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(targetNode.AllowedBrands))
                        {
                            var allowedBrands = targetNode.AllowedBrands.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            if (!allowedBrands.Any(b => string.Equals(b.Trim(), vehicle.Brand, StringComparison.OrdinalIgnoreCase)))
                            {
                                result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = "Brand not allowed at Target Node" });
                                continue;
                            }
                        }
                    }
                }

                // Hard Veto: Edge Constraints (IsEnabled, IsLocked, AllowedBrands)
                if (_edgesCache != null && !string.IsNullOrWhiteSpace(task.SourceNodeId) && !string.IsNullOrWhiteSpace(task.TargetNodeId))
                {
                    var relevantEdges = _edgesCache.Where(e =>
                        (e.FromNodeId == task.SourceNodeId || e.ToNodeId == task.SourceNodeId ||
                         e.FromNodeId == task.TargetNodeId || e.ToNodeId == task.TargetNodeId));

                    bool edgeBlocked = false;
                    foreach (var edge in relevantEdges)
                    {
                        if (!edge.IsEnabled)
                        {
                            result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = $"Edge {edge.EdgeId} is disabled" });
                            edgeBlocked = true;
                            break;
                        }
                        if (edge.IsLocked)
                        {
                            result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = $"Edge {edge.EdgeId} is locked" });
                            edgeBlocked = true;
                            break;
                        }
                        if (!string.IsNullOrWhiteSpace(edge.AllowedBrands))
                        {
                            var edgeBrands = edge.AllowedBrands.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            if (!edgeBrands.Any(b => string.Equals(b.Trim(), vehicle.Brand, StringComparison.OrdinalIgnoreCase)))
                            {
                                result.Rejections.Add(new RejectionReason { VehicleId = vehicle.VehicleId, Reason = $"Brand '{vehicle.Brand}' not allowed on edge {edge.EdgeId}" });
                                edgeBlocked = true;
                                break;
                            }
                        }
                    }
                    if (edgeBlocked) continue;
                }

                // Soft Scoring
                var candidate = CalculateScore(task, vehicle, status);
                result.Candidates.Add(candidate);
            }

            if (!result.Candidates.Any())
            {
                result.Reason = result.Rejections.Any() 
                    ? "No vehicles passed constraints."
                    : "No idle/online vehicles available.";
                return result;
            }

            // Pick highest score
            var bestCandidate = result.Candidates.OrderByDescending(c => c.TotalScore).First();
            result.SelectedVehicleId = bestCandidate.VehicleId;
            result.Reason = $"Selected with score {bestCandidate.TotalScore:0.##}";

            return result;
        }

        private double GetParamValue(string key, double defaultValue)
        {
            var param = _paramsCache?.FirstOrDefault(p => p.ParamKey == key);
            if (param != null && double.TryParse(param.ParamValue, out var val))
            {
                return val;
            }
            return defaultValue;
        }

        private CandidateScore CalculateScore(TaskOrder task, Vehicle vehicle, VehicleStatus status)
        {
            var score = new CandidateScore { VehicleId = vehicle.VehicleId };

            // Fetch dynamic weights or use defaults
            double weightBattery = GetParamValue("SCORE_WEIGHT_BATTERY", 25.0);
            double weightDistance = GetParamValue("SCORE_WEIGHT_DISTANCE", 35.0);
            double weightLoad = GetParamValue("SCORE_WEIGHT_LOAD", 10.0);
            double weightPriority = GetParamValue("SCORE_WEIGHT_PRIORITY", 15.0);
            double weightArea = GetParamValue("SCORE_WEIGHT_AREA", 15.0);

            // 1. Battery Score
            double batteryPoints = Math.Min(weightBattery, status.BatteryLevel * (weightBattery / 100.0));
            score.Breakdown["Battery"] = batteryPoints;

            // 2. Distance Score
            double distancePoints = 0;
            if (!string.IsNullOrWhiteSpace(status.LocationText) && !string.IsNullOrWhiteSpace(task.SourceNodeId))
            {
                if (status.LocationText.Equals(task.SourceNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    distancePoints = weightDistance;
                }
                else
                {
                    var vehicleNode = _nodesCache?.FirstOrDefault(n => n.NodeId == status.LocationText);
                    var taskNode = _nodesCache?.FirstOrDefault(n => n.NodeId == task.SourceNodeId);

                    if (vehicleNode != null && taskNode != null)
                    {
                        double dx = vehicleNode.Position.X - taskNode.Position.X;
                        double dy = vehicleNode.Position.Y - taskNode.Position.Y;
                        double distance = Math.Sqrt(dx * dx + dy * dy);
                        // Assuming max map distance is ~2000 units.
                        double distScore = weightDistance - (distance / 2000.0 * weightDistance);
                        distancePoints = Math.Max(0, distScore);
                    }
                    else
                    {
                        distancePoints = weightDistance * 0.3; // Give small fixed points if unknown
                    }
                }
            }
            else
            {
                distancePoints = weightDistance * 0.3;
            }
            score.Breakdown["Distance"] = distancePoints;

            // 3. Area Match
            double areaPoints = 0;
            if (!string.IsNullOrWhiteSpace(vehicle.AreaCode) && !string.IsNullOrWhiteSpace(task.SourceNodeId))
            {
                var taskNode = _nodesCache?.FirstOrDefault(n => n.NodeId == task.SourceNodeId);
                if (taskNode != null && string.Equals(vehicle.AreaCode, taskNode.AreaCode, StringComparison.OrdinalIgnoreCase))
                {
                    areaPoints = weightArea;
                }
            }
            score.Breakdown["Area"] = areaPoints;

            // 4. Capability / Load Score
            double loadPoints = weightLoad; // Assume full score if it passed hard check, but could be adjusted if load size varies
            score.Breakdown["Load"] = loadPoints;

            // 5. Priority / Priority Rules
            double priorityPoints = weightPriority; 
            score.Breakdown["Priority"] = priorityPoints;

            score.TotalScore = score.Breakdown.Values.Sum();
            return score;
        }
    }
}
