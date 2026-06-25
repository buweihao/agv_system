namespace AgvDispatcher.Infrastructure.Okapi
{
    public sealed class OkapiProtocolTraceRecord
    {
        public DateTimeOffset Time { get; init; } = DateTimeOffset.Now;

        public string Direction { get; init; } = string.Empty;

        public string VehicleId { get; init; } = string.Empty;

        public string CommandType { get; init; } = string.Empty;

        public string? TaskId { get; init; }

        public string? Url { get; init; }

        public string? RequestBody { get; init; }

        public string? ResponseBody { get; init; }

        public string? ErrorMessage { get; init; }
    }
}
