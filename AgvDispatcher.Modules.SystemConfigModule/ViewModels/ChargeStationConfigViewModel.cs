using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Commands;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.SystemConfigModule.ViewModels
{
    /// <summary>
    /// 系统设置 - 充电桩配置页的 ViewModel。
    /// <para>
    /// 提供充电桩的增删改查（CRUD）能力，直接操作充电桩仓储 <see cref="IChargeStationRepository"/>。
    /// 充电桩必须绑定一个"启用的、类型为 Charge 的地图节点"，因此从地图仓储
    /// <see cref="IMapRepository"/> 加载可用的充电节点 <see cref="AvailableNodeIds"/> 作为约束。
    /// </para>
    /// <para>说明：仓储为异步接口，此处在 UI 操作中以 GetAwaiter().GetResult() 同步等待以简化绑定。</para>
    /// </summary>
    public class ChargeStationConfigViewModel : BindableBase
    {
        private readonly IChargeStationRepository _chargeStationRepository;
        private readonly IMapRepository _mapRepository;

        /// <summary>充电桩列表数据源。</summary>
        public ObservableCollection<ChargeStation> Stations { get; } = new();

        /// <summary>可绑定的充电节点编号（仅含启用且类型为 Charge 的节点）。</summary>
        public ObservableCollection<string> AvailableNodeIds { get; } = new();

        private ChargeStation? _selectedStation;
        /// <summary>当前选中的充电桩。</summary>
        public ChargeStation? SelectedStation
        {
            get => _selectedStation;
            set => SetProperty(ref _selectedStation, value);
        }

        /// <summary>刷新（重新加载充电桩列表）命令。</summary>
        public DelegateCommand RefreshCommand { get; }

        /// <summary>新增充电桩命令。</summary>
        public DelegateCommand AddStationCommand { get; }

        /// <summary>保存当前充电桩命令（需有选中项）。</summary>
        public DelegateCommand SaveStationCommand { get; }

        /// <summary>删除当前充电桩命令（需有选中项）。</summary>
        public DelegateCommand DeleteStationCommand { get; }

        /// <summary>充电桩总数。</summary>
        public int TotalStations => Stations.Count;

        /// <summary>构造函数：注入仓储，绑定命令，加载可用充电节点与充电桩列表。</summary>
        /// <param name="chargeStationRepository">充电桩仓储。</param>
        /// <param name="mapRepository">地图仓储，用于获取可绑定的充电节点。</param>
        public ChargeStationConfigViewModel(IChargeStationRepository chargeStationRepository, IMapRepository mapRepository)
        {
            _chargeStationRepository = chargeStationRepository;
            _mapRepository = mapRepository;

            RefreshCommand = new DelegateCommand(LoadData);

            AddStationCommand = new DelegateCommand(AddStation);
            SaveStationCommand = new DelegateCommand(SaveStation, () => SelectedStation != null).ObservesProperty(() => SelectedStation);
            DeleteStationCommand = new DelegateCommand(DeleteStation, () => SelectedStation != null).ObservesProperty(() => SelectedStation);

            LoadNodesAsync();
            LoadData();
        }

        /// <summary>异步加载可绑定的充电节点（启用且 NodeType=Charge），在 UI 线程回填集合。</summary>
        private async void LoadNodesAsync()
        {
            var nodes = await _mapRepository.GetNodesAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableNodeIds.Clear();
                foreach (var node in nodes.Where(n => n.IsEnabled && n.NodeType == AgvDispatcher.Core.Enums.MapNodeType.Charge))
                {
                    AvailableNodeIds.Add(node.NodeId);
                }
            });
        }

        /// <summary>从仓储重新加载充电桩列表。</summary>
        private void LoadData()
        {
            Stations.Clear();
            var stations = _chargeStationRepository.GetAllAsync().GetAwaiter().GetResult();
            foreach (var s in stations) Stations.Add(s);

            RaisePropertyChanged(nameof(TotalStations));
        }

        /// <summary>
        /// 新增一个充电桩（带默认参数），绑定到首个可用充电节点。
        /// 若当前没有可用充电节点则提示用户先在地图中配置。
        /// </summary>
        private void AddStation()
        {
            if (!AvailableNodeIds.Any())
            {
                System.Windows.MessageBox.Show("当前没有可用的 Charge 类型节点，请先在地图节点中配置", "无法新增", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var nextNumber = Stations.Count + 1;
            var defaultNodeId = AvailableNodeIds.FirstOrDefault() ?? "";
            var newStation = new ChargeStation
            {
                StationId = $"CS{nextNumber:000}",
                StationCode = $"CS{nextNumber:000}",
                Name = $"Charge Station {nextNumber}",
                NodeId = defaultNodeId,
                Position = new MapPosition { MapId = "MAIN", NodeId = defaultNodeId, X = 0, Y = 0 },
                IsEnabled = true,
                RatedPowerKw = 3.3,
                OutputVoltage = 48,
                OutputCurrent = 30
            };
            Stations.Add(newStation);
            SelectedStation = newStation;
            RaisePropertyChanged(nameof(TotalStations));
        }

        /// <summary>
        /// 保存当前充电桩：校验编号非空、绑定节点必须是有效的启用 Charge 节点，通过后写入仓储并刷新。
        /// </summary>
        private void SaveStation()
        {
            if (SelectedStation == null) return;
            if (string.IsNullOrWhiteSpace(SelectedStation.StationId))
            {
                System.Windows.MessageBox.Show("充电桩ID不能为空", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!AvailableNodeIds.Contains(SelectedStation.NodeId))
            {
                System.Windows.MessageBox.Show($"充电桩绑定的节点 '{SelectedStation.NodeId}' 无效。它必须是启用的 Charge 类型节点。", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            _chargeStationRepository.SaveAsync(SelectedStation).GetAwaiter().GetResult();
            LoadData();
        }

        /// <summary>删除当前选中的充电桩并刷新列表。</summary>
        private void DeleteStation()
        {
            if (SelectedStation == null) return;
            _chargeStationRepository.DeleteAsync(SelectedStation.StationId).GetAwaiter().GetResult();
            LoadData();
        }
    }
}
