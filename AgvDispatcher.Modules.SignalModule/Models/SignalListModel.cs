namespace AgvDispatcher.Modules.SignalModule.Models
{
    public class SignalListModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string CurrentValue { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
        public string LastUpdatedTime { get; set; } = string.Empty;
    }
}
