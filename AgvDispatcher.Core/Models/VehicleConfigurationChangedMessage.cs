namespace AgvDispatcher.Core.Models
{
    public class VehicleConfigurationChangedMessage
    {
        public Vehicle Vehicle { get; set; } = new();
    }
}
