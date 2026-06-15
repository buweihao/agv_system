using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiStatusSyncService
    {
        private readonly OkapiClient _client;
        private readonly OkapiDtoMapper _mapper;
        private readonly IVehicleStatusPublisher _statusPublisher;
        private readonly ITaskService _taskService;
        private readonly OkapiVehicleIdentityMapper _identityMapper;
        private readonly OkapiPointMapper _pointMapper;
        private readonly OkapiProtocolLogger _logger;
        private readonly SemaphoreSlim _syncSemaphore = new(1, 1);

        public OkapiStatusSyncService(
            OkapiClient client,
            OkapiDtoMapper mapper,
            IVehicleStatusPublisher statusPublisher,
            ITaskService taskService,
            OkapiVehicleIdentityMapper identityMapper,
            OkapiPointMapper pointMapper,
            OkapiProtocolLogger logger)
        {
            _client = client;
            _mapper = mapper;
            _statusPublisher = statusPublisher;
            _taskService = taskService;
            _identityMapper = identityMapper;
            _pointMapper = pointMapper;
            _logger = logger;
        }

        public async Task SyncAgvInfosAsync(CancellationToken token = default)
        {
            if (!await _syncSemaphore.WaitAsync(0, token)) return;
            try
            {
                var result = await _client.GetAgvInfosAsync(token);
                if (!result.Success)
                {
                    _logger.LogError("System", "SyncAgvInfos", result.Message, null);
                    return;
                }

                if (result.Data != null)
                {
                    foreach (var dto in result.Data)
                    {
                        var snapshot = await _mapper.ToVehicleStatusSnapshotAsync(dto, token);
                        if (snapshot != null)
                        {
                            _statusPublisher.PublishStatus(snapshot);
                        }

                        if (dto.State == 3 || !string.IsNullOrWhiteSpace(dto.ErrorMsg))
                        {
                            _logger.LogError(dto.AgvId.ToString(), "SyncAgvInfos", $"Okapi fault state: {dto.State}, Msg: {dto.ErrorMsg}", dto.TaskId);
                        }
                    }
                }
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }

        public async Task SyncTaskInfosAsync(CancellationToken token = default)
        {
            var result = await _client.GetTaskInfosAsync(token);
            if (!result.Success)
            {
                _logger.LogError("System", "SyncTaskInfos", result.Message, null);
                return;
            }

            if (result.Data != null)
            {
                foreach (var dto in result.Data)
                {
                    if (string.IsNullOrWhiteSpace(dto.TaskId))
                    {
                        _logger.LogError("System", "SyncTaskInfos", "Received task info with empty TaskId, skipping.", null);
                        continue;
                    }

                    var task = _taskService.GetTask(dto.TaskId);
                    if (task == null)
                    {
                        _logger.LogError("System", "SyncTaskInfos", $"Local task {dto.TaskId} not found, skipping sync.", dto.TaskId);
                        continue;
                    }

                    var vehicleId = await _identityMapper.GetVehicleIdAsync(dto.AgvId, token);
                    if (!string.IsNullOrEmpty(vehicleId) && string.IsNullOrEmpty(task.AssignedVehicleId))
                    {
                        _taskService.AssignVehicle(dto.TaskId, vehicleId);
                    }

                    var state = _mapper.ConvertOkapiTaskState(dto.State, 0);
                    var progress = _mapper.ConvertOkapiTaskProgress(dto.State);

                    _taskService.UpdateTaskState(dto.TaskId, state, $"Synced from Okapi GetTaskInfos state={dto.State}");
                    _taskService.UpdateTaskProgress(dto.TaskId, progress);

                    var mappedSource = await _pointMapper.OkapiPointToNodeIdAsync(dto.Source, "Okapi", token);
                    var mappedTarget = await _pointMapper.OkapiPointToNodeIdAsync(dto.Target, "Okapi", token);

                    if (mappedSource != task.SourceNodeId)
                    {
                        _logger.LogError(vehicleId ?? "System", "SyncTaskInfos", $"Local SourceNodeId {task.SourceNodeId} != Okapi Source {mappedSource} for Task {dto.TaskId}", dto.TaskId);
                    }
                    if (mappedTarget != task.TargetNodeId)
                    {
                        _logger.LogError(vehicleId ?? "System", "SyncTaskInfos", $"Local TargetNodeId {task.TargetNodeId} != Okapi Target {mappedTarget} for Task {dto.TaskId}", dto.TaskId);
                    }
                }
            }
        }
    }
}
