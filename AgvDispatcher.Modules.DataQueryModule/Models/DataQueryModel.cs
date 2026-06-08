using Prism.Mvvm;

namespace AgvDispatcher.Modules.DataQueryModule.Models
{
    public class DataQueryModel : BindableBase
    {
        public int Seq { get; set; }
        public string QueryTime { get; set; } = string.Empty;
        public string AgvId { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string StartPoint { get; set; } = string.Empty;
        public string EndPoint { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Distance { get; set; } = string.Empty;
        public string AvgSpeed { get; set; } = string.Empty;
    }
}
