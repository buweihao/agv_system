using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;
using AgvDispatcher.Core.Contracts.Dispatching.Events;
using AgvDispatcher.Core.Contracts.Dispatching.Interfaces;
using AgvDispatcher.Core.Contracts.Dispatching.Models;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Dispatching.Results;
using Xunit;

namespace AgvDispatcher.Tests.Dispatching;

public sealed class DispatchOrchestrationCoreContractTests
{
    [Fact]
    public void Dispatching_CoreContracts_ShouldExist()
    {
        var contractTypes = new[]
        {
            typeof(IDispatchOrchestrationService),
            typeof(StartDispatchTaskRequest),
            typeof(AdvanceDispatchRouteRequest),
            typeof(CancelDispatchTaskRequest),
            typeof(GetDispatchExecutionRequest),
            typeof(DispatchExecutionDto),
            typeof(StartDispatchTaskResultDto),
            typeof(AdvanceDispatchRouteResultDto),
            typeof(DispatchExecutionState),
            typeof(DispatchOrchestrationFailureCode),
            typeof(DispatchOrchestrationEventType),
            typeof(DispatchOrchestrationChangedEvent)
        };

        Assert.All(contractTypes, type => Assert.NotNull(type));
    }

    [Fact]
    public void Dispatching_Requests_ShouldImplementIAgvRequest()
    {
        var requestTypes = new[]
        {
            typeof(StartDispatchTaskRequest),
            typeof(AdvanceDispatchRouteRequest),
            typeof(CancelDispatchTaskRequest),
            typeof(GetDispatchExecutionRequest)
        };

        Assert.All(requestTypes, type => Assert.True(typeof(IAgvRequest).IsAssignableFrom(type)));
    }

    [Fact]
    public void Dispatching_DefaultValues_ShouldBeReasonable()
    {
        var start = new StartDispatchTaskRequest();
        var advance = new AdvanceDispatchRouteRequest();
        var cancel = new CancelDispatchTaskRequest();

        Assert.Equal(2, start.RollingWindowSize);
        Assert.Equal(1, start.MaxReplanCount);
        Assert.True(start.SendVehicleCommand);
        Assert.True(advance.AcquireNextWindow);
        Assert.True(cancel.ReleaseReservation);
        Assert.True(cancel.SendCancelCommand);
    }

    [Fact]
    public void DispatchExecutionState_ShouldContainExpectedStates()
    {
        var expected = new[]
        {
            DispatchExecutionState.Created,
            DispatchExecutionState.PlanningPath,
            DispatchExecutionState.ReservingRoute,
            DispatchExecutionState.LockingFirstWindow,
            DispatchExecutionState.Running,
            DispatchExecutionState.WaitingForTraffic,
            DispatchExecutionState.Replanning,
            DispatchExecutionState.Completed,
            DispatchExecutionState.Canceled,
            DispatchExecutionState.Failed
        };

        Assert.All(expected, state => Assert.True(Enum.IsDefined(state)));
    }
}
