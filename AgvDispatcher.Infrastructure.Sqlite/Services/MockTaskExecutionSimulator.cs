using System.Collections.Concurrent;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class MockTaskExecutionSimulator : ITaskExecutionSimulator, IDisposable
    {
        private const int TicksPerSegment = 4;
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

        private readonly ITaskService _taskService;
        private readonly IMapService _mapService;
        private readonly IVehicleAdapterManager _vehicleAdapterManager;
        private readonly IAuditTrailService _auditTrail;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTasks = new(StringComparer.OrdinalIgnoreCase);

        public MockTaskExecutionSimulator(
            ITaskService taskService,
            IMapService mapService,
            IVehicleAdapterManager vehicleAdapterManager,
            IAuditTrailService auditTrail)
        {
            _taskService = taskService;
            _mapService = mapService;
            _vehicleAdapterManager = vehicleAdapterManager;
            _auditTrail = auditTrail;
        }

        public void Start(TaskOrder task, string vehicleId)
        {
            ArgumentNullException.ThrowIfNull(task);

            if (string.IsNullOrWhiteSpace(task.TaskId) || string.IsNullOrWhiteSpace(vehicleId))
            {
                return;
            }

            Cancel(task.TaskId);

            var cancellation = new CancellationTokenSource();
            if (!_runningTasks.TryAdd(task.TaskId, cancellation))
            {
                cancellation.Dispose();
                return;
            }

            _ = RunTaskAsync(task.TaskId, vehicleId.Trim(), cancellation.Token);
        }

        public void Cancel(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return;
            }

            if (_runningTasks.TryRemove(taskId, out var cancellation))
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var taskId in _runningTasks.Keys)
            {
                Cancel(taskId);
            }
        }

        private async System.Threading.Tasks.Task RunTaskAsync(string taskId, string vehicleId, CancellationToken cancellationToken)
        {
            try
            {
                var task = _taskService.GetTask(taskId);
                if (task is null)
                {
                    return;
                }

                _auditTrail.Record(new OperationLog
                {
                    Category = "Task",
                    Action = "SimulationStarted",
                    Message = $"Task {taskId} execution simulation started on {vehicleId}.",
                    TaskId = taskId,
                    VehicleId = vehicleId,
                    Operator = "MockTaskExecutionSimulator"
                });

                var route = ResolveRoute(task);
                _taskService.UpdateTaskProgress(taskId, 0, route.FirstOrDefault() ?? task.SourceNodeId);

                var segmentCount = Math.Max(route.Count - 1, 1);
                var totalTicks = segmentCount * TicksPerSegment;
                var completedTicks = 0;

                for (var segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                {
                    var fromNodeId = route[Math.Min(segmentIndex, route.Count - 1)];
                    var toNodeId = route[Math.Min(segmentIndex + 1, route.Count - 1)];

                    for (var tick = 1; tick <= TicksPerSegment; tick++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await System.Threading.Tasks.Task.Delay(TickInterval, cancellationToken);

                        completedTicks++;
                        var progress = Math.Min(95, (int)Math.Round((double)completedTicks / totalTicks * 95));
                        var currentNodeId = tick == TicksPerSegment ? toNodeId : fromNodeId;
                        _taskService.UpdateTaskProgress(taskId, progress, currentNodeId);

                        if (tick == TicksPerSegment)
                        {
                            await _vehicleAdapterManager.SendCommandAsync(new DispatchCommand
                            {
                                CommandType = DispatchCommandType.MoveToNode,
                                TaskId = taskId,
                                VehicleId = vehicleId,
                                TargetNodeId = toNodeId,
                                IssuedBy = nameof(MockTaskExecutionSimulator)
                            }, cancellationToken);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                await _vehicleAdapterManager.SendCommandAsync(new DispatchCommand
                {
                    CommandType = DispatchCommandType.CompleteTask,
                    TaskId = taskId,
                    VehicleId = vehicleId,
                    TargetNodeId = task.TargetNodeId,
                    IssuedBy = nameof(MockTaskExecutionSimulator)
                }, cancellationToken);

                _taskService.UpdateTaskProgress(taskId, 100, task.TargetNodeId);
                _taskService.UpdateTaskState(taskId, TaskState.Completed);

                _auditTrail.Record(new OperationLog
                {
                    Category = "Task",
                    Action = "SimulationCompleted",
                    Message = $"Task {taskId} completed by {vehicleId}.",
                    TaskId = taskId,
                    VehicleId = vehicleId,
                    Operator = "MockTaskExecutionSimulator"
                });
            }
            catch (OperationCanceledException)
            {
                _auditTrail.Record(new OperationLog
                {
                    Category = "Task",
                    Action = "SimulationCancelled",
                    Message = $"Task {taskId} execution simulation cancelled.",
                    TaskId = taskId,
                    VehicleId = vehicleId,
                    Operator = "MockTaskExecutionSimulator"
                });
            }
            finally
            {
                if (_runningTasks.TryRemove(taskId, out var cancellation))
                {
                    cancellation.Dispose();
                }
            }
        }

        private List<string> ResolveRoute(TaskOrder task)
        {
            var plannedPath = _mapService.FindPlannedPath(task.SourceNodeId, task.TargetNodeId);
            var route = plannedPath is { IsAvailable: true }
                ? plannedPath.Nodes.Select(node => node.NodeId).Where(nodeId => !string.IsNullOrWhiteSpace(nodeId)).ToList()
                : new List<string>();

            if (route.Count == 0 || !string.Equals(route[0], task.SourceNodeId, StringComparison.OrdinalIgnoreCase))
            {
                route.Insert(0, task.SourceNodeId);
            }

            if (!route.Any(nodeId => string.Equals(nodeId, task.TargetNodeId, StringComparison.OrdinalIgnoreCase)))
            {
                route.Add(task.TargetNodeId);
            }

            return route;
        }
    }
}
