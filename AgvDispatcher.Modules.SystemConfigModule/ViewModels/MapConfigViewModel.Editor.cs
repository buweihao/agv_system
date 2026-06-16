using System.Collections.ObjectModel;
using System.Globalization;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.SystemConfigModule.Editor;
using Prism.Commands;

namespace AgvDispatcher.Modules.SystemConfigModule.ViewModels
{
    /// <summary>
    /// MapConfigViewModel 的可视化编辑器分部：承载画布视图项投影、双向同步、撤销重做、
    /// 充电桩集合、地图比例尺设置与编辑器命令。表格编辑路径（另一分部文件）保持不变，
    /// 两者共享同一组领域集合 <see cref="Nodes"/>/<see cref="Edges"/>。
    /// </summary>
    public partial class MapConfigViewModel
    {
        // 编辑器交互模式
        private bool _isConnectMode;
        private bool _isPlaceStationMode;
        private bool _isGridSnapEnabled = true;

        /// <summary>对齐网格步长（画布像素）。</summary>
        public const double GridStep = 20;

        private readonly UndoRedoStack _undoRedo = new();

        /// <summary>画布节点项集合（由 <see cref="Nodes"/> 投影，双向同步）。</summary>
        public ObservableCollection<EditorNodeVm> EditorNodes { get; } = new();

        /// <summary>画布边项集合（由 <see cref="Edges"/> 投影，端点随节点联动）。</summary>
        public ObservableCollection<EditorEdgeVm> EditorEdges { get; } = new();

        /// <summary>充电桩领域集合（编辑器与表格共用）。</summary>
        public ObservableCollection<ChargeStation> Stations { get; } = new();

        /// <summary>画布充电桩项集合（由 <see cref="Stations"/> 投影，跟随绑定节点）。</summary>
        public ObservableCollection<EditorStationVm> EditorStations { get; } = new();

        /// <summary>地图比例尺/原点设置（"米/像素"显示换算）。</summary>
        public MapSettings Settings { get; } = new();

        // ---- 编辑器命令 ----

        /// <summary>切换"连线模式"（开启后在画布上从一个节点拖到另一个节点建边）。</summary>
        public DelegateCommand ToggleConnectModeCommand { get; private set; } = null!;

        /// <summary>切换"放置充电桩"模式（开启后点击某 Charge 节点为其新增充电桩）。</summary>
        public DelegateCommand TogglePlaceStationModeCommand { get; private set; } = null!;

        /// <summary>切换对齐网格吸附。</summary>
        public DelegateCommand ToggleGridSnapCommand { get; private set; } = null!;

        /// <summary>切换角落读数"米/像素"显示。</summary>
        public DelegateCommand ToggleUnitCommand { get; private set; } = null!;

        /// <summary>撤销上一步编辑。</summary>
        public DelegateCommand UndoCommand { get; private set; } = null!;

        /// <summary>重做被撤销的编辑。</summary>
        public DelegateCommand RedoCommand { get; private set; } = null!;

        /// <summary>在画布中心新增一个节点。</summary>
        public DelegateCommand AddNodeAtCenterCommand { get; private set; } = null!;

        /// <summary>删除当前选中的画布对象（节点/边/充电桩）。</summary>
        public DelegateCommand DeleteSelectionCommand { get; private set; } = null!;

        /// <summary>整图保存：前置校验通过后，把节点/边/别名/充电桩批量落库。</summary>
        public DelegateCommand SaveAllCommand { get; private set; } = null!;

        /// <summary>是否处于连线模式（供界面高亮工具按钮）。</summary>
        public bool IsConnectMode
        {
            get => _isConnectMode;
            set
            {
                if (SetProperty(ref _isConnectMode, value) && value)
                {
                    IsPlaceStationMode = false;
                }
            }
        }

        /// <summary>是否处于放置充电桩模式。</summary>
        public bool IsPlaceStationMode
        {
            get => _isPlaceStationMode;
            set
            {
                if (SetProperty(ref _isPlaceStationMode, value) && value)
                {
                    IsConnectMode = false;
                }
            }
        }

