namespace AgvDispatcher.Core.Interfaces
{
    public enum MapValidationLevel
    {
        Info,
        Warning,
        Error
    }

    public class MapValidationResult
    {
        public MapValidationLevel Level { get; set; }
        public string ObjectType { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public interface IMapValidationService
    {
        Task<IReadOnlyList<MapValidationResult>> ValidateMapAsync();
        Task<IReadOnlyList<MapValidationResult>> ValidateMapDataAsync(IEnumerable<AgvDispatcher.Core.Models.MapNode> nodes, IEnumerable<AgvDispatcher.Core.Models.MapEdge> edges, IEnumerable<AgvDispatcher.Core.Models.ChargeStation> chargeStations, IEnumerable<AgvDispatcher.Core.Models.MapLocationAlias> aliases);
    }
}
