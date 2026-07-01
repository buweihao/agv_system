using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Rules;
using AgvDispatcher.Modules.MonitorWorkspaceModule.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    /// <summary>
    /// 运行监控页面"AGV 列表"的 ViewModel。
    /// <para>
    /// 负责展示全部 AGV 行数据（<see cref="RobotModel"/>），维护当前选中车辆并通过
    /// <see cref="SelectedVehicleChangedEvent"/> 广播给地图等其它分视图；
    /// 同时提供一个手动上报车辆状态的调试入口（<see cref="PublishStatusCommand"/>）。
    /// 通过订阅 <see cref="VehicleStateChangedEvent"/> 对列表做增量增改删，保持与状态存储一致。
    /// </para>
    /// <para>部分展示字段（目标位置、速度、运行时长）当前为按车辆 ID 预设的演示数据。</para>
    /// </summary>
    public class RobotListViewModel : BindableBase
    {
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly IVehicleStatusPublisher _vehicleStatusPublisher;

        private ObservableCollection<RobotModel> _robotList = new();
        // 手动上报表单的默认值
        private string _vehicleId = "AGV-002";
        private string _brand = "RGV-A";
        private double _batteryLevel = 76;
        private string _location = "PICK-A1";
        private RobotState _state = RobotState.Running;
        private string _lastPublishMessage = "Ready";
        private RobotModel? _selectedRobot;
        private readonly IEventAggregator _eventAggregator;

        /// <summary>AGV 列表数据源。</summary>
        public ObservableCollection<RobotModel> RobotList
        {
            get => _robotList;
            set => SetProperty(ref _robotList, value);
        }

        /// <summary>手动上报表单：车辆 ID 候选项。</summary>
        public ObservableCollection<string> VehicleIdOptions { get; } = new()
        {
            "AGV-002",
            "AGV-003",
            "AGV-008",
            "AGV-010",
            "AGV-017"
        };

        /// <summary>手动上报表单：品牌候选项。</summary>
        public ObservableCollection<string> BrandOptions { get; } = new()
        {
            "RGV-A",
            "RGV-B",
            "RGV-C"
        };

        /// <summary>手动上报表单：状态候选项（全部 <see cref="RobotState"/>）。</summary>
        public IEnumerable<RobotState> StateOptions { get; } = Enum.GetValues<RobotState>();

        /// <summary>手动上报表单：车辆 ID。</summary>
        public string VehicleId
        {
            get => _vehicleId;
            set => SetProperty(ref _vehicleId, value);
        }

        /// <summary>手动上报表单：品牌。</summary>
        public string Brand
        {
            get => _brand;
            set => SetProperty(ref _brand, value);
        }

        /// <summary>手动上报表单：电量（0~100，自动裁剪）。</summary>
        public double BatteryLevel
        {
            get => _batteryLevel;
            set => SetProperty(ref _batteryLevel, Math.Clamp(value, 0, 100));
        }

        /// <summary>手动上报表单：位置。</summary>
        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        /// <summary>手动上报表单：状态。</summary>
        public RobotState State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        /// <summary>最近一次上报的结果提示文本。</summary>
        public string LastPublishMessage
        {
            get => _lastPublishMessage;
            set => SetProperty(ref _lastPublishMessage, value);
        }

        /// <summary>
        /// 当前选中的 AGV。变更时通过 <see cref="SelectedVehicleChangedEvent"/> 广播车辆 ID，
        /// 驱动地图视图高亮该车及其规划路径。
        /// </summary>
        public RobotModel? SelectedRobot
        {
            get => _selectedRobot;
            set
            {
                if (SetProperty(ref _selectedRobot, value))
                {
                    _eventAggregator.GetEvent<SelectedVehicleChangedEvent>().Publish(value?.Id);
                }
            }
        }

        /// <summary>手动上报车辆状态的命令（调试/演示用）。</summary>
        public DelegateCommand PublishStatusCommand { get; }

        /// <summary>
        /// 构造函数：注入依赖，初始化列表与候选项，选中首台车，并订阅车辆状态变化事件。
        /// </summary>
        /// <param name="eventAggregator">事件聚合器。</param>
        /// <param name="vehicleStateStore">车辆状态存储，提供全量车辆。</param>
        /// <param name="vehicleStatusPublisher">车辆状态发布服务，供手动上报使用。</param>
        public RobotListViewModel(
            IEventAggregator eventAggregator,
            IVehicleStateStore vehicleStateStore,
            IVehicleStatusPublisher vehicleStatusPublisher)
        {
            _eventAggregator = eventAggregator;
            _vehicleStateStore = vehicleStateStore;
            _vehicleStatusPublisher = vehicleStatusPublisher;
            PublishStatusCommand = new DelegateCommand(PublishStatus, CanPublishStatus)
                .ObservesProperty(() => VehicleId)
                .ObservesProperty(() => Brand)
                .ObservesProperty(() => Location);

            RobotList = new ObservableCollection<RobotModel>(
                _vehicleStateStore.GetAllVehicles().Select(ToRobotModel));

            foreach (var snapshot in _vehicleStateStore.GetAllVehicles())
            {
                AddVehicleIdOption(snapshot.VehicleId);
            }

            SelectedRobot = RobotList.FirstOrDefault();

            eventAggregator.GetEvent<VehicleStateChangedEvent>().Subscribe(ApplyVehicleStateChange, ThreadOption.UIThread);
        }

        /// <summary>上报命令可执行条件：车辆 ID、品牌、位置均非空。</summary>
        private bool CanPublishStatus()
        {
            return !string.IsNullOrWhiteSpace(VehicleId)
                && !string.IsNullOrWhiteSpace(Brand)
                && !string.IsNullOrWhiteSpace(Location);
        }

        /// <summary>根据表单构造快照并通过发布服务上报，更新结果提示。</summary>
        private void PublishStatus()
        {
            var snapshot = new VehicleStatusSnapshot
            {
                VehicleId = VehicleId.Trim(),
                Brand = Brand.Trim(),
                BatteryLevel = Math.Clamp(BatteryLevel, 0, 100),
                Location = Location.Trim(),
                State = State,
                ReportedAt = DateTime.Now
            };

            var result = _vehicleStatusPublisher.PublishStatus(snapshot);

            LastPublishMessage = result.LowBatteryDetected
                ? $"{result.Snapshot.VehicleId} low battery alert published"
                : $"{result.Snapshot.VehicleId} status updated";
        }

        /// <summary>处理车辆状态变化事件：按新增/更新/移除分别对列表做增量维护。</summary>
        private void ApplyVehicleStateChange(VehicleStateChangedMessage message)
        {
            switch (message.ChangeType)
            {
                case VehicleStateChangeType.Added:
                case VehicleStateChangeType.Updated:
                    if (message.Snapshot is not null)
                    {
                        UpsertRobot(message.Snapshot);
                    }
                    break;

                case VehicleStateChangeType.Removed:
                    RemoveRobot(message.RemovedVehicleId);
                    break;
            }
        }

        /// <summary>新增或更新列表中的某台车；若更新的是当前选中车则同步刷新选中项。</summary>
        private void UpsertRobot(VehicleStatusSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot.VehicleId))
            {
                return;
            }

            var robot = ToRobotModel(snapshot);
            var existingIndex = RobotList
                .Select((item, index) => new { item, index })
                .FirstOrDefault(x => string.Equals(x.item.Id, snapshot.VehicleId, StringComparison.OrdinalIgnoreCase))
                ?.index;

            if (existingIndex.HasValue)
            {
                RobotList[existingIndex.Value] = robot;
                if (string.Equals(SelectedRobot?.Id, robot.Id, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedRobot = robot;
                }

                return;
            }

            RobotList.Add(robot);
            AddVehicleIdOption(snapshot.VehicleId);
            SelectedRobot ??= robot;
        }

        /// <summary>从列表移除指定车辆；若移除的是当前选中车则回退选中首台。</summary>
        private void RemoveRobot(string? vehicleId)
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return;
            }

            var robot = RobotList.FirstOrDefault(item =>
                string.Equals(item.Id, vehicleId, StringComparison.OrdinalIgnoreCase));

            if (robot is not null)
            {
                RobotList.Remove(robot);
                if (string.Equals(SelectedRobot?.Id, robot.Id, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedRobot = RobotList.FirstOrDefault();
                }
            }
        }

        /// <summary>将车辆 ID 加入候选项（去重、忽略空白）。</summary>
        private void AddVehicleIdOption(string vehicleId)
        {
            if (!string.IsNullOrWhiteSpace(vehicleId) && !VehicleIdOptions.Contains(vehicleId))
            {
                VehicleIdOptions.Add(vehicleId);
            }
        }

        /// <summary>将车辆状态快照转换为列表展示模型；目标位置/速度/运行时长为演示占位数据。</summary>
        private static RobotModel ToRobotModel(VehicleStatusSnapshot snapshot)
        {
            return new RobotModel
            {
                Id = snapshot.VehicleId,
                Brand = snapshot.Brand,
                State = snapshot.State,
                TaskId = string.IsNullOrWhiteSpace(snapshot.CurrentTaskId) ? "-" : snapshot.CurrentTaskId,
                CurrentPosition = FormatNodeLocation(snapshot.Location),
                TargetPosition = FormatNodeLocation(GetMockTargetPosition(snapshot.VehicleId)),
                BatteryLevel = (int)Math.Round(snapshot.BatteryLevel),
                Speed = GetMockSpeed(snapshot.State, snapshot.VehicleId),
                RunningTime = GetMockRunningTime(snapshot.VehicleId)
            };
        }

        /// <summary>按车辆 ID 返回演示用目标位置。</summary>
        private static string GetMockTargetPosition(string vehicleId)
        {
            return vehicleId switch
            {
                "AGV-002" => "PUT-A1",
                "AGV-003" => "INT-C",
                "AGV-008" => "RAW-OUT-01",
                "AGV-010" => "FIRE-G2",
                "AGV-017" => "CHG-03",
                _ => "-"
            };
        }

        private static string FormatNodeLocation(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || nodeId == "-")
            {
                return "-";
            }

            return NodeNames.TryGetValue(nodeId, out var name)
                ? $"{nodeId} ({name})"
                : nodeId;
        }

        private static readonly IReadOnlyDictionary<string, string> NodeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PICK-A1"] = "\u6210\u54c1\u5e93\u53d6\u8d27\u70b9A1",
            ["PICK-A2"] = "\u6210\u54c1\u5e93\u53d6\u8d27\u70b9A2",
            ["PUT-A1"] = "\u6210\u54c1\u5e93\u653e\u8d27\u70b9A1",
            ["RAW-IN-01"] = "\u539f\u6599\u533a\u5165\u5e93\u70b91",
            ["RAW-IN-02"] = "\u539f\u6599\u533a\u5165\u5e93\u70b92",
            ["RAW-OUT-01"] = "\u539f\u6599\u533a\u51fa\u5e93\u70b91",
            ["QR-01"] = "\u4e8c\u7ef4\u7801\u5bfc\u822a\u70b91",
            ["QR-02"] = "\u4e8c\u7ef4\u7801\u5bfc\u822a\u70b92",
            ["QR-03"] = "\u4e8c\u7ef4\u7801\u5bfc\u822a\u70b93",
            ["SLAM-01"] = "\u6fc0\u5149\u533a\u5165\u53e3",
            ["SLAM-02"] = "\u6fc0\u5149\u5de5\u4f4d",
            ["INT-N"] = "\u4e92\u65a5\u533a\u5317\u53e3",
            ["INT-C"] = "\u4e92\u65a5\u533a\u4e2d\u5fc3",
            ["INT-S"] = "\u4e92\u65a5\u533a\u5357\u53e3",
            ["FIRE-G1"] = "\u6d88\u9632\u95e8\u524d\u70b9",
            ["FIRE-G2"] = "\u6d88\u9632\u95e8\u540e\u70b9",
            ["SPEED-IN"] = "\u9650\u901f\u533a\u5165\u53e3",
            ["WEIGH-01"] = "\u5730\u78c5\u79f0\u91cd\u70b9",
            ["WASH-01"] = "\u6e05\u6d17\u5de5\u4f4d",
            ["SPEED-OUT"] = "\u9650\u901f\u533a\u51fa\u53e3",
            ["WAIT-01"] = "\u5f85\u673a\u4f4d1",
            ["WAIT-02"] = "\u5f85\u673a\u4f4d2",
            ["WAIT-03"] = "\u5f85\u673a\u4f4d3",
            ["PARK-01"] = "\u505c\u8f66\u4f4d1",
            ["CHG-01"] = "\u5145\u7535\u68691",
            ["CHG-02"] = "\u5145\u7535\u68692",
            ["CHG-03"] = "\u5feb\u5145\u68693",
            ["MAINT-IN"] = "\u7ef4\u62a4\u533a\u5165\u53e3",
            ["MAINT-OUT"] = "\u7ef4\u62a4\u533a\u51fa\u53e3"
        };

        /// <summary>按状态与车辆 ID 返回演示用速度（非运行恒为 0）。</summary>
        private static double GetMockSpeed(RobotState state, string vehicleId)
        {
            if (state != RobotState.Running)
            {
                return 0.00;
            }

            return vehicleId switch
            {
                "AGV-002" => 1.25,
                "AGV-003" => 1.10,
                "AGV-010" => 1.48,
                _ => 0.00
            };
        }

        /// <summary>按车辆 ID 返回演示用累计运行时长文本。</summary>
        private static string GetMockRunningTime(string vehicleId)
        {
            return vehicleId switch
            {
                "AGV-002" => "02:35:23",
                "AGV-003" => "01:45:11",
                "AGV-008" => "00:12:08",
                "AGV-010" => "02:12:56",
                "AGV-017" => "01:10:34",
                _ => "00:00:00"
            };
        }
    }
}
