using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class SignalPoint
    {
        public string SignalId { get; set; } = string.Empty;

        public string SignalCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public SignalPointType PointType { get; set; } = SignalPointType.Custom;

        public SignalDirection Direction { get; set; } = SignalDirection.Bidirectional;

        public SignalState State { get; set; } = SignalState.Normal;

        public string AreaCode { get; set; } = string.Empty;

        public string? NodeId { get; set; }

        public MapPosition Position { get; set; } = new();

        public string PlcAddress { get; set; } = string.Empty;

        public string CurrentValue { get; set; } = string.Empty;

        public string ExpectedValue { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public DateTime? LastChangedAt { get; set; }

        public DateTime? LastHeartbeatAt { get; set; }

        public string Remark { get; set; } = string.Empty;
    }
}