        /// <summary>是否启用对齐网格吸附。</summary>
        public bool IsGridSnapEnabled
        {
            get => _isGridSnapEnabled;
            set => SetProperty(ref _isGridSnapEnabled, value);
        }

        private EditorNodeVm? _selectedEditorNode;
        /// <summary>当前选中的画布节点（点击/拖拽选中）。同步选中底层表格节点。</summary>
        public EditorNodeVm? SelectedEditorNode
        {
            get => _selectedEditorNode;
            set
            {
                if (SetProperty(ref _selectedEditorNode, value))
                {
                    UpdateSelectionVisuals();
                    if (value is not null)
                    {
                        SelectedNode = value.Model;
                        SelectedEditorEdge = null;
                        SelectedEditorStation = null;
                    }
                    RaisePropertyChanged(nameof(HasSelection));
                    DeleteSelectionCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private EditorEdgeVm? _selectedEditorEdge;
        /// <summary>当前选中的画布边。</summary>
        public EditorEdgeVm? SelectedEditorEdge
        {
            get => _selectedEditorEdge;
            set
            {
                if (SetProperty(ref _selectedEditorEdge, value))
                {
                    UpdateSelectionVisuals();
                    if (value is not null)
                    {
                        SelectedEdge = value.Model;
                        SelectedEditorNode = null;
                        SelectedEditorStation = null;
                    }
                    RaisePropertyChanged(nameof(HasSelection));
                    DeleteSelectionCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private EditorStationVm? _selectedEditorStation;
        /// <summary>当前选中的画布充电桩。</summary>
        public EditorStationVm? SelectedEditorStation
        {
            get => _selectedEditorStation;
            set
            {
                if (SetProperty(ref _selectedEditorStation, value))
                {
                    UpdateSelectionVisuals();
                    if (value is not null)
                    {
                        SelectedEditorNode = null;
                        SelectedEditorEdge = null;
                    }
                    RaisePropertyChanged(nameof(HasSelection));
                    DeleteSelectionCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>是否有任意画布对象被选中。</summary>
        public bool HasSelection => SelectedEditorNode is not null || SelectedEditorEdge is not null || SelectedEditorStation is not null;

        private string _coordReadout = string.Empty;
        /// <summary>画布角落坐标读数（随光标移动更新，按米/像素显示）。</summary>
        public string CoordReadout
        {
            get => _coordReadout;
            set => SetProperty(ref _coordReadout, value);
        }

        /// <summary>绑定与命令初始化（在构造函数中调用）。</summary>
        private void InitializeEditorCommands()
        {
            ToggleConnectModeCommand = new DelegateCommand(() => IsConnectMode = !IsConnectMode);
            TogglePlaceStationModeCommand = new DelegateCommand(() => IsPlaceStationMode = !IsPlaceStationMode);
            ToggleGridSnapCommand = new DelegateCommand(() => IsGridSnapEnabled = !IsGridSnapEnabled);
            ToggleUnitCommand = new DelegateCommand(() => Settings.ShowInMeters = !Settings.ShowInMeters);
            UndoCommand = new DelegateCommand(() => { _undoRedo.Undo(); RaiseUndoRedoState(); }, () => _undoRedo.CanUndo);
            RedoCommand = new DelegateCommand(() => { _undoRedo.Redo(); RaiseUndoRedoState(); }, () => _undoRedo.CanRedo);
            AddNodeAtCenterCommand = new DelegateCommand(AddNodeAtCenter);
            DeleteSelectionCommand = new DelegateCommand(DeleteSelection, () => HasSelection).ObservesProperty(() => HasSelection);
            SaveAllCommand = new DelegateCommand(SaveAll);

            LoadMapSettings();
        }

        private void RaiseUndoRedoState()
        {
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 由领域集合重建画布投影：清理旧项（含事件解绑），按 Nodes/Edges/Stations 重新构建，
        /// 并修复选中引用。在每次 LoadData 后调用。
        /// </summary>
        private void RebuildEditorProjections()
        {
            // 解绑旧的边/充电桩订阅，避免内存泄漏
            foreach (var e in EditorEdges) e.Dispose();
            foreach (var s in EditorStations) s.Dispose();

            EditorNodes.Clear();
            EditorEdges.Clear();
            EditorStations.Clear();

            var nodeVmMap = new Dictionary<string, EditorNodeVm>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in Nodes)
            {
                var vm = new EditorNodeVm(node);
                EditorNodes.Add(vm);
                nodeVmMap[node.NodeId] = vm;
            }

            foreach (var edge in Edges)
            {
                if (nodeVmMap.TryGetValue(edge.FromNodeId, out var from)
                    && nodeVmMap.TryGetValue(edge.ToNodeId, out var to))
                {
                    EditorEdges.Add(new EditorEdgeVm(edge, from, to));
                }
            }

            foreach (var station in Stations)
            {
                nodeVmMap.TryGetValue(station.NodeId, out var node);
                EditorStations.Add(new EditorStationVm(station, node));
            }

            // 重载后清空选中与撤销栈，避免悬挂引用
            _selectedEditorNode = null;
            _selectedEditorEdge = null;
            _selectedEditorStation = null;
            RaisePropertyChanged(nameof(SelectedEditorNode));
            RaisePropertyChanged(nameof(SelectedEditorEdge));
            RaisePropertyChanged(nameof(SelectedEditorStation));
            RaisePropertyChanged(nameof(HasSelection));
            _undoRedo.Clear();
            RaiseUndoRedoState();
        }

        /// <summary>同步刷新所有画布对象的选中态描边。</summary>
        private void UpdateSelectionVisuals()
        {
            foreach (var n in EditorNodes) n.IsSelected = ReferenceEquals(n, _selectedEditorNode);
            foreach (var e in EditorEdges) e.IsSelected = ReferenceEquals(e, _selectedEditorEdge);
            foreach (var s in EditorStations) s.IsSelected = ReferenceEquals(s, _selectedEditorStation);
        }

        /// <summary>
        /// 把一个画布坐标按网格步长吸附（仅当启用对齐网格）。
        /// </summary>
        /// <param name="value">原始坐标。</param>
        public double Snap(double value)
            => IsGridSnapEnabled ? Math.Round(value / GridStep) * GridStep : value;

        /// <summary>更新角落坐标读数为给定画布坐标对应的文本。</summary>
        /// <param name="canvasX">画布像素 X。</param>
        /// <param name="canvasY">画布像素 Y。</param>
        public void UpdateReadout(double canvasX, double canvasY)
        {
            CoordReadout = Settings.Format(canvasX, canvasY);
        }

        /// <summary>
        /// 记录一次节点移动（拖拽完成时由视图调用），以便撤销重做。移动已实时生效，此处仅登记。
        /// </summary>
        /// <param name="node">被移动的节点画布项。</param>
        /// <param name="oldX">移动前 X。</param>
        /// <param name="oldY">移动前 Y。</param>
        /// <param name="newX">移动后 X。</param>
        /// <param name="newY">移动后 Y。</param>
        public void RecordMove(EditorNodeVm node, double oldX, double oldY, double newX, double newY)
        {
            if (oldX == newX && oldY == newY) return;
            _undoRedo.Push(new EditorAction(
                $"移动节点 {node.NodeId}",
                redo: () => { node.X = newX; node.Y = newY; },
                undo: () => { node.X = oldX; node.Y = oldY; }));
            RaiseUndoRedoState();
        }

        /// <summary>
        /// 在画布上从起点节点连一条边到终点节点（连线模式松手时由视图调用）。
        /// 自动生成边 ID、默认双向、限速 1.0，登记撤销步。
        /// </summary>
        /// <param name="from">起点节点画布项。</param>
        /// <param name="to">终点节点画布项。</param>
        public void ConnectNodes(EditorNodeVm from, EditorNodeVm to)
        {
            if (ReferenceEquals(from, to)) return;
            // 避免重复边（同起止，忽略方向）
            if (Edges.Any(e =>
                (string.Equals(e.FromNodeId, from.NodeId, StringComparison.OrdinalIgnoreCase) && string.Equals(e.ToNodeId, to.NodeId, StringComparison.OrdinalIgnoreCase))
                || (string.Equals(e.FromNodeId, to.NodeId, StringComparison.OrdinalIgnoreCase) && string.Equals(e.ToNodeId, from.NodeId, StringComparison.OrdinalIgnoreCase))))
            {
                return;
            }

            var edgeId = GenerateEdgeId();
            var model = new MapEdge
            {
                EdgeId = edgeId,
                MapId = "MAIN",
                FromNodeId = from.NodeId,
                ToNodeId = to.NodeId,
                Direction = EdgeDirection.Bidirectional,
                MaxSpeed = 1.0,
                IsEnabled = true
            };
            var edgeVm = new EditorEdgeVm(model, from, to);

            _undoRedo.Do(new EditorAction(
                $"新增边 {edgeId}",
                redo: () => { if (!Edges.Contains(model)) Edges.Add(model); if (!EditorEdges.Contains(edgeVm)) EditorEdges.Add(edgeVm); RaisePropertyChanged(nameof(TotalEdges)); },
                undo: () => { Edges.Remove(model); EditorEdges.Remove(edgeVm); edgeVm.Dispose(); RaisePropertyChanged(nameof(TotalEdges)); }));
            RaiseUndoRedoState();
            SelectedEditorEdge = edgeVm;
        }

        /// <summary>
        /// 为一个 Charge 节点新增充电桩（放置充电桩模式点击节点时由视图调用）。
        /// 仅允许绑定启用的 Charge 类型节点；自动生成桩 ID 并登记撤销步。
        /// </summary>
        /// <param name="node">被点击的节点画布项。</param>
        public void PlaceStationOnNode(EditorNodeVm node)
        {
            if (node.Model.NodeType != MapNodeType.Charge || !node.Model.IsEnabled)
            {
                System.Windows.MessageBox.Show("充电桩只能放置在启用的 Charge 类型节点上。",
                    "无法放置", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var stationId = GenerateStationId();
            var model = new ChargeStation
            {
                StationId = stationId,
                StationCode = stationId,
                Name = $"充电桩 {stationId}",
                NodeId = node.NodeId,
                AreaCode = node.Model.AreaCode,
                Position = new MapPosition { MapId = "MAIN", NodeId = node.NodeId, X = node.X, Y = node.Y },
                IsEnabled = true,
                RatedPowerKw = 3.3,
                MaxQueueCount = 1
            };
            var stationVm = new EditorStationVm(model, node);

            _undoRedo.Do(new EditorAction(
                $"新增充电桩 {stationId}",
                redo: () => { if (!Stations.Contains(model)) Stations.Add(model); if (!EditorStations.Contains(stationVm)) EditorStations.Add(stationVm); },
                undo: () => { Stations.Remove(model); EditorStations.Remove(stationVm); stationVm.Dispose(); }));
            RaiseUndoRedoState();
            SelectedEditorStation = stationVm;
        }

        /// <summary>在画布中心新增一个默认节点并登记撤销步。</summary>
        private void AddNodeAtCenter()
        {
            var number = NextNodeNumber();
            var id = $"N{number:000}";
            var model = new MapNode
            {
                NodeId = id,
                MapId = "MAIN",
                NodeCode = id,
                Name = $"节点 {number:000}",
                NodeType = MapNodeType.Normal,
                Position = new MapPosition { MapId = "MAIN", NodeId = id, X = Snap(430), Y = Snap(290) },
                IsEnabled = true
            };
            var vm = new EditorNodeVm(model);

            _undoRedo.Do(new EditorAction(
                $"新增节点 {id}",
                redo: () => { if (!Nodes.Contains(model)) Nodes.Add(model); if (!EditorNodes.Contains(vm)) EditorNodes.Add(vm); RaisePropertyChanged(nameof(TotalNodes)); },
                undo: () => { Nodes.Remove(model); EditorNodes.Remove(vm); RaisePropertyChanged(nameof(TotalNodes)); }));
            RaiseUndoRedoState();
            SelectedEditorNode = vm;
        }

        /// <summary>删除当前选中的画布对象（节点会先检查是否被边引用），登记撤销步。</summary>
        private void DeleteSelection()
        {
            if (SelectedEditorNode is { } nodeVm)
            {
                var nodeId = nodeVm.Model.NodeId;
                if (Edges.Any(e => string.Equals(e.FromNodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(e.ToNodeId, nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    System.Windows.MessageBox.Show($"节点 {nodeId} 被路径引用，无法删除。请先删除相关边。",
                        "删除失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                var model = nodeVm.Model;
                _undoRedo.Do(new EditorAction(
                    $"删除节点 {nodeId}",
                    redo: () => { Nodes.Remove(model); EditorNodes.Remove(nodeVm); RaisePropertyChanged(nameof(TotalNodes)); },
                    undo: () => { Nodes.Add(model); EditorNodes.Add(nodeVm); RaisePropertyChanged(nameof(TotalNodes)); }));
                SelectedEditorNode = null;
            }
            else if (SelectedEditorEdge is { } edgeVm)
            {
                var model = edgeVm.Model;
                _undoRedo.Do(new EditorAction(
                    $"删除边 {model.EdgeId}",
                    redo: () => { Edges.Remove(model); EditorEdges.Remove(edgeVm); RaisePropertyChanged(nameof(TotalEdges)); },
                    undo: () => { Edges.Add(model); EditorEdges.Add(edgeVm); RaisePropertyChanged(nameof(TotalEdges)); }));
                SelectedEditorEdge = null;
            }
            else if (SelectedEditorStation is { } stationVm)
            {
                var model = stationVm.Model;
                _undoRedo.Do(new EditorAction(
                    $"删除充电桩 {model.StationId}",
                    redo: () => { Stations.Remove(model); EditorStations.Remove(stationVm); },
                    undo: () => { Stations.Add(model); EditorStations.Add(stationVm); }));
                SelectedEditorStation = null;
            }
            RaiseUndoRedoState();
        }

        /// <summary>
        /// 整图保存：前置校验（复用 <see cref="PassesPreSaveValidation"/>）通过后，
        /// 把当前内存中的节点/边/别名/充电桩与库做差异化同步（删除已移除项、保存现存项），
        /// 并持久化比例尺设置。最后刷新。
        /// </summary>
        private void SaveAll()
        {
            if (!PassesPreSaveValidation()) return;

            try
            {
                // 节点：删除库里已不存在的，保存现存的
                var dbNodes = _mapRepository.GetNodesAsync().GetAwaiter().GetResult();
                foreach (var n in dbNodes)
                {
                    if (Nodes.All(x => !string.Equals(x.NodeId, n.NodeId, StringComparison.OrdinalIgnoreCase)))
                        _mapRepository.DeleteNodeAsync(n.NodeId).GetAwaiter().GetResult();
                }
                foreach (var n in Nodes) _mapRepository.SaveNodeAsync(n).GetAwaiter().GetResult();

                // 边
                var dbEdges = _mapRepository.GetEdgesAsync().GetAwaiter().GetResult();
                foreach (var e in dbEdges)
                {
                    if (Edges.All(x => !string.Equals(x.EdgeId, e.EdgeId, StringComparison.OrdinalIgnoreCase)))
                        _mapRepository.DeleteEdgeAsync(e.EdgeId).GetAwaiter().GetResult();
                }
                foreach (var e in Edges) _mapRepository.SaveEdgeAsync(e).GetAwaiter().GetResult();

                // 别名
                var dbAliases = _aliasRepo.GetAllAsync().GetAwaiter().GetResult();
                foreach (var a in dbAliases)
                {
                    if (Aliases.All(x => x.AliasId != a.AliasId))
                        _aliasRepo.DeleteAsync(a.AliasId).GetAwaiter().GetResult();
                }
                foreach (var a in Aliases) _aliasRepo.SaveAsync(a).GetAwaiter().GetResult();

                // 充电桩
                var dbStations = _chargeStationRepo.GetAllAsync().GetAwaiter().GetResult();
                foreach (var s in dbStations)
                {
                    if (Stations.All(x => !string.Equals(x.StationId, s.StationId, StringComparison.OrdinalIgnoreCase)))
                        _chargeStationRepo.DeleteAsync(s.StationId).GetAwaiter().GetResult();
                }
                foreach (var s in Stations) _chargeStationRepo.SaveAsync(s).GetAwaiter().GetResult();

                SaveMapSettings();

                LoadData();
                System.Windows.MessageBox.Show("地图已保存。", "成功",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // ---- ID 生成与计数辅助 ----

        private int NextNodeNumber()
        {
            var max = 0;
            foreach (var n in Nodes)
            {
                if (n.NodeId.Length > 1 && (n.NodeId[0] == 'N' || n.NodeId[0] == 'n')
                    && int.TryParse(n.NodeId.AsSpan(1), out var num) && num > max)
                {
                    max = num;
                }
            }
            return max + 1;
        }

        private string GenerateEdgeId()
        {
            var i = Edges.Count + 1;
            string id;
            do { id = $"E{i:000}"; i++; }
            while (Edges.Any(e => string.Equals(e.EdgeId, id, StringComparison.OrdinalIgnoreCase)));
            return id;
        }

        private string GenerateStationId()
        {
            var i = Stations.Count + 1;
            string id;
            do { id = $"CS{i:000}"; i++; }
            while (Stations.Any(s => string.Equals(s.StationId, id, StringComparison.OrdinalIgnoreCase)));
            return id;
        }

        // ---- 比例尺持久化（系统参数仓储 key/value）----

        private const string KeyPixelsPerMeter = "Map.PixelsPerMeter";
        private const string KeyOriginX = "Map.OriginX";
        private const string KeyOriginY = "Map.OriginY";

        /// <summary>从系统参数仓储读取比例尺/原点；缺省则保留默认值。</summary>
        private void LoadMapSettings()
        {
            try
            {
                var all = _parameterRepo.GetAllAsync().GetAwaiter().GetResult();
                foreach (var p in all)
                {
                    if (string.Equals(p.ParamKey, KeyPixelsPerMeter, StringComparison.OrdinalIgnoreCase)
                        && double.TryParse(p.ParamValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var ppm))
                        Settings.PixelsPerMeter = ppm;
                    else if (string.Equals(p.ParamKey, KeyOriginX, StringComparison.OrdinalIgnoreCase)
                        && double.TryParse(p.ParamValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var ox))
                        Settings.OriginX = ox;
                    else if (string.Equals(p.ParamKey, KeyOriginY, StringComparison.OrdinalIgnoreCase)
                        && double.TryParse(p.ParamValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var oy))
                        Settings.OriginY = oy;
                }
            }
            catch
            {
                // 读取失败时静默使用默认值，不阻塞编辑器加载
            }
        }

        /// <summary>把当前比例尺/原点写回系统参数仓储。</summary>
        private void SaveMapSettings()
        {
            SaveParam(KeyPixelsPerMeter, "每米像素数", Settings.PixelsPerMeter);
            SaveParam(KeyOriginX, "地图原点像素X", Settings.OriginX);
            SaveParam(KeyOriginY, "地图原点像素Y", Settings.OriginY);
        }

        private void SaveParam(string key, string name, double value)
        {
            _parameterRepo.SaveAsync(new ParameterConfig
            {
                ParamKey = key,
                ParamName = name,
                ParamValue = value.ToString(CultureInfo.InvariantCulture),
                DataType = "double",
                Description = "地图编辑器比例尺设置"
            }).GetAwaiter().GetResult();
        }
    }
}
