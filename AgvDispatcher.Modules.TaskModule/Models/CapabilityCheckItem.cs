using AgvDispatcher.Core.Enums;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskModule.Models
{
    public class CapabilityCheckItem : BindableBase
    {
        public string Name { get; set; } = string.Empty;
        public VehicleCapability Value { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }
    }
}
