using AgvDispatcher.Core.Models;
using Prism.Events;

namespace AgvDispatcher.Core.Events
{
    public class VehicleStateChangedEvent : PubSubEvent<VehicleStateChangedMessage>
    {
    }
}
