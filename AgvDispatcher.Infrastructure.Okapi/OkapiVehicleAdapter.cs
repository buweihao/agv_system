using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Okapi.Models;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiVehicleAdapter : IVehicleAdapter
    {
        private readonly Vehicle _vehicle;
        private readonly OkapiClient _client;
        private readonly OkapiCallbackServer _server;
        private readonly OkapiPointMapper _pointMapper;
        private readonly OkapiVehicleIdentityMapper _identityMapper;
        private readonly OkapiTaskStateHandler _taskStateHandler;
        private readonly OkapiAreaControlHandler _areaControlHandler;
        private readonly OkapiProtocolLogger _logger;
        private readonly OkapiOptions _options;

        private CancellationTokenSource? _pollingCts;
        private Task? _pollingTask;

        public OkapiVehicleAdapter(
            Vehicle vehicle,
            OkapiClient client,
            OkapiCallbackServer server,
            OkapiPointMapper pointMapper,
            OkapiVehicleIdentityMapper identityMapper,
            OkapiTaskStateHandler taskStateHandler,
            OkapiAreaControlHandler areaControlHandler,
            OkapiProtocolLogger logger,
            OkapiOptions options)
        {
            _vehicle = vehicle;
            _client = client;
            _server = server;
            _pointMapper = pointMapper;
            _identityMapper = identityMapper;
            _taskStateHandler = taskStateHandler;
            _areaControlHandler = areaControlHandler;
            _logger = logger;
            _options = options;
        }

        public string VehicleId => _vehicle.VehicleId;
        public string Brand => _vehicle.Brand;

        public event EventHandler<VehicleStatusSnapshot>? StatusReceived;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _server.TaskStateReceived -= OnTaskStateReceived;
            _server.TaskStateReceived += OnTaskStateReceived;

            _server.AreaControlReceived -= OnAreaControlReceived;
            _server.AreaControlReceived += OnAreaControlReceived;

            await _server.StartAsync(cancellationToken);

            if (_options.EnableStatusPolling)
            {
                _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _pollingTask = PollingLoopAsync(_pollingCts.Token);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _server.TaskStateReceived -= OnTaskStateReceived;
            _server.AreaControlReceived -= OnAreaControlReceived;

            if (_pollingCts != null)
            {
                await _pollingCts.CancelAsync();
                try
                {
                    if (_pollingTask != null)
                    {
                        await _pollingTask.WaitAsync(cancellationToken);
                    }
                }
                catch (OperationCanceledException) { }
                finally
                {
                    _pollingCts.Dispose();
                    _pollingCts = null;
                    _pollingTask = null;
                }
            }
        }

        public async Task SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            var agvId = await _identityMapper.GetAgvIdAsync(VehicleId, cancellationToken);
            if (!agvId.HasValue)
            {
                throw new Exception($"Cannot map VehicleId {VehicleId} to Okapi agvId.");
            }

            switch (command.CommandType)
            {
                case DispatchCommandType.AssignTask:
                    await SendAssignTaskAsync(command, agvId.Value, cancellationToken);
                    break;
                case DispatchCommandType.CancelTask:
                    await SendCancelTaskAsync(command, agvId.Value, cancellationToken);
                    break;
                case DispatchCommandType.MoveToNode:
                    await SendMoveToNodeAsync(command, agvId.Value, cancellationToken);
                    break;
                case DispatchCommandType.RequestControl:
                case DispatchCommandType.LockTrafficArea:
                    await SendRequestControlAsync(command, agvId.Value, cancellationToken);
                    break;
                case DispatchCommandType.ReleaseTrafficArea:
                    if (!command.RequestControlType.HasValue)
                    {
                        throw new NotSupportedException("RequestControlType is required for ReleaseTrafficArea.");
                    }
                    await SendRequestControlAsync(command, agvId.Value, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"Command {command.CommandType} is not supported by Okapi HTTP adapter.");
            }
        }

        private async Task SendAssignTaskAsync(DispatchCommand command, int agvId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(command.SourceNodeId) || string.IsNullOrWhiteSpace(command.TargetNodeId))
            {
                throw new ArgumentException("SourceNodeId and TargetNodeId are required for AssignTask.");
            }

            var sourcePoint = await _pointMapper.NodeIdToOkapiPointAsync(command.SourceNodeId, Brand, token);
            var targetPoint = await _pointMapper.NodeIdToOkapiPointAsync(command.TargetNodeId, Brand, token);

            if (sourcePoint == null || targetPoint == null)
            {
                throw new Exception($"Failed to map SourceNodeId {command.SourceNodeId} or TargetNodeId {command.TargetNodeId} to Okapi points.");
            }

            var request = new TaskDownloadRequest
            {
                TaskId = command.TaskId ?? command.CommandId,
                Type = 1, // Default type
                Prio = command.Priority > 0 ? command.Priority : 1,
                Source = sourcePoint,
                Target = targetPoint,
                CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var result = await _client.TaskDownloadAsync(request, VehicleId, token);
            if (!result.Success)
            {
                throw new Exception($"Failed to send AssignTask to Okapi: {result.Message}");
            }
        }

        private async Task SendMoveToNodeAsync(DispatchCommand command, int agvId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(command.TargetNodeId))
            {
                throw new ArgumentException("TargetNodeId is required for MoveToNode.");
            }

            if (string.IsNullOrWhiteSpace(command.SourceNodeId))
            {
                throw new NotSupportedException("SourceNodeId is required for Okapi MoveToNode command.");
            }

            var sourcePoint = await _pointMapper.NodeIdToOkapiPointAsync(command.SourceNodeId, Brand, token);
            var targetPoint = await _pointMapper.NodeIdToOkapiPointAsync(command.TargetNodeId, Brand, token);

            if (sourcePoint == null || targetPoint == null)
            {
                throw new Exception($"Failed to map SourceNodeId {command.SourceNodeId} or TargetNodeId {command.TargetNodeId} to Okapi point.");
            }

            var request = new TaskDownloadRequest
            {
                TaskId = command.TaskId ?? command.CommandId,
                Type = 1,
                Prio = command.Priority > 0 ? command.Priority : 1,
                Source = sourcePoint,
                Target = targetPoint,
                CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var result = await _client.TaskDownloadAsync(request, VehicleId, token);
            if (!result.Success)
            {
                throw new Exception($"Failed to send MoveToNode to Okapi: {result.Message}");
            }
        }

        private async Task SendCancelTaskAsync(DispatchCommand command, int agvId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(command.TaskId))
            {
                throw new ArgumentException("TaskId is required for CancelTask.");
            }

            var request = new DeleteTaskRequest
            {
                TaskId = command.TaskId
            };

            var result = await _client.DeleteTaskAsync(request, VehicleId, token);
            if (!result.Success)
            {
                throw new Exception($"Failed to send CancelTask to Okapi: {result.Message}");
            }
        }

        private async Task SendRequestControlAsync(DispatchCommand command, int agvId, CancellationToken token)
        {
            var request = new RequestControlRequest
            {
                AreaId = command.AreaId ?? "Unknown",
                AgvId = agvId,
                RequestType = command.RequestControlType ?? 1,
                RequestTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var result = await _client.RequestControlAsync(request, VehicleId, token);
            if (!result.Success)
            {
                throw new Exception($"Failed to send RequestControl to Okapi: {result.Message}");
            }
        }

        public VehicleStatusSnapshot ConvertStatus(object rawStatus)
        {
            if (rawStatus is ReturnTaskStateRequest req)
            {
                var state = req.FaultCode != 0 ? RobotState.Fault : (req.State == 1 ? RobotState.Running : RobotState.Idle);
                return new VehicleStatusSnapshot
                {
                    VehicleId = VehicleId,
                    Brand = Brand,
                    State = state,
                    CurrentTaskId = req.TaskId,
                    ReportedAt = DateTime.Now
                };
            }
            throw new ArgumentException("Unsupported raw status type.", nameof(rawStatus));
        }

        private async void OnTaskStateReceived(object? sender, ReturnTaskStateRequest e)
        {
            try
            {
                var vId = await _identityMapper.GetVehicleIdAsync(e.AgvId);
                if (vId == VehicleId)
                {
                    var snapshot = await _taskStateHandler.HandleTaskStateAsync(e);
                    if (snapshot != null)
                    {
                        StatusReceived?.Invoke(this, snapshot);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(VehicleId, "OnTaskStateReceived", ex.Message);
            }
        }

        private async void OnAreaControlReceived(object? sender, RequestControlRequest e)
        {
            try
            {
                var vId = await _identityMapper.GetVehicleIdAsync(e.AgvId);
                if (vId == VehicleId)
                {
                    await _areaControlHandler.HandleAreaControlAsync(e);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(VehicleId, "OnAreaControlReceived", ex.Message);
            }
        }

        private async Task PollingLoopAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.StatusPollingIntervalSeconds));
            while (await timer.WaitForNextTickAsync(token))
            {
                try
                {
                    await _client.GetAgvInfosAsync(token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception) { /* Logged in client */ }
            }
        }
    }
}
