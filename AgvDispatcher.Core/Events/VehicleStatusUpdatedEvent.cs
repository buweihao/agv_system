using AgvDispatcher.Core.Models;
using Prism.Events;

namespace AgvDispatcher.Core.Events
{
    public class VehicleStatusUpdatedEvent : PubSubEvent<VehicleStatusSnapshot>
    {
    }
}
