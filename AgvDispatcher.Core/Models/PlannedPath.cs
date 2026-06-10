namespace AgvDispatcher.Core.Models
{
    public class PlannedPath
    {
        public string StartNodeId { get; set; } = string.Empty;

        public string EndNodeId { get; set; } = string.Empty;

        public IReadOnlyList<MapNode> Nodes { get; set; } = Array.Empty<MapNode>();

        public IReadOnlyList<MapEdge> Edges { get; set; } = Array.Empty<MapEdge>();

        public double TotalLength { get; set; }

        public bool IsAvailable { get; set; }

        public string Message { get; set; } = string.Empty;

        public static PlannedPath Unavailable(string startNodeId, string endNodeId, string message)
        {
            return new PlannedPath
            {
                StartNodeId = startNodeId,
                EndNodeId = endNodeId,
                Message = message,
                IsAvailable = false
            };
        }
    }
}
