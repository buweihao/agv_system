using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class DispatchScoringService : IDispatchScoringService
    {
        public DispatchScoringResult ScoreAndSelectVehicle(TaskOrder task, IEnumerable<(Vehicle Vehicle, VehicleStatus Status)> availableVehicles)
        {
            var result = new DispatchScoringResult();
            var candidates = new List<(Vehicle Vehicle, VehicleStatus Status, double Score)>();
            
            var rejectionReasons = new List<string>();

            foreach (var (vehicle, status) in availableVehicles)
            {
                // Hard Veto: Status
                if (status.State != RobotState.Idle)
                {
                    continue; // Skip silently or log debug
                }
                if (!status.IsOnline)
                {
                    continue;
                }
                if (status.HasAlarm)
                {
                    continue;
                }

                // Hard Veto: Battery
                double minBattery = (task.MinBatteryRequired ?? 0) > 0 ? task.MinBatteryRequired.Value : 30.0;
                if (status.BatteryLevel < minBattery)
                {
                    rejectionReasons.Add($"[{vehicle.VehicleId}] Battery {status.BatteryLevel:0.#}% < required {minBattery:0.#}%");
                    continue;
                }

                // Hard Veto: Capability
                if (task.RequiredCapabilities != VehicleCapability.None)
                {
                    if ((vehicle.CapabilityFlags & task.RequiredCapabilities) != task.RequiredCapabilities)
                    {
                        rejectionReasons.Add($"[{vehicle.VehicleId}] Missing required capability {task.RequiredCapabilities}");
                        continue;
                    }
                }

                // Hard Veto: Forbidden Brands
                if (!string.IsNullOrWhiteSpace(task.ForbiddenBrands))
                {
                    var forbidden = task.ForbiddenBrands.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (forbidden.Any(b => string.Equals(b.Trim(), vehicle.Brand, StringComparison.OrdinalIgnoreCase)))
                    {
                        rejectionReasons.Add($"[{vehicle.VehicleId}] Brand '{vehicle.Brand}' is forbidden for this task");
                        continue;
                    }
                }

                // Hard Veto: Allowed Brands
                if (!string.IsNullOrWhiteSpace(task.AllowedBrands))
                {
                    var allowed = task.AllowedBrands.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!allowed.Any(b => string.Equals(b.Trim(), vehicle.Brand, StringComparison.OrdinalIgnoreCase)))
                    {
                        rejectionReasons.Add($"[{vehicle.VehicleId}] Brand '{vehicle.Brand}' is not in allowed list");
                        continue;
                    }
                }

                // Soft Scoring
                double score = CalculateScore(task, vehicle, status);
                candidates.Add((vehicle, status, score));
            }

            if (!candidates.Any())
            {
                result.Reason = rejectionReasons.Any() 
                    ? "No vehicles passed constraints: " + string.Join("; ", rejectionReasons.Take(3))
                    : "No idle/online vehicles available.";
                return result;
            }

            // Pick highest score
            var bestCandidate = candidates.OrderByDescending(c => c.Score).First();
            result.SelectedVehicleId = bestCandidate.Vehicle.VehicleId;
            result.Reason = $"Selected with score {bestCandidate.Score:0.##}";

            return result;
        }

        private double CalculateScore(TaskOrder task, Vehicle vehicle, VehicleStatus status)
        {
            double score = 0;

            // 1. Battery (Max 25 points)
            // Higher battery gives more points. Normalize 0-100% to 0-25 points.
            score += Math.Min(25, status.BatteryLevel * 0.25);

            // 2. Distance to Source Node (Max 35 points)
            // Assuming we don't have real map routing distance here, we can mock it or use a heuristic.
            // If location matches task source exactly -> 35 points.
            if (!string.IsNullOrWhiteSpace(status.LocationText) && status.LocationText.Equals(task.SourceNodeId, StringComparison.OrdinalIgnoreCase))
            {
                score += 35;
            }
            else
            {
                // Just a fallback heuristic: 10 points for being idle somewhere else
                score += 10;
            }

            // 3. Area Match (Max 15 points)
            // No AreaCode on task currently, skip area points

            // 4. Capability / Load (Max 10 points)
            // If vehicle has the exact capabilities or more, give points.
            score += 10;

            // 5. Priority / Custom rules (Max 15 points)
            // TBD: priority based scoring
            score += 15;

            return score;
        }
    }
}
