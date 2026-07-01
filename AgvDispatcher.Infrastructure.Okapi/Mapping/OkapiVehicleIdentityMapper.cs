using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiVehicleIdentityMapper
    {
        private readonly IVehicleRepository _vehicleRepository;
        private readonly OkapiProtocolLogger _logger;
        private readonly Dictionary<string, int> _vehicleToAgvId = new();
        private readonly Dictionary<int, string> _agvIdToVehicle = new();
        private readonly SemaphoreSlim _sync = new(1, 1);

        public OkapiVehicleIdentityMapper(IVehicleRepository vehicleRepository, OkapiProtocolLogger logger)
        {
            _vehicleRepository = vehicleRepository;
            _logger = logger;
        }

        public async Task<int?> GetAgvIdAsync(string vehicleId, CancellationToken token = default)
        {
            await _sync.WaitAsync(token);
            try
            {
                if (_vehicleToAgvId.TryGetValue(vehicleId, out var cached))
                {
                    return cached;
                }

                var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
                if (vehicle == null)
                {
                    _logger.LogError(vehicleId, "MapIdentity", "Vehicle not found in repository");
                    return null;
                }

                var agvId = ParseAgvId(vehicle);
                if (agvId.HasValue)
                {
                    _vehicleToAgvId[vehicleId] = agvId.Value;
                    _agvIdToVehicle[agvId.Value] = vehicleId;
                }

                return agvId;
            }
            finally
            {
                _sync.Release();
            }
        }

        public async Task<string?> GetVehicleIdAsync(int agvId, CancellationToken token = default)
        {
            await _sync.WaitAsync(token);
            try
            {
                if (_agvIdToVehicle.TryGetValue(agvId, out var cached))
                {
                    return cached;
                }

                var vehicles = await _vehicleRepository.GetAllAsync();
                foreach (var v in vehicles)
                {
                    if (ParseAgvId(v) == agvId)
                    {
                        _vehicleToAgvId[v.VehicleId] = agvId;
                        _agvIdToVehicle[agvId] = v.VehicleId;
                        return v.VehicleId;
                    }
                }

                _logger.LogError($"AgvId:{agvId}", "MapIdentity", "Cannot find matching VehicleId for agvId");
                return null;
            }
            finally
            {
                _sync.Release();
            }
        }

        private int? ParseAgvId(Vehicle vehicle)
        {
            // 1. Try SerialNumber
            if (int.TryParse(vehicle.SerialNumber, out int result1))
            {
                return result1;
            }

            // 2. Try parsing digits from VehicleCode or VehicleId
            var code = !string.IsNullOrWhiteSpace(vehicle.VehicleCode) ? vehicle.VehicleCode : vehicle.VehicleId;
            var digits = new string(code.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int result2))
            {
                return result2;
            }

            // 3. Try Remark
            if (!string.IsNullOrWhiteSpace(vehicle.Remark))
            {
                var lines = vehicle.Remark.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("OkapiAgvId=", StringComparison.OrdinalIgnoreCase))
                    {
                        var valStr = line.Substring("OkapiAgvId=".Length).Trim();
                        if (int.TryParse(valStr, out int result3))
                        {
                            return result3;
                        }
                    }
                }
            }

            return null;
        }
    }
}
