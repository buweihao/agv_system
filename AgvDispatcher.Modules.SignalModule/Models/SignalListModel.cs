namespace AgvDispatcher.Modules.SignalModule.Models
{
    public class SignalListModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string State { get; set; }
        public string CurrentValue { get; set; }
        public string Device { get; set; }
        public string LastUpdatedTime { get; set; }
    }
}
