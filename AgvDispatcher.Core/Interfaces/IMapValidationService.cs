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
    }
}
