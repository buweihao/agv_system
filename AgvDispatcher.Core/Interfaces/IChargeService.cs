using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IChargeService
    {
        IReadOnlyList<ChargeStation> GetStations();

        ChargeStation? GetStation(string stationId);

        bool ShouldCharge(string vehicleId);

        ChargeRecommendation RecommendStation(string vehicleId);

        TaskOrder CreateChargeTask(string vehicleId, string stationId);
    }
}
