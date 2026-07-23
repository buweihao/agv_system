using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;
using ContractMap = AgvDispatcher.Core.Contracts.Map;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    /// <summary>
    /// 杩愯鐩戞帶椤甸潰"鍦板浘瑙嗗浘"鐨?ViewModel銆?
    /// <para>
    /// 璐熻矗鎶婂湴鍥炬嫇鎵戯紙鑺傜偣 <see cref="MapNode"/>銆佽竟 <see cref="MapEdge"/>锛夊拰杞﹁締瀹炴椂浣嶇疆娓叉煋鍒扮敾甯冧笂锛?
    /// 骞跺湪閫変腑鏌愬彴 AGV 鏃跺熀浜庣嫭绔嬭矾寰勮鍒掓湇鍔￠珮浜睍绀鸿矾寰勩€?
    /// 閫氳繃璁㈤槄 <see cref="SelectedVehicleChangedEvent"/> 涓?<see cref="VehicleStateChangedEvent"/> 瀹炵幇鑱斿姩涓庡埛鏂般€?
    /// 鍦板浘鍏冪礌琚偣鍑绘椂鍦ㄨ鎯呭尯灞曠ず鑺傜偣/璺緞/杞﹁締鐨勮缁嗕俊鎭€?
    /// </para>
    /// <para>
    /// 浣嶇疆鍖归厤鏀寔鍒悕锛?see cref="IMapLocationAliasRepository"/>锛夈€佽妭鐐圭紪鍙?缂栫爜绮剧‘鍖归厤锛?
    /// 浠ュ強瀵瑰舰濡?"A01" 鐨勪綅缃覆鍋氬綊涓€鍖栫殑妯＄硦鍖归厤锛堣 <see cref="ResolveNode"/>锛夈€?
    /// </para>
    /// </summary>
    public class MapViewModel : BindableBase, IDisposable, IDestructible
    {
        private readonly ContractMap.IMapService _mapService;
        private readonly IPathPlanningService _pathPlanningService;
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly ITaskService _taskService;
        private readonly IMapLocationAliasRepository _aliasRepo;
        private readonly SubscriptionToken _selectedVehicleSubscription;
        private readonly SubscriptionToken _vehicleStateSubscription;
        private readonly SubscriptionToken _mapPublishedSubscription;
        private readonly Dictionary<string, VehicleMapViewItem> _vehicleItems =
            new(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<MapLocationAlias> _aliases = new List<MapLocationAlias>();
        private IReadOnlyList<MapNode> _mapNodes = Array.Empty<MapNode>();

        private string? _selectedVehicleId;
        private string _pathSummary = "\u8bf7\u9009\u62e9 AGV \u67e5\u770b\u89c4\u5212\u8def\u5f84";

        // 璺緞瑙勫垝棰勮锛氭墜鍔ㄩ€夋嫨鐨勮捣鐐?缁堢偣涓庣粨鏋滄憳瑕?
        private MapNodeViewItem? _previewStartNode;
        private MapNodeViewItem? _previewEndNode;
        private string? _previewStartNodeId;
        private string? _previewEndNodeId;
        private bool _isReloadingMap;
        private bool _isUpdatingPreviewSelection;
        private string _previewSummary = "\u9009\u62e9\u8d77\u70b9\u4e0e\u7ec8\u70b9\u540e\u70b9\u51fb\u201c\u9884\u89c8\u8def\u5f84\u201d";
        private bool _isPreviewPanelExpanded = true;
        private bool _disposed;

        public Task Initialization { get; }

        /// <summary>鍦板浘鍏ㄩ儴杈癸紙鏅€氭覆鏌撳浘灞傦級銆?/summary>
        public ObservableCollection<MapEdgeViewItem> Edges { get; } = new();

        public ObservableCollection<MapAreaViewItem> Areas { get; } = new();

        /// <summary>褰撳墠閫変腑杞﹁締鐨勮鍒掕矾寰勬墍缁忚繃鐨勮竟锛堥珮浜浘灞傦級銆?/summary>
        public ObservableCollection<MapEdgeViewItem> PlannedEdges { get; } = new();

        /// <summary>鎵嬪姩棰勮璺緞鎵€缁忚繃鐨勮竟锛堥瑙堥珮浜浘灞傦紝鐙珛浜庤溅杈嗚矾寰勶級銆?/summary>
        public ObservableCollection<MapEdgeViewItem> PreviewEdges { get; } = new();

        /// <summary>鍚勮竟鐨勬柟鍚戠澶达紙鍙犲姞鍦ㄦ櫘閫氳竟鍥惧眰涔嬩笂锛岃〃杈鹃€氳鏂瑰悜锛夈€?/summary>
        public ObservableCollection<MapEdgeArrowViewItem> EdgeArrows { get; } = new();

        /// <summary>鍦板浘鍏ㄩ儴鑺傜偣銆?/summary>
        public ObservableCollection<MapNodeViewItem> Nodes { get; } = new();

        /// <summary>杞﹁締褰撳墠浣嶇疆鏍囪銆?/summary>
        public ObservableCollection<VehicleMapViewItem> Vehicles { get; } = new();

        /// <summary>璺緞鎽樿鏂囨湰锛堝璧锋鐐广€佺偣鏁般€佹€婚噷绋嬶紝鎴栦笉鍙敤鎻愮ず锛夈€?/summary>
        public string PathSummary
        {
            get => _pathSummary;
            set => SetProperty(ref _pathSummary, value);
        }

        private string _detailTitle = "\u8be6\u60c5";
        /// <summary>璇︽儏鍖烘爣棰樸€?/summary>
        public string DetailTitle
        {
            get => _detailTitle;
            set => SetProperty(ref _detailTitle, value);
        }

        private string _detailContent = "\u70b9\u51fb\u5730\u56fe\u5143\u7d20\u67e5\u770b\u8be6\u60c5";
        /// <summary>璇︽儏鍖哄唴瀹广€?/summary>
        public string DetailContent
        {
            get => _detailContent;
            set => SetProperty(ref _detailContent, value);
        }

        /// <summary>鍦板浘鍏冪礌鐐瑰嚮鍛戒护锛屽弬鏁颁负琚偣鍑荤殑鑺傜偣/杈?杞﹁締瑙嗗浘椤广€?/summary>
        public DelegateCommand<object> MapItemClickCommand { get; }

        /// <summary>璺緞棰勮鐨勮捣鐐硅妭鐐癸紙渚涚晫闈笅鎷夌粦瀹氾級銆?/summary>
        public MapNodeViewItem? PreviewStartNode
        {
            get => _previewStartNode;
            set
            {
                if (_isReloadingMap && value is null)
                {
                    return;
                }

                if (SetProperty(ref _previewStartNode, value))
                {
                    _previewStartNodeId = value?.NodeId;
                    RaisePropertyChanged(nameof(PreviewStartNodeId));
                    OnPreviewSelectionChanged();
                }
            }
        }

        public string? PreviewStartNodeId
        {
            get => _previewStartNodeId;
            set
            {
                if (_isReloadingMap && string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                if (SetProperty(ref _previewStartNodeId, value))
                {
                    _previewStartNode = string.IsNullOrWhiteSpace(value)
                        ? null
                        : Nodes.FirstOrDefault(node => string.Equals(node.NodeId, value, StringComparison.OrdinalIgnoreCase));
                    RaisePropertyChanged(nameof(PreviewStartNode));
                    OnPreviewSelectionChanged();
                }
            }
        }

        /// <summary>璺緞棰勮鐨勭粓鐐硅妭鐐癸紙渚涚晫闈笅鎷夌粦瀹氾級銆?/summary>
        public MapNodeViewItem? PreviewEndNode
        {
            get => _previewEndNode;
            set
            {
                if (_isReloadingMap && value is null)
                {
                    return;
                }

                if (SetProperty(ref _previewEndNode, value))
                {
                    _previewEndNodeId = value?.NodeId;
                    RaisePropertyChanged(nameof(PreviewEndNodeId));
                    OnPreviewSelectionChanged();
                }
            }
        }

        public string? PreviewEndNodeId
        {
            get => _previewEndNodeId;
            set
            {
                if (_isReloadingMap && string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                if (SetProperty(ref _previewEndNodeId, value))
                {
                    _previewEndNode = string.IsNullOrWhiteSpace(value)
                        ? null
                        : Nodes.FirstOrDefault(node => string.Equals(node.NodeId, value, StringComparison.OrdinalIgnoreCase));
                    RaisePropertyChanged(nameof(PreviewEndNode));
                    OnPreviewSelectionChanged();
                }
            }
        }

        /// <summary>璺緞棰勮缁撴灉鎽樿锛堣捣姝㈢偣銆佺偣鏁般€佹€婚噷绋嬫垨涓嶅彲杈炬彁绀猴級銆?/summary>
        public string PreviewSummary
        {
            get => _previewSummary;
            set => SetProperty(ref _previewSummary, value);
        }

        /// <summary>璺緞棰勮闈㈡澘鏄惁灞曞紑銆備繚鎸佸湪 ViewModel锛岄伩鍏嶇偣鍑诲湴鍥惧悗琚鍥鹃噸寤轰负鎶樺彔鐘舵€併€?/summary>
        public bool IsPreviewPanelExpanded
        {
            get => _isPreviewPanelExpanded;
            set => SetProperty(ref _isPreviewPanelExpanded, value);
        }

        /// <summary>鏍规嵁鎵€閫夎捣鐐?缁堢偣璁＄畻骞堕珮浜樉绀洪瑙堣矾寰勩€?/summary>
        public DelegateCommand PreviewPathCommand { get; }

        /// <summary>娓呴櫎褰撳墠棰勮璺緞涓庨€夋嫨銆?/summary>
        public DelegateCommand ClearPreviewCommand { get; }

        /// <summary>
        /// 鏋勯€犲嚱鏁帮紝娉ㄥ叆渚濊禆銆佸姞杞藉埆鍚嶅苟棣栨缁樺埗鍦板浘锛屽悓鏃惰闃呴€変腑杞﹁締涓庣姸鎬佸彉鍖栦簨浠躲€?
        /// </summary>
        /// <param name="eventAggregator">浜嬩欢鑱氬悎鍣ㄣ€?/param>
        /// <param name="mapService">鍦板浘鏈嶅姟锛屾彁渚涜妭鐐?杈规煡璇笌璺緞瑙勫垝銆?/param>
        /// <param name="vehicleStateStore">杞﹁締鐘舵€佸瓨鍌紝鎻愪緵杞﹁締瀹炴椂浣嶇疆銆?/param>
        /// <param name="taskService">浠诲姟鏈嶅姟锛岀敤浜庤В鏋愯溅杈嗗綋鍓嶄换鍔＄殑鐩爣鑺傜偣銆?/param>
        /// <param name="aliasRepo">鍦板浘鍒悕浠撳偍锛岀敤浜庝綅缃埆鍚嶅埌鑺傜偣鐨勬槧灏勩€?/param>
        public MapViewModel(
            IEventAggregator eventAggregator,
            ContractMap.IMapService mapService,
            IPathPlanningService pathPlanningService,
            IVehicleStateStore vehicleStateStore,
            ITaskService taskService,
            IMapLocationAliasRepository aliasRepo)
        {
            _mapService = mapService;
            _pathPlanningService = pathPlanningService;
            _vehicleStateStore = vehicleStateStore;
            _taskService = taskService;
            _aliasRepo = aliasRepo;

            MapItemClickCommand = new DelegateCommand<object>(OnMapItemClicked);
            PreviewPathCommand = new DelegateCommand(OnPreviewPath, CanPreviewPath);
            ClearPreviewCommand = new DelegateCommand(OnClearPreview);

            // 鍏堝紓姝ュ姞杞藉埆鍚嶏紝瀹屾垚鍚庡湪 UI 绾跨▼棣栨缁樺埗鍦板浘
            Initialization = InitializeAsync();
            // 閫変腑杞﹁締鍙樺寲锛氶噸缁樺苟楂樹寒鍏惰矾寰?
            _selectedVehicleSubscription = eventAggregator.GetEvent<SelectedVehicleChangedEvent>()
                .Subscribe(LoadMap, ThreadOption.UIThread);
            // 杞﹁締鐘舵€佸彉鍖栵細淇濇寔褰撳墠閫変腑杞﹁締骞堕噸缁?
            _vehicleStateSubscription = eventAggregator.GetEvent<VehicleStateChangedEvent>()
                .Subscribe(OnVehicleStateChanged, ThreadOption.UIThread);
            _mapPublishedSubscription = eventAggregator.GetEvent<PubSubEvent<MapPublishedEvent>>()
                .Subscribe(_ => LoadMap(_selectedVehicleId), ThreadOption.UIThread);
        }

        /// <summary>
        /// 鍦板浘鍏冪礌琚偣鍑绘椂濉厖璇︽儏鍖猴細鍒嗗埆澶勭悊鑺傜偣銆佽竟銆佽溅杈嗕笁绫昏鍥鹃」銆?
        /// 鑺傜偣鐨勫埆鍚嶄俊鎭紓姝ヨ幏鍙栧悗鍥炲～銆?
        /// </summary>
        private void OnMapItemClicked(object item)
        {
            if (item is MapNodeViewItem node)
            {
                UseNodeForPreview(node);
                DetailTitle = $"点位详情: {node.Name}";
                DetailContent = $"点位ID: {node.NodeId}\n点位编码: {node.NodeCode}\n类型: {node.NodeType}\n所属区域: {node.AreaName}\n" +
                                $"启用状态: {(node.IsEnabled ? "启用" : "禁用")}\n厂商/别名映射: 加载中...";

                Task.Run(() =>
                {
                    var nodeAliases = _aliases.Where(a => a.NodeId == node.NodeId).Select(a => a.AliasValue);
                    var aliasStr = nodeAliases.Any() ? string.Join(", ", nodeAliases) : "无";
                    System.Windows.Application.Current.Dispatcher.Invoke(() => DetailContent = DetailContent.Replace("加载中...", aliasStr));
                });
            }
            else if (item is MapEdgeViewItem edge)
            {
                DetailTitle = "路线详情";
                DetailContent = $"路线ID: {edge.EdgeId}\n起点: {edge.FromNodeId}\n终点: {edge.ToNodeId}\n" +
                                $"方向: {(edge.IsBidirectional ? "双向" : "单向")}\n限速: {edge.MaxSpeed} m/s\n状态: {(edge.IsEnabled ? "启用" : "禁用")}";
            }
            else if (item is VehicleMapViewItem vehicle)
            {
                DetailTitle = $"车辆详情: {vehicle.VehicleId}";
                var taskStr = string.IsNullOrWhiteSpace(vehicle.CurrentTaskId) ? "无任务" : vehicle.CurrentTaskId;
                DetailContent = $"状态: {vehicle.StateText}\n当前点位: {vehicle.Location}\n当前任务: {taskStr}\n" +
                                $"坐标: ({vehicle.X:F2}, {vehicle.Y:F2})\n朝向: {vehicle.Heading:F1}°\n电量: {vehicle.BatteryLevel:F1}%";
            }
        }
        /// <summary>寮傛鍔犺浇鍏ㄩ儴鍦板浘浣嶇疆鍒悕鍒板唴瀛樼紦瀛樸€?/summary>
        private async Task LoadAliasesAsync()
        {
            _aliases = await _aliasRepo.GetAllAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadAliasesAsync().ConfigureAwait(false);
            if (_disposed)
            {
                return;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                LoadMap(null);
                return;
            }

            await dispatcher.InvokeAsync(() => LoadMap(null));
        }

        /// <summary>
        /// 閲嶆柊缁樺埗鏁村紶鍦板浘锛氭瀯寤鸿竟/鑺傜偣/杞﹁締瑙嗗浘椤癸紝骞跺閫変腑杞﹁締鐨勮鍒掕矾寰勫仛楂樹寒鍙犲姞銆?
        /// </summary>
        /// <param name="selectedVehicleId">褰撳墠閫変腑鐨勮溅杈?ID锛涗负绌鸿〃绀轰笉楂樹寒浠讳綍璺緞銆?/param>
        private void LoadMap(string? selectedVehicleId)
        {
            _selectedVehicleId = selectedVehicleId;

            var nodes = GetNodes();
            _mapNodes = nodes;
            var edges = GetEdges();
            var areas = GetAreas();
            var nodeMap = nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            var areaNameMap = areas.ToDictionary(area => area.AreaId, GetAreaDisplayName, StringComparer.OrdinalIgnoreCase);
            var selectedVehicle = string.IsNullOrWhiteSpace(selectedVehicleId)
                ? null
                : _vehicleStateStore.GetVehicle(selectedVehicleId);
            // 璁＄畻閫変腑杞﹁締鐨勮鍒掕矾寰勶紝骞跺彇鍑哄叾杈归泦鍚堢敤浜庨珮浜垽瀹?
            var plannedPath = CreateSelectedVehiclePath(selectedVehicle, nodes);
            var plannedEdgeIds = plannedPath?.Edges
                .Select(edge => edge.EdgeId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 鏅€氳竟鍥惧眰锛堜粎娓叉煋涓ょ鑺傜偣閮藉瓨鍦ㄧ殑杈癸級
            Areas.Clear();
            foreach (var area in areas)
            {
                Areas.Add(CreateAreaItem(area));
            }

            Edges.Clear();
            EdgeArrows.Clear();
            foreach (var edge in edges)
            {
                if (nodeMap.TryGetValue(edge.FromNodeId, out var fromNode)
                    && nodeMap.TryGetValue(edge.ToNodeId, out var toNode))
                {
                    var edgeItem = CreateEdgeItem(edge, fromNode, toNode, plannedEdgeIds.Contains(edge.EdgeId));
                    Edges.Add(edgeItem);
                    foreach (var arrow in CreateEdgeArrows(edge, fromNode, toNode, edgeItem.Stroke))
                    {
                        EdgeArrows.Add(arrow);
                    }
                }
            }

            // 瑙勫垝璺緞楂樹寒鍥惧眰锛堥噾鑹插姞绮楋級
            PlannedEdges.Clear();
            if (plannedPath is not null)
            {
                foreach (var edge in plannedPath.Edges)
                {
                    if (nodeMap.TryGetValue(edge.FromNodeId, out var fromNode)
                        && nodeMap.TryGetValue(edge.ToNodeId, out var toNode))
                    {
                        var item = CreateEdgeItem(edge, fromNode, toNode, true);
                        item.Stroke = "#FFD700";
                        item.StrokeThickness = 5;
                        item.Opacity = 0.95;
                        PlannedEdges.Add(item);
                    }
                }
            }

            // 鑺傜偣鍥惧眰锛堣矾寰勭粡杩囩殑鑺傜偣鎻忚竟楂樹寒锛?
            _isReloadingMap = true;
            try
            {
                Nodes.Clear();
                foreach (var node in nodes)
                {
                    var areaName = areaNameMap.TryGetValue(node.AreaCode, out var displayAreaName)
                        ? displayAreaName
                        : node.AreaCode;
                    Nodes.Add(CreateNodeItem(node, plannedPath?.Nodes.Any(pathNode =>
                        string.Equals(pathNode.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase)) == true, areaName));
                }

                // 閲嶅缓鑺傜偣闆嗗悎鍚庯紝閲嶆柊瑙ｆ瀽棰勮璧锋鐐瑰紩鐢ㄥ苟鍒锋柊棰勮璺緞鍥惧眰
                RefreshPreviewAfterReload(nodes);
            }
            finally
            {
                _isReloadingMap = false;
            }

            // 杞﹁締浣嶇疆鏍囪
            Vehicles.Clear();
            _vehicleItems.Clear();
            foreach (var vehicle in CreateVehiclePositions(nodes, selectedVehicleId))
            {
                Vehicles.Add(vehicle);
                _vehicleItems[vehicle.VehicleId] = vehicle;
            }

            PathSummary = BuildPathSummary(selectedVehicle, plannedPath, nodes);
        }

        private void OnVehicleStateChanged(VehicleStateChangedMessage message)
        {
            if (message.ChangeType == VehicleStateChangeType.Removed)
            {
                RemoveVehicleItem(message.RemovedVehicleId);
                if (string.Equals(message.RemovedVehicleId, _selectedVehicleId, StringComparison.OrdinalIgnoreCase))
                {
                    LoadMap(null);
                }

                return;
            }

            if (message.Snapshot is null)
            {
                return;
            }

            _vehicleItems.TryGetValue(message.Snapshot.VehicleId, out var existing);
            var pathInputsChanged = existing is not null &&
                (!string.Equals(existing.Location, message.Snapshot.Location, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(existing.CurrentTaskId, message.Snapshot.CurrentTaskId, StringComparison.OrdinalIgnoreCase));

            UpsertVehicleItem(message.Snapshot);

            if (pathInputsChanged &&
                string.Equals(message.Snapshot.VehicleId, _selectedVehicleId, StringComparison.OrdinalIgnoreCase))
            {
                // Logical node/task changes can change the highlighted route. Continuous
                // animation frames do not enter this branch and therefore never rebuild the map.
                LoadMap(_selectedVehicleId);
            }
        }

        private void UpsertVehicleItem(VehicleStatusSnapshot snapshot)
        {
            var item = CreateVehicleItem(snapshot, _mapNodes, _selectedVehicleId);
            if (item is null)
            {
                RemoveVehicleItem(snapshot.VehicleId);
                return;
            }

            if (_vehicleItems.TryGetValue(snapshot.VehicleId, out var existing))
            {
                existing.UpdateFrom(item);
                return;
            }

            _vehicleItems[snapshot.VehicleId] = item;
            Vehicles.Add(item);
        }

        private void RemoveVehicleItem(string? vehicleId)
        {
            if (string.IsNullOrWhiteSpace(vehicleId) || !_vehicleItems.Remove(vehicleId, out var item))
            {
                return;
            }

            Vehicles.Remove(item);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _selectedVehicleSubscription.Dispose();
            _vehicleStateSubscription.Dispose();
            _mapPublishedSubscription.Dispose();
        }

        public void Destroy() => Dispose();

        /// <summary>
        /// 鍦板浘閲嶈浇鍚庝慨澶嶉瑙堣捣姝㈢偣瀵规柊鑺傜偣瑙嗗浘椤圭殑寮曠敤锛屽苟鎹閲嶇粯棰勮璺緞鍥惧眰銆?
        /// 鑻ュ師璧锋鐐瑰湪鏂板湴鍥句腑涓嶅瓨鍦ㄥ垯娓呯┖棰勮銆?
        /// </summary>
        private void RefreshPreviewAfterReload(IReadOnlyList<MapNode> mapNodes)
        {
            var startId = _previewStartNodeId ?? _previewStartNode?.NodeId;
            var endId = _previewEndNodeId ?? _previewEndNode?.NodeId;

            // 閲嶆柊鎸囧悜 Nodes 闆嗗悎涓殑鏂板疄渚嬶紙閬垮厤涓嬫媺妗嗛€変腑椤逛笌鍒楄〃椤逛笉涓€鑷达級
            _previewStartNode = string.IsNullOrEmpty(startId)
                ? null
                : Nodes.FirstOrDefault(n => string.Equals(n.NodeId, startId, StringComparison.OrdinalIgnoreCase));
            _previewEndNode = string.IsNullOrEmpty(endId)
                ? null
                : Nodes.FirstOrDefault(n => string.Equals(n.NodeId, endId, StringComparison.OrdinalIgnoreCase));
            _previewStartNodeId = _previewStartNode?.NodeId;
            _previewEndNodeId = _previewEndNode?.NodeId;
            RaisePropertyChanged(nameof(PreviewStartNode));
            RaisePropertyChanged(nameof(PreviewEndNode));
            RaisePropertyChanged(nameof(PreviewStartNodeId));
            RaisePropertyChanged(nameof(PreviewEndNodeId));
            PreviewPathCommand.RaiseCanExecuteChanged();

            // 鍙璧锋鐐逛粛鏈夋晥锛屽氨鍦ㄥ埛鏂板悗鑷姩鎭㈠棰勮璺緞锛屼笉渚濊禆鏃?PreviewEdges 鏄惁杩樺瓨鍦ㄣ€?
            if (_previewStartNode is not null && _previewEndNode is not null)
            {
                RenderPreviewPath(mapNodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// 鐐瑰嚮鍦板浘鐐逛綅鏃惰緟鍔╅€夋嫨棰勮璧风粓鐐癸細棣栨鐐归€夎捣鐐癸紝绗簩娆＄偣閫夌粓鐐瑰苟鑷姩棰勮銆?
        /// 鑻ュ凡缁忓瓨鍦ㄥ畬鏁磋捣缁堢偣锛屽啀鐐瑰叾浠栬妭鐐瑰垯寮€濮嬩竴鏉℃柊鐨勯瑙堛€?
        /// </summary>
        private void UseNodeForPreview(MapNodeViewItem node)
        {
            IsPreviewPanelExpanded = true;

            if (PreviewStartNode is null)
            {
                SetPreviewSelection(
                    startNode: node,
                    endNode: null,
                    clearEdges: false,
                    summary: $"已选择起点 {node.LabelText} ({node.NodeId})，请点击终点或从下拉框选择。",
                    renderIfComplete: false);
                return;
            }

            if (PreviewEndNode is not null)
            {
                PreviewSummary = $"当前起点 {PreviewStartNode.LabelText} ({PreviewStartNode.NodeId})，终点 {PreviewEndNode.LabelText} ({PreviewEndNode.NodeId})。如需重新选择，请先点击“清除”。";
                return;
            }

            if (PreviewStartNode is { } startNode &&
                !string.Equals(startNode.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase))
            {
                SetPreviewSelection(
                    startNode,
                    node,
                    clearEdges: false,
                    summary: null,
                    renderIfComplete: true);
            }
            else
            {
                PreviewSummary = $"已选择起点 {node.LabelText} ({node.NodeId})，请点击不同点位作为终点。";
            }
        }

        private void OnPreviewSelectionChanged()
        {
            PreviewPathCommand.RaiseCanExecuteChanged();
            if (_isUpdatingPreviewSelection)
            {
                return;
            }

            if (CanPreviewPath())
            {
                var nodeMap = GetNodes().ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
                RenderPreviewPath(nodeMap);
            }
            else if (_previewStartNode is not null && _previewEndNode is null)
            {
                PreviewSummary = $"已选择起点 {_previewStartNode.LabelText} ({_previewStartNode.NodeId})，请点击终点或从下拉框选择。";
            }
            else if (_previewStartNode is null && _previewEndNode is not null)
            {
                PreviewSummary = $"已选择终点 {_previewEndNode.LabelText} ({_previewEndNode.NodeId})，请继续选择起点。";
            }
        }

        private void SetPreviewSelection(
            MapNodeViewItem? startNode,
            MapNodeViewItem? endNode,
            bool clearEdges,
            string? summary,
            bool renderIfComplete)
        {
            _isUpdatingPreviewSelection = true;
            try
            {
                _previewStartNode = startNode;
                _previewEndNode = endNode;
                _previewStartNodeId = startNode?.NodeId;
                _previewEndNodeId = endNode?.NodeId;
                RaisePropertyChanged(nameof(PreviewStartNode));
                RaisePropertyChanged(nameof(PreviewEndNode));
                RaisePropertyChanged(nameof(PreviewStartNodeId));
                RaisePropertyChanged(nameof(PreviewEndNodeId));
                PreviewPathCommand.RaiseCanExecuteChanged();
            }
            finally
            {
                _isUpdatingPreviewSelection = false;
            }

            if (clearEdges)
            {
                PreviewEdges.Clear();
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                PreviewSummary = summary;
            }

            if (renderIfComplete && CanPreviewPath())
            {
                OnPreviewPath();
            }
        }

        /// <summary>鏄惁鍏佽鎵ц棰勮锛氬凡閫夋嫨璧风偣鍜岀粓鐐癸紝涓斾袱鑰呬笉鍚屻€?/summary>
        private bool CanPreviewPath()
            => _previewStartNode is not null
               && _previewEndNode is not null
               && !string.Equals(_previewStartNode.NodeId, _previewEndNode.NodeId, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 璁＄畻鎵€閫夎捣鐐瑰埌缁堢偣鐨勮鍒掕矾寰勶紝娓叉煋棰勮楂樹寒鍥惧眰骞舵洿鏂伴瑙堟憳瑕併€?
        /// </summary>
        private void OnPreviewPath()
        {
            if (_previewStartNode is null || _previewEndNode is null)
            {
                return;
            }

            var nodeMap = GetNodes().ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            RenderPreviewPath(nodeMap);
        }

        /// <summary>
        /// 鐢ㄥ綋鍓嶈捣姝㈢偣鍚戝湴鍥炬湇鍔¤姹傝鍒掕矾寰勶紝濉厖 <see cref="PreviewEdges"/> 楂樹寒鍥惧眰骞惰缃?<see cref="PreviewSummary"/>銆?
        /// </summary>
        private void RenderPreviewPath(IReadOnlyDictionary<string, MapNode> nodeMap)
        {
            if (_previewStartNode is null || _previewEndNode is null)
            {
                PreviewSummary = "\u9009\u62e9\u8d77\u70b9\u4e0e\u7ec8\u70b9\u540e\u70b9\u51fb\u201c\u9884\u89c8\u8def\u5f84\u201d";
                return;
            }

            var startId = _previewStartNode.NodeId;
            var endId = _previewEndNode.NodeId;
            var path = BuildDisplayPath(startId, endId);

            if (path is null || !path.IsAvailable || path.Edges.Count == 0)
            {
                PreviewSummary = $"{startId} -> {endId}: "
                    + (string.IsNullOrWhiteSpace(path?.Message) ? "暂无可用路径" : path!.Message);
                return;
            }

            var previewItems = new List<MapEdgeViewItem>();
            foreach (var edge in path.Edges)
            {
                if (nodeMap.TryGetValue(edge.FromNodeId, out var fromNode)
                    && nodeMap.TryGetValue(edge.ToNodeId, out var toNode))
                {
                    var item = CreateEdgeItem(edge, fromNode, toNode, true);
                    item.Stroke = "#00E5FF";   // 闈掕壊锛屽尯鍒簬杞﹁締閲戣壊璺緞
                    item.StrokeThickness = 4;
                    item.Opacity = 0.95;
                    previewItems.Add(item);
                }
            }

            if (previewItems.Count == 0)
            {
                PreviewSummary = $"{startId} -> {endId}: 暂无可显示路径";
                return;
            }

            PreviewEdges.Clear();
            foreach (var item in previewItems)
            {
                PreviewEdges.Add(item);
            }

            PreviewSummary = $"{path.StartNodeId} -> {path.EndNodeId}，共 {path.Nodes.Count} 个点，{path.TotalLength:F0} m";
        }

        /// <summary>娓呴櫎棰勮璺緞鍥惧眰銆侀噸缃捣姝㈢偣閫夋嫨涓庢憳瑕併€?/summary>
        private void OnClearPreview()
        {
            SetPreviewSelection(
                startNode: null,
                endNode: null,
                clearEdges: true,
                summary: "\u9009\u62e9\u8d77\u70b9\u4e0e\u7ec8\u70b9\u540e\u70b9\u51fb\u201c\u9884\u89c8\u8def\u5f84\u201d",
                renderIfComplete: false);
        }

        /// <summary>
        /// 涓洪€変腑杞﹁締璁＄畻瑙勫垝璺緞锛氳В鏋愬叾褰撳墠浣嶇疆涓鸿捣鐐广€佸叾浠诲姟鐩爣涓虹粓鐐瑰悗璋冪敤鍦板浘鏈嶅姟瑙勫垝銆?
        /// 璧风粓鐐圭己澶辨垨鐩稿悓鍒欒繑鍥?null銆?
        /// </summary>
        private PlannedPath? CreateSelectedVehiclePath(VehicleStatusSnapshot? vehicle, IReadOnlyList<MapNode> nodes)
        {
            if (vehicle is null)
            {
                return null;
            }

            var startNode = ResolveNode(vehicle.Location, nodes, _aliases);
            var targetNodeId = ResolveTargetNodeId(vehicle);

            if (startNode is null || string.IsNullOrWhiteSpace(targetNodeId))
            {
                return null;
            }

            var targetNode = ResolveNode(targetNodeId, nodes, _aliases);
            if (targetNode is null || string.Equals(startNode.NodeId, targetNode.NodeId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return BuildDisplayPath(startNode.NodeId, targetNode.NodeId);
        }

        private IReadOnlyList<MapNode> GetNodes()
        {
            var result = _mapService.GetNodes(new ContractMap.GetMapSnapshotRequest { Context = new RequestContext() });
            return result.Data?.Select(ToLegacyNode).ToList() ?? [];
        }

        private IReadOnlyList<MapEdge> GetEdges()
        {
            var result = _mapService.GetEdges(new ContractMap.GetMapSnapshotRequest { Context = new RequestContext() });
            return result.Data?.Select(ToLegacyEdge).ToList() ?? [];
        }

        private IReadOnlyList<ContractMap.MapAreaDto> GetAreas()
        {
            var result = _mapService.GetCurrentMap(new ContractMap.GetMapSnapshotRequest { Context = new RequestContext() });
            return result.Data?.Areas ?? [];
        }

        private PlannedPath BuildDisplayPath(string startNodeId, string endNodeId)
        {
            return _pathPlanningService.PlanPath(GetNodes(), GetEdges(), startNodeId, endNodeId);
        }

        private static MapNode ToLegacyNode(ContractMap.MapNodeDto node)
        {
            return new MapNode
            {
                NodeId = node.NodeId,
                MapId = "MAIN",
                NodeCode = node.NodeCode,
                Name = string.IsNullOrWhiteSpace(node.NodeName) ? node.NodeCode : node.NodeName,
                NodeType = ToLegacyNodeType(node.NodeType),
                Position = new MapPosition { MapId = "MAIN", NodeId = node.NodeId, X = node.X, Y = node.Y },
                Heading = node.Angle ?? 0,
                AreaCode = node.AreaId ?? string.Empty,
                IsEnabled = node.Enabled,
                ParkingCapacity = TryReadInt(node.Properties, "Capacity", 1),
                AllowedBrands = TryReadString(node.Properties, "AllowedBrands")
            };
        }

        private static MapEdge ToLegacyEdge(ContractMap.MapEdgeDto edge)
        {
            return new MapEdge
            {
                EdgeId = edge.EdgeId,
                MapId = "MAIN",
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Direction = edge.Enabled
                    ? edge.Direction == ContractMap.MapEdgeDirection.Bidirectional ? EdgeDirection.Bidirectional : EdgeDirection.ForwardOnly
                    : EdgeDirection.Closed,
                Length = edge.Distance,
                MaxSpeed = edge.SpeedLimit ?? 0,
                Cost = (int)Math.Round(edge.Cost),
                IsEnabled = edge.Enabled,
                AreaCode = edge.AreaId ?? string.Empty,
                AllowedBrands = TryReadString(edge.Properties, "AllowedBrands"),
                MaxVehicleFlow = TryReadInt(edge.Properties, "MaxVehicleFlow", 1),
                Remark = TryReadString(edge.Properties, "Remark")
            };
        }

        private static MapNodeType ToLegacyNodeType(ContractMap.MapNodeType nodeType) => nodeType switch
        {
            ContractMap.MapNodeType.WorkStation => MapNodeType.Station,
            ContractMap.MapNodeType.PickPoint => MapNodeType.Pickup,
            ContractMap.MapNodeType.PutPoint => MapNodeType.Dropoff,
            ContractMap.MapNodeType.ChargeStation => MapNodeType.Charge,
            ContractMap.MapNodeType.WaitingPoint => MapNodeType.Waiting,
            ContractMap.MapNodeType.Elevator => MapNodeType.Elevator,
            ContractMap.MapNodeType.Door => MapNodeType.Door,
            _ => MapNodeType.Normal
        };

        private static int TryReadInt(IReadOnlyDictionary<string, string> properties, string key, int fallback)
        {
            return properties.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;
        }

        private static string TryReadString(IReadOnlyDictionary<string, string> properties, string key)
        {
            return properties.TryGetValue(key, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// 鏋勫缓璺緞鎽樿鏂囨湰锛岃鐩栵細鏈€夎溅銆佷綅缃湭鍖归厤銆佹棤浠诲姟鐩爣銆佹棤鍙敤璺緞銆佹甯歌矾寰勭瓑澶氱鎯呭舰銆?
        /// </summary>
        private string BuildPathSummary(VehicleStatusSnapshot? vehicle, PlannedPath? plannedPath, IReadOnlyList<MapNode> nodes)
        {
            if (vehicle is null)
            {
                return "\u8bf7\u9009\u62e9 AGV \u67e5\u770b\u89c4\u5212\u8def\u5f84";
            }

            var startNode = ResolveNode(vehicle.Location, nodes, _aliases);
            var targetNodeId = ResolveTargetNodeId(vehicle);

            if (startNode is null)
            {
                return $"{vehicle.VehicleId} \u5f53\u524d\u4f4d\u7f6e {vehicle.Location} \u672a\u5339\u914d\u5230\u5730\u56fe\u70b9\u4f4d";
            }

            if (string.IsNullOrWhiteSpace(targetNodeId))
            {
                return $"{vehicle.VehicleId} 当前无任务目标，仅显示车辆位置";
            }

            if (plannedPath is null)
            {
                return $"{vehicle.VehicleId} {startNode.NodeId} -> {targetNodeId} \u6682\u65e0\u53ef\u7528\u8def\u5f84";
            }

            return plannedPath.IsAvailable
                ? $"{vehicle.VehicleId} {plannedPath.StartNodeId} -> {plannedPath.EndNodeId}，共 {plannedPath.Nodes.Count} 个点，{plannedPath.TotalLength:F0} m"
                : $"{vehicle.VehicleId} {plannedPath.Message}";
        }

        /// <summary>
        /// 瑙ｆ瀽杞﹁締鐨勭洰鏍囪妭鐐癸細浼樺厛鍙栧叾褰撳墠浠诲姟鐨勭洰鏍囪妭鐐癸紱鍚﹀垯鏌ユ壘鍒嗛厤缁欒杞︿笖澶勪簬寰呮墽琛?鎵ц涓殑浠诲姟鐩爣銆?
        /// </summary>
        private string? ResolveTargetNodeId(VehicleStatusSnapshot vehicle)
        {
            if (!string.IsNullOrWhiteSpace(vehicle.CurrentTaskId))
            {
                var task = _taskService.GetTask(vehicle.CurrentTaskId);
                if (!string.IsNullOrWhiteSpace(task?.TargetNodeId))
                {
                    return task.TargetNodeId;
                }
            }

            var activeTask = _taskService.GetTasks()
                .FirstOrDefault(task =>
                    string.Equals(task.AssignedVehicleId, vehicle.VehicleId, StringComparison.OrdinalIgnoreCase)
                    && task.State is TaskState.Pending or TaskState.Running);

            return activeTask?.TargetNodeId;
        }

        /// <summary>
        /// 鐢遍鍩熻竟妯″瀷鏋勫缓鐢诲竷杈硅鍥鹃」锛氳绠椾袱绔潗鏍囷紝鎸夌姸鎬佸尯鍒嗙潃鑹?
        /// 锛堟甯?缁裤€侀攣瀹?姗欍€佺鐢?灏侀棴=绾級锛岃鍒掕矾寰勪笂鐨勮竟鍔犵矖銆?
        /// </summary>
        private static MapAreaViewItem CreateAreaItem(ContractMap.MapAreaDto area)
        {
            var color = area.Properties.TryGetValue("Color", out var configuredColor) && !string.IsNullOrWhiteSpace(configuredColor)
                ? NormalizeColor(configuredColor)
                : AreaColor(area.AreaType);
            var points = area.BoundaryPoints.Count > 0
                ? area.BoundaryPoints
                : Array.Empty<ContractMap.MapPointDto>();

            return new MapAreaViewItem
            {
                AreaId = area.AreaId,
                AreaName = BuildAreaLabel(area),
                AreaType = ToChineseAreaType(area.AreaType),
                PointsText = string.Join(" ", points.Select(point => $"{point.X:0.##},{point.Y:0.##}")),
                Fill = ToAlphaColor(color, "26"),
                Stroke = color,
                Opacity = area.Enabled ? 1.0 : 0.35,
                LabelLeft = points.Count > 0 ? points.Average(point => point.X) : 0,
                LabelTop = points.Count > 0 ? points.Average(point => point.Y) : 0
            };
        }

        private static string BuildAreaLabel(ContractMap.MapAreaDto area)
        {
            var name = string.IsNullOrWhiteSpace(area.AreaName) ? area.AreaId : area.AreaName;
            if (area.Properties.TryGetValue("Label", out var label) && !string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            if (area.Properties.TryGetValue("SpeedLimit", out var speedLimit) && !string.IsNullOrWhiteSpace(speedLimit))
            {
                return $"{name}\nMax {speedLimit} m/s";
            }

            if (area.Properties.TryGetValue("NavigationMedium", out var medium) && !string.IsNullOrWhiteSpace(medium))
            {
                return $"{name}\n{medium}";
            }

            return area.Properties.TryGetValue("Capacity", out var capacity) && !string.IsNullOrWhiteSpace(capacity)
                ? $"{name}\n0/{capacity}"
                : name;
        }

        private static string GetAreaDisplayName(ContractMap.MapAreaDto area)
        {
            return string.IsNullOrWhiteSpace(area.AreaName) ? area.AreaId : area.AreaName;
        }

        private static string ToChineseAreaType(ContractMap.MapAreaType areaType) => areaType switch
        {
            ContractMap.MapAreaType.WorkArea => "作业区域",
            ContractMap.MapAreaType.ChargingArea => "充电区域",
            ContractMap.MapAreaType.WaitingArea => "待机区域",
            ContractMap.MapAreaType.NarrowArea => "限速/狭窄区域",
            ContractMap.MapAreaType.IntersectionArea => "路口/互斥区域",
            ContractMap.MapAreaType.BlockedArea => "禁行/维护区域",
            _ => "普通区域"
        };

        private static string ToChineseNodeType(MapNodeType nodeType) => nodeType switch
        {
            MapNodeType.Pickup => "取货点",
            MapNodeType.Dropoff => "放货点",
            MapNodeType.Charge => "充电点",
            MapNodeType.Station => "工位",
            MapNodeType.Waiting => "待机点",
            MapNodeType.Intersection => "路口点",
            MapNodeType.Door => "门禁点",
            MapNodeType.Elevator => "电梯点",
            _ => "普通点"
        };

        private static string ToChineseRobotState(RobotState state) => state switch
        {
            RobotState.Running => "运行中",
            RobotState.Idle => "空闲",
            RobotState.Fault => "故障",
            RobotState.Offline => "离线",
            _ => state.ToString()
        };

        private static MapEdgeViewItem CreateEdgeItem(
            MapEdge edge,
            MapNode fromNode,
            MapNode toNode,
            bool isPlanned)
        {
            // 鐘舵€佺潃鑹诧細绂佺敤鎴栧皝闂啋绾紙鐗╃悊闃绘柇锛夛紝閿佸畾鈫掓锛堜复鏃剁鎺э級锛屾甯糕啋缁?
            var isBlocked = !edge.IsEnabled || edge.Direction == EdgeDirection.Closed;
            string stroke;
            double opacity;
            if (isBlocked)
            {
                stroke = "#FF4500";
                opacity = 0.9;
            }
            else
            {
                stroke = "#00FF7F";
                opacity = 0.55;
            }

            return new MapEdgeViewItem
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                IsBidirectional = edge.Direction == EdgeDirection.Bidirectional,
                Direction = edge.Direction,
                IsEnabled = edge.IsEnabled,
                IsLocked = false,
                MaxSpeed = edge.MaxSpeed,
                X1 = fromNode.Position.X,
                Y1 = fromNode.Position.Y,
                X2 = toNode.Position.X,
                Y2 = toNode.Position.Y,
                Stroke = stroke,
                StrokeThickness = isPlanned ? 3 : 2,
                Opacity = opacity
            };
        }

        /// <summary>
        /// 涓轰竴鏉¤竟鐢熸垚鏂瑰悜绠ご锛?锛?褰㈡姌绾匡級锛?
        /// 鍙屽悜鈫掑湪涓偣涓や晶鍚勪竴涓弽鍚戠澶达紱浠呮鍚戔啋涓€涓寚鍚戠粓鐐圭殑绠ご锛?
        /// 浠呭弽鍚戔啋涓€涓寚鍚戣捣鐐圭殑绠ご锛涘皝闂啋涓嶇敾绠ご銆傜澶撮鑹茶窡闅忚竟鐨勭姸鎬佽壊銆?
        /// </summary>
        private static IEnumerable<MapEdgeArrowViewItem> CreateEdgeArrows(
            MapEdge edge,
            MapNode fromNode,
            MapNode toNode,
            string stroke)
        {
            if (edge.Direction == EdgeDirection.Closed)
            {
                yield break;
            }

            double ax = fromNode.Position.X, ay = fromNode.Position.Y;
            double bx = toNode.Position.X, by = toNode.Position.Y;
            double dx = bx - ax, dy = by - ay;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6)
            {
                yield break; // 鑷幆鎴栭噸鍚堢偣锛屾棤鏂瑰悜鍙█
            }

            // 鍗曚綅鏂瑰悜涓庡崟浣嶆硶鍚?
            double ux = dx / len, uy = dy / len;
            double nx = -uy, ny = ux;
            double mx = (ax + bx) / 2, my = (ay + by) / 2; // 杈逛腑鐐?

            const double wing = 7;   // 绠考娌胯竟鏂瑰悜鍥為€€闀垮害
            const double half = 5;   // 绠考妯悜鍗婂
            const double gap = 6;    // 鍙屽悜绠ご閿欏紑闂磋窛

            if (edge.Direction == EdgeDirection.Bidirectional)
            {
                // 鎸囧悜缁堢偣鐨勭澶达紙鐣ュ亸鍚戠粓鐐逛竴渚э級
                yield return BuildArrow(mx + ux * gap, my + uy * gap, ux, uy, nx, ny, wing, half, stroke);
                // 鎸囧悜璧风偣鐨勭澶达紙鍙嶆柟鍚戯紝鐣ュ亸鍚戣捣鐐逛竴渚э級
                yield return BuildArrow(mx - ux * gap, my - uy * gap, -ux, -uy, nx, ny, wing, half, stroke);
            }
            else if (edge.Direction == EdgeDirection.ReverseOnly)
            {
                yield return BuildArrow(mx, my, -ux, -uy, nx, ny, wing, half, stroke);
            }
            else // ForwardOnly
            {
                yield return BuildArrow(mx, my, ux, uy, nx, ny, wing, half, stroke);
            }
        }

        /// <summary>
        /// 鍦ㄧ粰瀹氱灏栦綅缃?(tipX,tipY) 鍜屾寚鍚?(ux,uy) 澶勬瀯寤轰竴涓?"锛? 褰㈢澶达細
        /// 涓ょ考绔偣 = 绠皷娌垮弽鏂瑰悜鍥為€€ wing 鍚庯紝鍒嗗埆娌挎硶鍚?(nx,ny) 鍋忕Щ 卤half銆?
        /// </summary>
        private static MapEdgeArrowViewItem BuildArrow(
            double tipX, double tipY,
            double ux, double uy,
            double nx, double ny,
            double wing, double half,
            string stroke)
        {
            double baseX = tipX - ux * wing, baseY = tipY - uy * wing;
            return new MapEdgeArrowViewItem
            {
                X1 = baseX + nx * half,
                Y1 = baseY + ny * half,
                Xc = tipX,
                Yc = tipY,
                X2 = baseX - nx * half,
                Y2 = baseY - ny * half,
                Stroke = stroke
            };
        }

        /// <summary>
        /// 鐢遍鍩熻妭鐐规ā鍨嬫瀯寤虹敾甯冭妭鐐硅鍥鹃」锛氳绠楃粯鍒跺潗鏍囦笌鏍囩鍋忕Щ锛屾寜鑺傜偣绫诲瀷濉壊锛?
        /// 绂佺敤鑺傜偣缁熶竴鐏版樉锛岃鍒掕矾寰勪笂鐨勮妭鐐规弿杈归珮浜€?
        /// </summary>
        private static MapNodeViewItem CreateNodeItem(MapNode node, bool isOnPlannedPath, string areaName)
        {
            // 绂佺敤鑺傜偣鐏版樉锛屾槑鏄惧尯鍒簬鍚敤鑺傜偣锛涘惎鐢ㄨ妭鐐规寜绫诲瀷鐫€鑹?
            var fill = !node.IsEnabled
                ? "#556070"
                : node.NodeType switch
                {
                    MapNodeType.Pickup => "#1E90FF",
                    MapNodeType.Dropoff => "#00BFFF",
                    MapNodeType.Charge => "#9370DB",
                    MapNodeType.Intersection => "#00FF7F",
                    _ => "#9FB7CC"
                };

            string stroke;
            double strokeThickness;
            if (isOnPlannedPath)
            {
                stroke = "#FFD700";
                strokeThickness = 3;
            }
            else if (!node.IsEnabled)
            {
                stroke = "#3A4452";
                strokeThickness = 1;
            }
            else
            {
                stroke = "#D8F3FF";
                strokeThickness = 1;
            }

            return new MapNodeViewItem
            {
                NodeId = node.NodeId,
                Name = node.Name,
                NodeCode = string.IsNullOrWhiteSpace(node.NodeCode) ? node.NodeId : node.NodeCode,
                LabelText = string.IsNullOrWhiteSpace(node.Name) ? node.NodeId : node.Name,
                DisplayText = string.IsNullOrWhiteSpace(node.Name)
                    ? node.NodeId
                    : $"{node.NodeId} ({node.Name})",
                NodeType = ToChineseNodeType(node.NodeType),
                AreaCode = node.AreaCode,
                AreaName = string.IsNullOrWhiteSpace(areaName) ? node.AreaCode : $"{areaName} ({node.AreaCode})",
                IsEnabled = node.IsEnabled,
                X = node.Position.X,
                Y = node.Position.Y,
                CanvasLeft = node.Position.X - 9,
                CanvasTop = node.Position.Y - 9,
                LabelLeft = node.Position.X + 12,
                LabelTop = node.Position.Y - 12,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = strokeThickness,
                Opacity = node.IsEnabled ? 1.0 : 0.5
            };
        }

        /// <summary>
        /// 閬嶅巻鎵€鏈夎溅杈嗭紝灏嗚兘鍖归厤鍒板湴鍥捐妭鐐圭殑杞﹁締鐢熸垚浣嶇疆鏍囪锛涢€変腑杞﹁締鐢ㄩ噾鑹茬獊鍑猴紝鍏朵綑鎸夌姸鎬佺潃鑹层€?
        /// </summary>
        private IEnumerable<VehicleMapViewItem> CreateVehiclePositions(IReadOnlyList<MapNode> nodes, string? selectedVehicleId)
        {
            foreach (var snapshot in _vehicleStateStore.GetAllVehicles())
            {
                var item = CreateVehicleItem(snapshot, nodes, selectedVehicleId);
                if (item is not null)
                {
                    yield return item;
                }
            }
        }

        private VehicleMapViewItem? CreateVehicleItem(
            VehicleStatusSnapshot snapshot,
            IReadOnlyList<MapNode> nodes,
            string? selectedVehicleId)
        {
            var node = ResolveNode(snapshot.Location, nodes, _aliases);
            var position = VehiclePositionResolver.Resolve(snapshot, node?.Position);
            if (position is null)
            {
                return null;
            }

            var stateText = ToChineseRobotState(snapshot.State);
            return new VehicleMapViewItem
            {
                VehicleId = snapshot.VehicleId,
                CurrentTaskId = snapshot.CurrentTaskId ?? string.Empty,
                State = stateText,
                Location = snapshot.Location,
                X = position.X,
                Y = position.Y,
                CanvasLeft = position.X + 12,
                CanvasTop = position.Y - 34,
                Heading = position.Heading,
                BatteryLevel = snapshot.BatteryLevel,
                StateText = stateText,
                Fill = VehicleFill(snapshot.State,
                    string.Equals(snapshot.VehicleId, selectedVehicleId, StringComparison.OrdinalIgnoreCase))
            };
        }

        private static string VehicleFill(RobotState state, bool isSelected) => isSelected
            ? "#FFD700"
            : state switch
            {
                RobotState.Running => "#32CD32",
                RobotState.Fault => "#FF4500",
                RobotState.Idle => "#00BFFF",
                RobotState.Offline => "#D3D3D3",
                _ => "#FFD700"
            };

        private static string AreaColor(ContractMap.MapAreaType areaType) => areaType switch
        {
            ContractMap.MapAreaType.WorkArea => "#2D8CFF",
            ContractMap.MapAreaType.ChargingArea => "#9B6DFF",
            ContractMap.MapAreaType.WaitingArea => "#00BFA6",
            ContractMap.MapAreaType.NarrowArea => "#FFB020",
            ContractMap.MapAreaType.IntersectionArea => "#32D583",
            ContractMap.MapAreaType.BlockedArea => "#FF4D4F",
            _ => "#5A7FA6"
        };

        private static string NormalizeColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                return "#5A7FA6";
            }

            color = color.Trim();
            return color.StartsWith("#", StringComparison.Ordinal) ? color : $"#{color}";
        }

        private static string ToAlphaColor(string color, string alpha)
        {
            var normalized = NormalizeColor(color);
            return normalized.Length == 7 ? $"#{alpha}{normalized[1..]}" : normalized;
        }

        /// <summary>
        /// 灏嗕竴涓綅缃瓧绗︿覆瑙ｆ瀽涓哄湴鍥捐妭鐐癸紝鎸変互涓嬩紭鍏堢骇鍖归厤锛?
        /// 鈶?鍚敤鐨勪綅缃埆鍚?鈫?鈶?鑺傜偣 ID/缂栫爜绮剧‘鍖归厤 鈫?鈶?鍚?"Charge" 鏃跺洖閫€鍒颁换涓€鍏呯數鑺傜偣 鈫?
        /// 鈶?瀵瑰舰濡?"A01" 鐨勪覆褰掍竴鍖栧悗鍖归厤锛堝幓鍓嶅闆跺苟澶у啓锛夆啋 鈶?鎸夐瀛楁瘝浣滀负鍖哄煙/鍓嶇紑鍏滃簳鍖归厤銆?
        /// 鏃犳硶瑙ｆ瀽鏃惰繑鍥?null銆?
        /// </summary>
        private static MapNode? ResolveNode(string? location, IReadOnlyList<MapNode> nodes, IReadOnlyList<MapLocationAlias> aliases)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            var alias = aliases.FirstOrDefault(a => 
                a.IsEnabled && string.Equals(a.AliasValue, location, StringComparison.OrdinalIgnoreCase));
            if (alias != null)
            {
                var mappedNode = nodes.FirstOrDefault(n => n.NodeId == alias.NodeId);
                if (mappedNode != null) return mappedNode;
            }

            var exact = nodes.FirstOrDefault(node =>
                string.Equals(node.NodeId, location, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.NodeCode, location, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }

            if (location.Contains("Charge", StringComparison.OrdinalIgnoreCase))
            {
                return nodes.FirstOrDefault(node => node.NodeType == MapNodeType.Charge);
            }

            var normalized = Regex.Match(location, @"^([A-Za-z]+)0*(\d+)");
            if (normalized.Success)
            {
                var nodeId = $"{normalized.Groups[1].Value.ToUpperInvariant()}{normalized.Groups[2].Value}";
                var node = nodes.FirstOrDefault(item =>
                    string.Equals(item.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.NodeCode, nodeId, StringComparison.OrdinalIgnoreCase));
                if (node is not null)
                {
                    return node;
                }
            }

            var areaCode = location[..1].ToUpperInvariant();
            return nodes.FirstOrDefault(node =>
                string.Equals(node.AreaCode, areaCode, StringComparison.OrdinalIgnoreCase)
                || node.NodeId.StartsWith(areaCode, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>鍦板浘杈圭殑鐢诲竷娓叉煋瑙嗗浘椤癸細鎼哄甫涓ょ鍧愭爣涓庢牱寮忥紙棰滆壊/绮楃粏/閫忔槑搴︼級銆?/summary>
    public class MapAreaViewItem
    {
        public string AreaId { get; set; } = string.Empty;

        public string AreaName { get; set; } = string.Empty;

        public string AreaType { get; set; } = string.Empty;

        public string PointsText { get; set; } = string.Empty;

        public string Fill { get; set; } = "#265A7FA6";

        public string Stroke { get; set; } = "#5A7FA6";

        public double Opacity { get; set; } = 1.0;

        public double LabelLeft { get; set; }

        public double LabelTop { get; set; }
    }

    public class MapEdgeViewItem
    {
        /// <summary>杈圭紪鍙枫€?/summary>
        public string EdgeId { get; set; } = string.Empty;

        /// <summary>璧峰鑺傜偣缂栧彿銆?/summary>
        public string FromNodeId { get; set; } = string.Empty;

        /// <summary>缁堟鑺傜偣缂栧彿銆?/summary>
        public string ToNodeId { get; set; } = string.Empty;

        /// <summary>鏄惁鍙屽悜閫氳銆?/summary>
        public bool IsBidirectional { get; set; }

        /// <summary>杈规柟鍚戙€?/summary>
        public EdgeDirection Direction { get; set; }

        /// <summary>鏄惁鍚敤锛堢鐢紳鐗╃悊闃绘柇锛夈€?/summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>鏄惁琚攣瀹氾紙涓存椂鍗犵敤/绠℃帶锛夈€?/summary>
        public bool IsLocked { get; set; }

        /// <summary>闄愰€燂紙m/s锛夈€?/summary>
        public double MaxSpeed { get; set; }

        /// <summary>璧风偣 X 鐢诲竷鍧愭爣銆?/summary>
        public double X1 { get; set; }

        /// <summary>璧风偣 Y 鐢诲竷鍧愭爣銆?/summary>
        public double Y1 { get; set; }

        /// <summary>缁堢偣 X 鐢诲竷鍧愭爣銆?/summary>
        public double X2 { get; set; }

        /// <summary>缁堢偣 Y 鐢诲竷鍧愭爣銆?/summary>
        public double Y2 { get; set; }

        /// <summary>绾挎潯棰滆壊锛堝崄鍏繘鍒惰壊鍊硷級銆?/summary>
        public string Stroke { get; set; } = "#00FF7F";

        /// <summary>绾挎潯绮楃粏銆?/summary>
        public double StrokeThickness { get; set; } = 2;

        /// <summary>绾挎潯閫忔槑搴︺€?/summary>
        public double Opacity { get; set; } = 0.6;
    }

    /// <summary>
    /// 杈规柟鍚戠澶寸殑鐢诲竷娓叉煋椤癸細鐢ㄤ竴娈典笁鐐规姌绾匡紙"锛?褰級琛ㄧず閫氳鏂瑰悜锛岀疆浜庤竟涓偣闄勮繎銆?
    /// </summary>
    public class MapEdgeArrowViewItem
    {
        /// <summary>绠ご涓€缈肩鐐?X銆?/summary>
        public double X1 { get; set; }

        /// <summary>绠ご涓€缈肩鐐?Y銆?/summary>
        public double Y1 { get; set; }

        /// <summary>绠皷 X锛堟寚鍚戦€氳鏂瑰悜锛夈€?/summary>
        public double Xc { get; set; }

        /// <summary>绠皷 Y锛堟寚鍚戦€氳鏂瑰悜锛夈€?/summary>
        public double Yc { get; set; }

        /// <summary>绠ご鍙︿竴缈肩鐐?X銆?/summary>
        public double X2 { get; set; }

        /// <summary>绠ご鍙︿竴缈肩鐐?Y銆?/summary>
        public double Y2 { get; set; }

        /// <summary>绠ご棰滆壊锛堣窡闅忔墍灞炶竟鐨勭姸鎬佽壊锛夈€?/summary>
        public string Stroke { get; set; } = "#00FF7F";

        /// <summary>绠ご绾垮銆?/summary>
        public double StrokeThickness { get; set; } = 2;

        /// <summary>绠ご閫忔槑搴︺€?/summary>
        public double Opacity { get; set; } = 0.85;

        /// <summary>渚?Polyline.Points 缁戝畾鐨勪笁鐐瑰瓧绗︿覆銆?/summary>
        public string PointsText => $"{X1},{Y1} {Xc},{Yc} {X2},{Y2}";
    }

    /// <summary>鍦板浘鑺傜偣鐨勭敾甯冩覆鏌撹鍥鹃」锛氭惡甯︾粯鍒跺潗鏍囥€佹爣绛惧亸绉讳笌鏍峰紡銆?/summary>
    public class MapNodeViewItem
    {
        /// <summary>鑺傜偣缂栧彿銆?/summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>鑺傜偣鍚嶇О銆?/summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>鑺傜偣缂栫爜锛堢己鐪佹椂鍥為€€涓鸿妭鐐圭紪鍙凤級銆?/summary>
        public string NodeCode { get; set; } = string.Empty;

        /// <summary>地图标签显示文本，优先展示中文名称。</summary>
        public string LabelText { get; set; } = string.Empty;

        /// <summary>点位选择控件显示文本：节点ID（中文名称）。</summary>
        public string DisplayText { get; set; } = string.Empty;

        /// <summary>鑺傜偣绫诲瀷鏂囨湰锛堝彇鑷?<c>MapNodeType</c>锛夈€?/summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>鎵€灞炲尯鍩熺紪鐮併€?/summary>
        public string AreaCode { get; set; } = string.Empty;

        /// <summary>区域中文名称和区域编号。</summary>
        public string AreaName { get; set; } = string.Empty;

        /// <summary>鏄惁鍚敤銆?/summary>
        public bool IsEnabled { get; set; }

        /// <summary>鑺傜偣閫昏緫 X 鍧愭爣銆?/summary>
        public double X { get; set; }

        /// <summary>鑺傜偣閫昏緫 Y 鍧愭爣銆?/summary>
        public double Y { get; set; }

        /// <summary>鍦嗙偣缁樺埗宸︿笂瑙?X锛堝凡鎸夊崐寰勫亸绉伙級銆?/summary>
        public double CanvasLeft { get; set; }

        /// <summary>鍦嗙偣缁樺埗宸︿笂瑙?Y锛堝凡鎸夊崐寰勫亸绉伙級銆?/summary>
        public double CanvasTop { get; set; }

        /// <summary>鏍囩宸︿晶 X 鍋忕Щ銆?/summary>
        public double LabelLeft { get; set; }

        /// <summary>鏍囩椤堕儴 Y 鍋忕Щ銆?/summary>
        public double LabelTop { get; set; }

        /// <summary>濉厖棰滆壊锛堟寜鑺傜偣绫诲瀷鍖哄垎锛夈€?/summary>
        public string Fill { get; set; } = "#00BFFF";

        /// <summary>鎻忚竟棰滆壊锛堣矾寰勪笂鑺傜偣楂樹寒涓洪噾鑹诧級銆?/summary>
        public string Stroke { get; set; } = "#D8F3FF";

        /// <summary>鎻忚竟绮楃粏銆?/summary>
        public double StrokeThickness { get; set; } = 1;

        /// <summary>鏁翠綋閫忔槑搴︼紙绂佺敤鑺傜偣鍗婇€忔槑锛夈€?/summary>
        public double Opacity { get; set; } = 1.0;
    }

    /// <summary>杞﹁締鍦ㄥ湴鍥句笂鐨勪綅缃爣璁拌鍥鹃」锛氭惡甯﹀潗鏍囥€佺姸鎬佷笌鏍峰紡銆?/summary>
    public class VehicleMapViewItem : BindableBase
    {
        private string _vehicleId = string.Empty;
        private string _currentTaskId = string.Empty;
        private string _state = string.Empty;
        private string _location = string.Empty;
        private double _x;
        private double _y;
        private double _canvasLeft;
        private double _canvasTop;
        private double _heading;
        private double _batteryLevel;
        private string _stateText = string.Empty;
        private string _fill = "#00BFFF";

        /// <summary>杞﹁締缂栧彿銆?/summary>
        public string VehicleId { get => _vehicleId; set => SetProperty(ref _vehicleId, value); }

        /// <summary>褰撳墠浠诲姟缂栧彿銆?/summary>
        public string CurrentTaskId { get => _currentTaskId; set => SetProperty(ref _currentTaskId, value); }

        /// <summary>鐘舵€佹枃鏈€?/summary>
        public string State { get => _state; set => SetProperty(ref _state, value); }

        /// <summary>褰撳墠浣嶇疆锛堝師濮嬩綅缃覆锛夈€?/summary>
        public string Location { get => _location; set => SetProperty(ref _location, value); }

        /// <summary>杞﹁締閫昏緫 X 鍧愭爣銆?/summary>
        public double X { get => _x; set => SetProperty(ref _x, value); }

        /// <summary>杞﹁締閫昏緫 Y 鍧愭爣銆?/summary>
        public double Y { get => _y; set => SetProperty(ref _y, value); }

        /// <summary>鏍囪缁樺埗宸︿笂瑙?X锛堝凡鍋忕Щ锛夈€?/summary>
        public double CanvasLeft { get => _canvasLeft; set => SetProperty(ref _canvasLeft, value); }

        /// <summary>鏍囪缁樺埗宸︿笂瑙?Y锛堝凡鍋忕Щ锛夈€?/summary>
        public double CanvasTop { get => _canvasTop; set => SetProperty(ref _canvasTop, value); }

        public double Heading { get => _heading; set => SetProperty(ref _heading, value); }

        /// <summary>鐢甸噺鐧惧垎姣斻€?/summary>
        public double BatteryLevel { get => _batteryLevel; set => SetProperty(ref _batteryLevel, value); }

        /// <summary>鐘舵€佹樉绀烘枃鏈€?/summary>
        public string StateText { get => _stateText; set => SetProperty(ref _stateText, value); }

        /// <summary>濉厖棰滆壊锛堥€変腑涓洪噾鑹诧紝鍚﹀垯鎸夌姸鎬佺潃鑹诧級銆?/summary>
        public string Fill { get => _fill; set => SetProperty(ref _fill, value); }

        public void UpdateFrom(VehicleMapViewItem source)
        {
            CurrentTaskId = source.CurrentTaskId;
            State = source.State;
            Location = source.Location;
            X = source.X;
            Y = source.Y;
            CanvasLeft = source.CanvasLeft;
            CanvasTop = source.CanvasTop;
            Heading = source.Heading;
            BatteryLevel = source.BatteryLevel;
            StateText = source.StateText;
            Fill = source.Fill;
        }
    }
}

