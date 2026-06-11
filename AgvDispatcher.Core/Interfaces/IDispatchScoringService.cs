using AgvDispatcher.Core.Models;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IDispatchScoringService
    {
        /// <summary>
        /// Selects the best vehicle for a task based on hard constraints and soft scoring.
        /// </summary>
        /// <param name="task">The task to assign.</param>
        /// <param name="availableVehicles">The list of available vehicles with their dynamic status.</param>
        /// <returns>The ID of the best vehicle, or null if none are eligible.</returns>
        DispatchScoringResult ScoreAndSelectVehicle(TaskOrder task, IEnumerable<(Vehicle Vehicle, VehicleStatus Status)> availableVehicles);
    }

    public class DispatchScoringResult
    {
        public string? SelectedVehicleId { get; set; }
        public bool Success => !string.IsNullOrEmpty(SelectedVehicleId);
        public string Reason { get; set; } = string.Empty;

        // Stage F: Extended scoring structure
        public List<CandidateScore> Candidates { get; set; } = new();
        public List<RejectionReason> Rejections { get; set; } = new();
    }

    public class CandidateScore
    {
        public string VehicleId { get; set; } = string.Empty;
        public double TotalScore { get; set; }
        public Dictionary<string, double> Breakdown { get; set; } = new();
    }

    public class RejectionReason
    {
        public string VehicleId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
