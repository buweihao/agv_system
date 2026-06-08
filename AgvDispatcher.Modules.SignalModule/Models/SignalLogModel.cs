using Prism.Mvvm;

namespace AgvDispatcher.Modules.SignalModule.Models
{
    public class SignalLogModel : BindableBase
    {
        public string LogTime { get; set; } = string.Empty;
        public string SignalName { get; set; } = string.Empty;
        public string SignalId { get; set; } = string.Empty;
        public string SignalType { get; set; } = string.Empty;
        public string StateChange { get; set; } = string.Empty;
        public string TriggerValue { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
        public string ResponseTime { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
    }
}
