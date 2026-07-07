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
            typeof(CompleteDispatchTaskRequest),
            typeof(CancelDispatchTaskRequest),
            typeof(GetDispatchExecutionRequest),
            typeof(RetryWaitingDispatchRequest),
            typeof(ReplanDispatchTaskRequest),
            typeof(DispatchExecutionDto),
            typeof(StartDispatchTaskResultDto),
            typeof(AdvanceDispatchRouteResultDto),
            typeof(RetryWaitingDispatchResultDto),
            typeof(ReplanDispatchTaskResultDto),
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
            typeof(GetDispatchExecutionRequest),
            typeof(RetryWaitingDispatchRequest),
            typeof(ReplanDispatchTaskRequest)
        };

        Assert.All(requestTypes, type => Assert.True(typeof(IAgvRequest).IsAssignableFrom(type)));
    }

    [Fact]
    public void Dispatching_DefaultValues_ShouldBeReasonable()
    {
        var start = new StartDispatchTaskRequest();
        var advance = new AdvanceDispatchRouteRequest();
        var cancel = new CancelDispatchTaskRequest();
        var replan = new ReplanDispatchTaskRequest();

        Assert.Equal(2, start.RollingWindowSize);
        Assert.Equal(1, start.MaxReplanCount);
        Assert.True(start.SendVehicleCommand);
        Assert.True(advance.AcquireNextWindow);
        Assert.True(cancel.ReleaseReservation);
        Assert.True(cancel.SendCancelCommand);
        Assert.True(new RetryWaitingDispatchRequest().SendVehicleCommand);
        Assert.True(replan.AvoidOccupiedResources);
        Assert.True(replan.ReleaseOldReservation);
        Assert.True(replan.AcquireFirstWindow);
        Assert.Empty(replan.ForbiddenEdgeIds);
        Assert.Empty(replan.ForbiddenNodeIds);
    }

    [Fact]
    public async Task ReplanTask_CanBeImplementedByTestFake()
    {
        IDispatchOrchestrationService service = new FakeReplanDispatchOrchestrationService();

        var result = await service.ReplanTaskAsync(new ReplanDispatchTaskRequest
        {
            TaskId = "TASK-A",
            VehicleId = "AGV-A",
            CurrentNodeId = "N2",
            CurrentSegmentSequence = 1,
            Reason = "test"
        });

        Assert.True(result.Success, result.Message);
        Assert.Equal("TASK-A", result.Data!.TaskId);
        Assert.Equal("AGV-A", result.Data.VehicleId);
        Assert.Equal("old-plan", result.Data.OldPlanId);
        Assert.Equal("new-plan", result.Data.NewPlanId);
        Assert.True(result.Data.RouteChanged);
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

    private sealed class FakeReplanDispatchOrchestrationService : IDispatchOrchestrationService
    {
        public Task<AgvResult<StartDispatchTaskResultDto>> StartTaskAsync(StartDispatchTaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult<RetryWaitingDispatchResultDto>> RetryWaitingTaskAsync(RetryWaitingDispatchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult<AdvanceDispatchRouteResultDto>> AdvanceRouteAsync(AdvanceDispatchRouteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult> CompleteTaskAsync(CompleteDispatchTaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult> CancelTaskAsync(CancelDispatchTaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult<DispatchExecutionDto>> GetExecutionAsync(GetDispatchExecutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult<IReadOnlyList<DispatchExecutionDto>>> GetActiveExecutionsAsync(GetDispatchExecutionsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AgvResult<ReplanDispatchTaskResultDto>> ReplanTaskAsync(
            ReplanDispatchTaskRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AgvResult<ReplanDispatchTaskResultDto>.Ok(new ReplanDispatchTaskResultDto
            {
                TaskId = request.TaskId,
                VehicleId = request.VehicleId,
                OldPlanId = "old-plan",
                NewPlanId = "new-plan",
                OldReservationId = "old-reservation",
                NewReservationId = "new-reservation",
                RouteChanged = true,
                FirstWindowLocked = true,
                Message = request.Reason
            }));
        }
    }
}
