using System.Collections.ObjectModel;
using System.Collections.Generic;
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
        private bool _isAddNodeMode;
        private bool _isGridSnapEnabled = true;
        private AreaDrawMode _areaDrawMode = AreaDrawMode.None;

        /// <summary>对齐网格步长（画布像素）。</summary>
        public const double GridStep = 20;

        private readonly UndoRedoStack _undoRedo = new();

        /// <summary>画布节点项集合（由 <see cref="Nodes"/> 投影，双向同步）。</summary>
        public ObservableCollection<EditorNodeVm> EditorNodes { get; } = new();

        /// <summary>画布边项集合（由 <see cref="Edges"/> 投影，端点随节点联动）。</summary>
        public ObservableCollection<EditorEdgeVm> EditorEdges { get; } = new();

        public ObservableCollection<EditorAreaVm> EditorAreas { get; } = new();

        public IReadOnlyList<string> AreaColorOptions { get; } =
        [
            "#2D8CFF",
            "#00BFA6",
            "#9B6DFF",
            "#FFB020",
            "#FF4D4F",
            "#32D583",
            "#5A7FA6",
            "#00BFFF",
            "#FFD700",
            "#FF7A45"
        ];

        /// <summary>充电桩领域集合（编辑器与表格共用）。</summary>
        public ObservableCollection<ChargeStation> Stations { get; } = new();

        /// <summary>画布充电桩项集合（由 <see cref="Stations"/> 投影，跟随绑定节点）。</summary>
        public ObservableCollection<EditorStationVm> EditorStations { get; } = new();

        /// <summary>地图比例尺/原点设置（"米/像素"显示换算）。</summary>
        public MapSettings Settings { get; } = new();

        private readonly HashSet<string> _bulkHighlightedNodeIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _bulkHighlightedEdgeIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _previewNodeIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _previewEdgeIds = new(StringComparer.OrdinalIgnoreCase);

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

        /// <summary>切换节点放置模式：开启后点击画布空白处新增节点。</summary>
        public DelegateCommand AddNodeAtCenterCommand { get; private set; } = null!;

        public DelegateCommand AddAreaCommand { get; private set; } = null!;

        public DelegateCommand DeleteRectangleCommand { get; private set; } = null!;

        public DelegateCommand DrawRectangleAreaCommand { get; private set; } = null!;

        public DelegateCommand DrawPolygonAreaCommand { get; private set; } = null!;

        public DelegateCommand EncloseElementsAreaCommand { get; private set; } = null!;

        public DelegateCommand<string> SetSelectedAreaColorCommand { get; private set; } = null!;

        /// <summary>删除当前选中的画布对象（节点/边/充电桩）。</summary>
        public DelegateCommand DeleteSelectionCommand { get; private set; } = null!;

        /// <summary>交换当前选中路线的起点/终点，只修改静态地图边定义。</summary>
        public DelegateCommand ReverseSelectedEdgeCommand { get; private set; } = null!;

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
                    IsAddNodeMode = false;
                    AreaDrawMode = AreaDrawMode.None;
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
                    IsAddNodeMode = false;
                    AreaDrawMode = AreaDrawMode.None;
                }
            }
        }

        public bool IsAddNodeMode
        {
            get => _isAddNodeMode;
            set
            {
                if (SetProperty(ref _isAddNodeMode, value) && value)
                {
                    IsConnectMode = false;
                    IsPlaceStationMode = false;
                    AreaDrawMode = AreaDrawMode.None;
                }
            }
        }

        /// <summary>是否启用对齐网格吸附。</summary>
        public bool IsGridSnapEnabled
        {
            get => _isGridSnapEnabled;
            set => SetProperty(ref _isGridSnapEnabled, value);
        }

        public AreaDrawMode AreaDrawMode
        {
            get => _areaDrawMode;
            set
            {
                if (SetProperty(ref _areaDrawMode, value))
                {
                    RaisePropertyChanged(nameof(IsRectangleAreaMode));
                    RaisePropertyChanged(nameof(IsPolygonAreaMode));
                    RaisePropertyChanged(nameof(IsEncloseElementsAreaMode));
                    RaisePropertyChanged(nameof(IsDeleteElementsMode));
                    if (value != AreaDrawMode.None)
                    {
                        IsConnectMode = false;
                        IsPlaceStationMode = false;
                        IsAddNodeMode = false;
                    }
                }
            }
        }

        public bool IsRectangleAreaMode => AreaDrawMode == AreaDrawMode.Rectangle;

        public bool IsPolygonAreaMode => AreaDrawMode == AreaDrawMode.Polygon;

        public bool IsEncloseElementsAreaMode => AreaDrawMode == AreaDrawMode.EncloseElements;

        public bool IsDeleteElementsMode => AreaDrawMode == AreaDrawMode.DeleteElements;

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
                        SelectedEditorArea = null;
                    }
                    RaisePropertyChanged(nameof(HasSelection));
                    DeleteSelectionCommand.RaiseCanExecuteChanged();
                    ReverseSelectedEdgeCommand.RaiseCanExecuteChanged();
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
                        SelectedEditorArea = null;
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
                        SelectedEditorArea = null;
                    }
                    RaisePropertyChanged(nameof(HasSelection));
                    DeleteSelectionCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>是否有任意画布对象被选中。</summary>
        private EditorAreaVm? _selectedEditorArea;
        public EditorAreaVm? SelectedEditorArea
        {
            get => _selectedEditorArea;
            set
            {
                if (SetProperty(ref _selectedEditorArea, value))
                {
                    UpdateSelectionVisuals();
                    if (value is not null)
                    {
                        SelectedEditorNode = null;
                        SelectedEditorEdge = null;
                        SelectedEditorStation = null;
                    }
                    RaisePropertyChanged(nameof(HasSelection));
                    DeleteSelectionCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HasSelection => SelectedEditorNode is not null || SelectedEditorEdge is not null || SelectedEditorStation is not null || SelectedEditorArea is not null;

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
            TogglePlaceStationModeCommand = new DelegateCommand(TogglePlaceStationMode);
            ToggleGridSnapCommand = new DelegateCommand(() => IsGridSnapEnabled = !IsGridSnapEnabled);
            ToggleUnitCommand = new DelegateCommand(() => Settings.ShowInMeters = !Settings.ShowInMeters);
            UndoCommand = new DelegateCommand(() => { _undoRedo.Undo(); RaiseUndoRedoState(); }, () => _undoRedo.CanUndo);
            RedoCommand = new DelegateCommand(() => { _undoRedo.Redo(); RaiseUndoRedoState(); }, () => _undoRedo.CanRedo);
            AddNodeAtCenterCommand = new DelegateCommand(() => IsAddNodeMode = !IsAddNodeMode);
            AddAreaCommand = new DelegateCommand(AddArea);
            DrawRectangleAreaCommand = new DelegateCommand(() => ToggleAreaDrawMode(AreaDrawMode.Rectangle));
            DrawPolygonAreaCommand = new DelegateCommand(() => ToggleAreaDrawMode(AreaDrawMode.Polygon));
            EncloseElementsAreaCommand = new DelegateCommand(() => ToggleAreaDrawMode(AreaDrawMode.EncloseElements));
            DeleteRectangleCommand = new DelegateCommand(() => ToggleAreaDrawMode(AreaDrawMode.DeleteElements));
            SetSelectedAreaColorCommand = new DelegateCommand<string>(SetSelectedAreaColor);
            DeleteSelectionCommand = new DelegateCommand(DeleteSelection, () => HasSelection).ObservesProperty(() => HasSelection);
            ReverseSelectedEdgeCommand = new DelegateCommand(ReverseSelectedEdge, () => SelectedEditorEdge is not null)
                .ObservesProperty(() => SelectedEditorEdge);
            SaveAllCommand = new DelegateCommand(SaveAll);

            LoadMapSettings();
        }

        private void ToggleAreaDrawMode(AreaDrawMode mode)
        {
            AreaDrawMode = AreaDrawMode == mode ? AreaDrawMode.None : mode;
            ClearBulkHighlights();
        }

        private void TogglePlaceStationMode()
        {
            if (EditorNodes.Count == 0)
            {
                AddChargeNodeAtCenter();
                return;
            }

            if (SelectedEditorNode is { } selectedNode)
            {
                selectedNode.NodeType = MapNodeType.Charge;
                selectedNode.IsEnabled = true;
                PlaceStationOnNode(selectedNode);
                return;
            }

            IsPlaceStationMode = !IsPlaceStationMode;
        }

        private void SetSelectedAreaColor(string color)
        {
            if (SelectedEditorArea is null || string.IsNullOrWhiteSpace(color))
            {
                return;
            }

            SelectedEditorArea.Color = color;
        }

        private void ReverseSelectedEdge()
        {
            if (SelectedEditorEdge is not { } edgeVm)
            {
                return;
            }

            _undoRedo.Do(new EditorAction(
                $"反向路线 {edgeVm.EdgeId}",
                redo: edgeVm.ReverseEndpoints,
                undo: edgeVm.ReverseEndpoints));
            RaiseUndoRedoState();
            SelectedEdge = edgeVm.Model;
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
            _selectedEditorArea = null;
            RaisePropertyChanged(nameof(SelectedEditorNode));
            RaisePropertyChanged(nameof(SelectedEditorEdge));
            RaisePropertyChanged(nameof(SelectedEditorStation));
            RaisePropertyChanged(nameof(SelectedEditorArea));
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
            foreach (var a in EditorAreas) a.IsSelected = ReferenceEquals(a, _selectedEditorArea);
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
            var distance = CalculateDistanceInMeters(from.X, from.Y, to.X, to.Y);
            var model = new MapEdge
            {
                EdgeId = edgeId,
                MapId = "MAIN",
                FromNodeId = from.NodeId,
                ToNodeId = to.NodeId,
                Direction = EdgeDirection.Bidirectional,
                Length = distance,
                Cost = Math.Max(1, (int)Math.Round(distance)),
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

        private double CalculateDistanceInMeters(double fromX, double fromY, double toX, double toY)
        {
            var dx = toX - fromX;
            var dy = toY - fromY;
            var pixelDistance = Math.Sqrt(dx * dx + dy * dy);
            var pixelsPerMeter = Settings.PixelsPerMeter <= 0 ? 1 : Settings.PixelsPerMeter;
            return pixelDistance / pixelsPerMeter;
        }

        /// <summary>
        /// 为一个 Charge 节点新增充电桩（放置充电桩模式点击节点时由视图调用）。
        /// 仅允许绑定启用的 Charge 类型节点；自动生成桩 ID 并登记撤销步。
        /// </summary>
        /// <param name="node">被点击的节点画布项。</param>
        public void PlaceStationOnNode(EditorNodeVm node)
        {
            var existing = EditorStations.FirstOrDefault(station =>
                string.Equals(station.Model.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                SelectedEditorStation = existing;
                return;
            }

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
            AddNodeAt(430, 290);
        }

        public EditorNodeVm? AddNodeAt(double x, double y, MapNodeType nodeType = MapNodeType.Normal)
        {
            var snappedX = Snap(x);
            var snappedY = Snap(y);
            var minimumSpacing = EditorNodeVm.NodeDiameter;
            var overlappingNode = EditorNodes.FirstOrDefault(node =>
            {
                var deltaX = node.X - snappedX;
                var deltaY = node.Y - snappedY;
                return deltaX * deltaX + deltaY * deltaY <= minimumSpacing * minimumSpacing;
            });

            if (overlappingNode is not null)
            {
                SelectedEditorNode = overlappingNode;
                CoordReadout = $"该位置已有节点 {overlappingNode.NodeId}，未重复创建";
                return null;
            }

            var number = NextNodeNumber();
            var id = $"N{number:000}";
            var model = new MapNode
            {
                NodeId = id,
                MapId = "MAIN",
                NodeCode = id,
                Name = nodeType == MapNodeType.Charge ? $"充电点 {number:000}" : $"节点 {number:000}",
                NodeType = nodeType,
                Position = new MapPosition { MapId = "MAIN", NodeId = id, X = snappedX, Y = snappedY },
                IsEnabled = true
            };
            var vm = new EditorNodeVm(model);

            _undoRedo.Do(new EditorAction(
                $"新增节点 {id}",
                redo: () => { if (!Nodes.Contains(model)) Nodes.Add(model); if (!EditorNodes.Contains(vm)) EditorNodes.Add(vm); RaisePropertyChanged(nameof(TotalNodes)); },
                undo: () => { Nodes.Remove(model); EditorNodes.Remove(vm); RaisePropertyChanged(nameof(TotalNodes)); }));
            RaiseUndoRedoState();
            SelectedEditorNode = vm;
            return vm;
        }

        private void AddChargeNodeAtCenter()
        {
            var nodeVm = AddNodeAt(430, 290, MapNodeType.Charge);
            if (nodeVm is null) return;

            SelectedEditorNode = nodeVm;
            PlaceStationOnNode(nodeVm);
        }

        /// <summary>删除当前选中的画布对象（节点会先检查是否被边引用），登记撤销步。</summary>
        public void ApplyAreas(IEnumerable<AgvDispatcher.Core.Contracts.Map.MapAreaDto> areas)
        {
            EditorAreas.Clear();
            foreach (var area in areas)
            {
                EditorAreas.Add(new EditorAreaVm(area));
            }

            RaisePropertyChanged(nameof(TotalAreas));
        }

        private void AddArea()
        {
            var number = EditorAreas.Count + 1;
            string areaId;
            do { areaId = $"AREA-{number:00}"; number++; }
            while (EditorAreas.Any(area => string.Equals(area.AreaId, areaId, StringComparison.OrdinalIgnoreCase)));

            var area = new AgvDispatcher.Core.Contracts.Map.MapAreaDto
            {
                AreaId = areaId,
                AreaName = $"区域 {areaId}",
                AreaType = AgvDispatcher.Core.Contracts.Map.MapAreaType.Normal,
                BoundaryPoints =
                [
                    new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = 120, Y = 120 },
                    new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = 360, Y = 120 },
                    new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = 360, Y = 300 },
                    new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = 120, Y = 300 }
                ],
                Enabled = true,
                Properties = new Dictionary<string, string> { ["Color"] = "#2D8CFF" }
            };
            var vm = new EditorAreaVm(area);

            _undoRedo.Do(new EditorAction(
                $"新增区域 {areaId}",
                redo: () => { if (!EditorAreas.Contains(vm)) EditorAreas.Add(vm); RaisePropertyChanged(nameof(TotalAreas)); },
                undo: () => { EditorAreas.Remove(vm); RaisePropertyChanged(nameof(TotalAreas)); }));
            RaiseUndoRedoState();
            SelectedEditorArea = vm;
        }

        public EditorAreaVm CreateAreaFromRectangle(double x1, double y1, double x2, double y2)
        {
            var left = Snap(Math.Min(x1, x2));
            var top = Snap(Math.Min(y1, y2));
            var right = Snap(Math.Max(x1, x2));
            var bottom = Snap(Math.Max(y1, y2));
            if (Math.Abs(right - left) < 8 || Math.Abs(bottom - top) < 8)
            {
                return SelectedEditorArea ?? AddAreaCore();
            }

            var points = new[]
            {
                new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = left, Y = top },
                new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = right, Y = top },
                new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = right, Y = bottom },
                new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = left, Y = bottom }
            };
            return AddAreaFromPoints(points, "\u77e9\u5f62\u533a\u57df");
        }

        public EditorAreaVm? CreateAreaFromPolygon(IEnumerable<AgvDispatcher.Core.Contracts.Map.MapPointDto> points)
        {
            var snappedPoints = points
                .Select(point => new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = Snap(point.X), Y = Snap(point.Y) })
                .ToList();
            if (snappedPoints.Count < 3)
            {
                return null;
            }

            return AddAreaFromPoints(snappedPoints, "\u591a\u8fb9\u5f62\u533a\u57df");
        }

        public void PreviewEnclosedElements(double x1, double y1, double x2, double y2)
        {
            var left = Math.Min(x1, x2);
            var top = Math.Min(y1, y2);
            var right = Math.Max(x1, x2);
            var bottom = Math.Max(y1, y2);

            PreviewElementsInRectangle(left, top, right, bottom);
        }

        public void PreviewDeleteElements(double x1, double y1, double x2, double y2)
        {
            PreviewEnclosedElements(x1, y1, x2, y2);
        }

        public void DeleteElementsInRectangle(double x1, double y1, double x2, double y2)
        {
            var left = Math.Min(x1, x2);
            var top = Math.Min(y1, y2);
            var right = Math.Max(x1, x2);
            var bottom = Math.Max(y1, y2);
            if (Math.Abs(right - left) < 8 || Math.Abs(bottom - top) < 8)
            {
                ClearBulkHighlights();
                return;
            }

            var nodes = EditorNodes
                .Where(node => IsInside(node.X, node.Y, left, top, right, bottom))
                .ToList();
            var nodeIds = nodes.Select(node => node.NodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var edges = EditorEdges
                .Where(edge =>
                    nodeIds.Contains(edge.Model.FromNodeId)
                    || nodeIds.Contains(edge.Model.ToNodeId)
                    || (IsInside(edge.X1, edge.Y1, left, top, right, bottom) && IsInside(edge.X2, edge.Y2, left, top, right, bottom)))
                .Distinct()
                .ToList();
            var stations = EditorStations
                .Where(station =>
                    nodeIds.Contains(station.Model.NodeId)
                    || IsInside(station.Model.Position.X, station.Model.Position.Y, left, top, right, bottom))
                .ToList();
            var aliases = Aliases
                .Where(alias => nodeIds.Contains(alias.NodeId))
                .ToList();
            var areas = EditorAreas
                .Where(area =>
                {
                    var points = EditorAreaVm.ParseBoundary(area.BoundaryText);
                    return points.Count > 0 && points.All(point => IsInside(point.X, point.Y, left, top, right, bottom));
                })
                .ToList();

            if (nodes.Count == 0 && edges.Count == 0 && stations.Count == 0 && aliases.Count == 0 && areas.Count == 0)
            {
                ClearBulkHighlights();
                return;
            }

            _undoRedo.Do(new EditorAction(
                $"框选删除 {nodes.Count} 点/{edges.Count} 边/{areas.Count} 区",
                redo: () =>
                {
                    foreach (var edge in edges) { Edges.Remove(edge.Model); EditorEdges.Remove(edge); }
                    foreach (var station in stations) { Stations.Remove(station.Model); EditorStations.Remove(station); }
                    foreach (var alias in aliases) Aliases.Remove(alias);
                    foreach (var node in nodes) { Nodes.Remove(node.Model); EditorNodes.Remove(node); }
                    foreach (var area in areas) EditorAreas.Remove(area);
                    RaiseEditorCounts();
                },
                undo: () =>
                {
                    foreach (var area in areas) if (!EditorAreas.Contains(area)) EditorAreas.Add(area);
                    foreach (var node in nodes) { if (!Nodes.Contains(node.Model)) Nodes.Add(node.Model); if (!EditorNodes.Contains(node)) EditorNodes.Add(node); }
                    foreach (var edge in edges) { if (!Edges.Contains(edge.Model)) Edges.Add(edge.Model); if (!EditorEdges.Contains(edge)) EditorEdges.Add(edge); }
                    foreach (var station in stations) { if (!Stations.Contains(station.Model)) Stations.Add(station.Model); if (!EditorStations.Contains(station)) EditorStations.Add(station); }
                    foreach (var alias in aliases) if (!Aliases.Contains(alias)) Aliases.Add(alias);
                    RaiseEditorCounts();
                }));

            SelectedEditorNode = null;
            SelectedEditorEdge = null;
            SelectedEditorStation = null;
            SelectedEditorArea = null;
            RaiseUndoRedoState();
            ClearBulkHighlights();
        }

        public void AssignEnclosedElementsToArea(double x1, double y1, double x2, double y2)
        {
            var left = Snap(Math.Min(x1, x2));
            var top = Snap(Math.Min(y1, y2));
            var right = Snap(Math.Max(x1, x2));
            var bottom = Snap(Math.Max(y1, y2));
            if (Math.Abs(right - left) < 8 || Math.Abs(bottom - top) < 8)
            {
                ClearBulkHighlights();
                return;
            }

            var area = SelectedEditorArea ?? AddAreaFromPoints(new[]
            {
                new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = left, Y = top },
                new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = right, Y = top },
                new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = right, Y = bottom },
                new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = left, Y = bottom }
            }, "\u6846\u9009\u5143\u7d20\u533a");

            var nodes = EditorNodes
                .Where(node => IsInside(node.X, node.Y, left, top, right, bottom))
                .ToList();
            var edges = EditorEdges
                .Where(edge => IsInside(edge.X1, edge.Y1, left, top, right, bottom) && IsInside(edge.X2, edge.Y2, left, top, right, bottom))
                .ToList();
            var oldNodeAreas = nodes.ToDictionary(node => node, node => node.AreaCode);
            var oldEdgeAreas = edges.ToDictionary(edge => edge, edge => edge.Model.AreaCode);
            var oldBoundary = area.BoundaryText;
            var newBoundary = string.Join("; ", new[]
            {
                $"{left:0.##},{top:0.##}",
                $"{right:0.##},{top:0.##}",
                $"{right:0.##},{bottom:0.##}",
                $"{left:0.##},{bottom:0.##}"
            });

            _undoRedo.Do(new EditorAction(
                $"\u6846\u9009\u5143\u7d20\u5f52\u5165\u533a\u57df {area.AreaId}",
                redo: () =>
                {
                    foreach (var node in nodes) node.AreaCode = area.AreaId;
                    foreach (var edge in edges) edge.Model.AreaCode = area.AreaId;
                    area.BoundaryText = newBoundary;
                },
                undo: () =>
                {
                    foreach (var pair in oldNodeAreas) pair.Key.AreaCode = pair.Value;
                    foreach (var pair in oldEdgeAreas) pair.Key.Model.AreaCode = pair.Value;
                    area.BoundaryText = oldBoundary;
                }));
            RaiseUndoRedoState();
            ClearBulkHighlights();
            SelectedEditorArea = area;
        }

        public void ClearBulkHighlights()
        {
            if (_bulkHighlightedNodeIds.Count == 0 && _bulkHighlightedEdgeIds.Count == 0)
            {
                return;
            }

            foreach (var node in EditorNodes)
            {
                if (_bulkHighlightedNodeIds.Contains(node.NodeId))
                {
                    node.IsBulkHighlighted = false;
                }
            }

            foreach (var edge in EditorEdges)
            {
                if (_bulkHighlightedEdgeIds.Contains(edge.EdgeId))
                {
                    edge.IsBulkHighlighted = false;
                }
            }

            _bulkHighlightedNodeIds.Clear();
            _bulkHighlightedEdgeIds.Clear();
        }

        private void ApplyBulkHighlights(HashSet<string> nextNodeIds, HashSet<string> nextEdgeIds)
        {
            if (_bulkHighlightedNodeIds.SetEquals(nextNodeIds)
                && _bulkHighlightedEdgeIds.SetEquals(nextEdgeIds))
            {
                return;
            }

            foreach (var node in EditorNodes)
            {
                var shouldHighlight = nextNodeIds.Contains(node.NodeId);
                if (shouldHighlight != _bulkHighlightedNodeIds.Contains(node.NodeId))
                {
                    node.IsBulkHighlighted = shouldHighlight;
                }
            }

            foreach (var edge in EditorEdges)
            {
                var shouldHighlight = nextEdgeIds.Contains(edge.EdgeId);
                if (shouldHighlight != _bulkHighlightedEdgeIds.Contains(edge.EdgeId))
                {
                    edge.IsBulkHighlighted = shouldHighlight;
                }
            }

            _bulkHighlightedNodeIds.Clear();
            foreach (var nodeId in nextNodeIds)
            {
                _bulkHighlightedNodeIds.Add(nodeId);
            }

            _bulkHighlightedEdgeIds.Clear();
            foreach (var edgeId in nextEdgeIds)
            {
                _bulkHighlightedEdgeIds.Add(edgeId);
            }
        }

        private void PreviewElementsInRectangle(double left, double top, double right, double bottom)
        {
            _previewNodeIds.Clear();
            _previewEdgeIds.Clear();

            foreach (var node in EditorNodes)
            {
                if (IsInside(node.X, node.Y, left, top, right, bottom))
                {
                    _previewNodeIds.Add(node.NodeId);
                }
            }

            foreach (var edge in EditorEdges)
            {
                if (LineIntersectsRect(edge.X1, edge.Y1, edge.X2, edge.Y2, left, top, right, bottom))
                {
                    _previewEdgeIds.Add(edge.EdgeId);
                }
            }

            ApplyBulkHighlights(_previewNodeIds, _previewEdgeIds);
        }

        private EditorAreaVm AddAreaCore()
        {
            var number = EditorAreas.Count + 1;
            string areaId;
            do { areaId = $"AREA-{number:00}"; number++; }
            while (EditorAreas.Any(area => string.Equals(area.AreaId, areaId, StringComparison.OrdinalIgnoreCase)));

            var area = new AgvDispatcher.Core.Contracts.Map.MapAreaDto
            {
                AreaId = areaId,
                AreaName = $"\u533a\u57df {areaId}",
                AreaType = AgvDispatcher.Core.Contracts.Map.MapAreaType.Normal,
                BoundaryPoints =
                [
                    new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = 120, Y = 120 },
                    new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = 360, Y = 120 },
                    new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = 360, Y = 300 },
                    new AgvDispatcher.Core.Contracts.Map.MapPointDto { X = 120, Y = 300 }
                ],
                Enabled = true,
                Properties = new Dictionary<string, string> { ["Color"] = "#2D8CFF" }
            };
            return AddAreaDto(area);
        }

        private EditorAreaVm AddAreaFromPoints(IReadOnlyList<AgvDispatcher.Core.Contracts.Map.MapPointDto> points, string namePrefix)
        {
            var area = new AgvDispatcher.Core.Contracts.Map.MapAreaDto
            {
                AreaId = NextAreaId(),
                AreaName = $"{namePrefix} {EditorAreas.Count + 1:00}",
                AreaType = AgvDispatcher.Core.Contracts.Map.MapAreaType.Normal,
                BoundaryPoints = points,
                Enabled = true,
                Properties = new Dictionary<string, string> { ["Color"] = AreaColorOptions[EditorAreas.Count % AreaColorOptions.Count] }
            };
            return AddAreaDto(area);
        }

        private EditorAreaVm AddAreaDto(AgvDispatcher.Core.Contracts.Map.MapAreaDto area)
        {
            var vm = new EditorAreaVm(area);
            _undoRedo.Do(new EditorAction(
                $"\u65b0\u589e\u533a\u57df {vm.AreaId}",
                redo: () => { if (!EditorAreas.Contains(vm)) EditorAreas.Add(vm); RaisePropertyChanged(nameof(TotalAreas)); },
                undo: () => { EditorAreas.Remove(vm); RaisePropertyChanged(nameof(TotalAreas)); }));
            RaiseUndoRedoState();
            SelectedEditorArea = vm;
            return vm;
        }

        private string NextAreaId()
        {
            var number = EditorAreas.Count + 1;
            string areaId;
            do { areaId = $"AREA-{number:00}"; number++; }
            while (EditorAreas.Any(area => string.Equals(area.AreaId, areaId, StringComparison.OrdinalIgnoreCase)));
            return areaId;
        }

        private static bool IsInside(double x, double y, double left, double top, double right, double bottom)
            => x >= left && x <= right && y >= top && y <= bottom;

        private static bool LineIntersectsRect(double x1, double y1, double x2, double y2, double left, double top, double right, double bottom)
        {
            if (IsInside(x1, y1, left, top, right, bottom) || IsInside(x2, y2, left, top, right, bottom))
            {
                return true;
            }

            return LinesIntersect(x1, y1, x2, y2, left, top, right, top)
                || LinesIntersect(x1, y1, x2, y2, right, top, right, bottom)
                || LinesIntersect(x1, y1, x2, y2, right, bottom, left, bottom)
                || LinesIntersect(x1, y1, x2, y2, left, bottom, left, top);
        }

        private static bool LinesIntersect(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
        {
            static double Cross(double x1, double y1, double x2, double y2) => x1 * y2 - y1 * x2;
            static bool InRange(double value, double a, double b) => value >= Math.Min(a, b) - 0.0001 && value <= Math.Max(a, b) + 0.0001;
            static bool OnSegment(double px, double py, double sx, double sy, double ex, double ey)
                => InRange(px, sx, ex) && InRange(py, sy, ey);

            var abx = bx - ax;
            var aby = by - ay;
            var acx = cx - ax;
            var acy = cy - ay;
            var adx = dx - ax;
            var ady = dy - ay;
            var abToC = Cross(abx, aby, acx, acy);
            var abToD = Cross(abx, aby, adx, ady);

            var cdx = dx - cx;
            var cdy = dy - cy;
            var cax = ax - cx;
            var cay = ay - cy;
            var cbx = bx - cx;
            var cby = by - cy;
            var cdToA = Cross(cdx, cdy, cax, cay);
            var cdToB = Cross(cdx, cdy, cbx, cby);

            if (Math.Abs(abToC) < 0.0001 && OnSegment(cx, cy, ax, ay, bx, by)) return true;
            if (Math.Abs(abToD) < 0.0001 && OnSegment(dx, dy, ax, ay, bx, by)) return true;
            if (Math.Abs(cdToA) < 0.0001 && OnSegment(ax, ay, cx, cy, dx, dy)) return true;
            if (Math.Abs(cdToB) < 0.0001 && OnSegment(bx, by, cx, cy, dx, dy)) return true;

            return abToC * abToD < 0 && cdToA * cdToB < 0;
        }

        private void DeleteSelection()
        {
            if (SelectedEditorNode is { } nodeVm)
            {
                var nodeId = nodeVm.Model.NodeId;
                if (false && Edges.Any(e => string.Equals(e.FromNodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(e.ToNodeId, nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    System.Windows.MessageBox.Show($"节点 {nodeId} 被路径引用，无法删除。请先删除相关边。",
                        "删除失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                var model = nodeVm.Model;
                var relatedEdges = EditorEdges
                    .Where(edge => string.Equals(edge.Model.FromNodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(edge.Model.ToNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var relatedStations = EditorStations
                    .Where(station => string.Equals(station.Model.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var relatedAliases = Aliases
                    .Where(alias => string.Equals(alias.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                _undoRedo.Do(new EditorAction(
                    $"删除节点 {nodeId}",
                    redo: () =>
                    {
                        foreach (var edge in relatedEdges) { Edges.Remove(edge.Model); EditorEdges.Remove(edge); }
                        foreach (var station in relatedStations) { Stations.Remove(station.Model); EditorStations.Remove(station); }
                        foreach (var alias in relatedAliases) Aliases.Remove(alias);
                        Nodes.Remove(model);
                        EditorNodes.Remove(nodeVm);
                        RaisePropertyChanged(nameof(TotalNodes));
                        RaisePropertyChanged(nameof(TotalEdges));
                        RaisePropertyChanged(nameof(TotalAliases));
                    },
                    undo: () =>
                    {
                        Nodes.Add(model);
                        EditorNodes.Add(nodeVm);
                        foreach (var edge in relatedEdges) { if (!Edges.Contains(edge.Model)) Edges.Add(edge.Model); if (!EditorEdges.Contains(edge)) EditorEdges.Add(edge); }
                        foreach (var station in relatedStations) { if (!Stations.Contains(station.Model)) Stations.Add(station.Model); if (!EditorStations.Contains(station)) EditorStations.Add(station); }
                        foreach (var alias in relatedAliases) { if (!Aliases.Contains(alias)) Aliases.Add(alias); }
                        RaisePropertyChanged(nameof(TotalNodes));
                        RaisePropertyChanged(nameof(TotalEdges));
                        RaisePropertyChanged(nameof(TotalAliases));
                    }));
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
            else if (SelectedEditorArea is { } areaVm)
            {
                var areaId = areaVm.AreaId;
                var areaNodes = Nodes
                    .Where(node => string.Equals(node.AreaCode, areaId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var areaEdges = Edges
                    .Where(edge => string.Equals(edge.AreaCode, areaId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                _undoRedo.Do(new EditorAction(
                    $"删除区域 {areaId}",
                    redo: () =>
                    {
                        foreach (var node in areaNodes) node.AreaCode = string.Empty;
                        foreach (var edge in areaEdges) edge.AreaCode = string.Empty;
                        EditorAreas.Remove(areaVm);
                        RaisePropertyChanged(nameof(TotalAreas));
                    },
                    undo: () =>
                    {
                        if (!EditorAreas.Contains(areaVm)) EditorAreas.Add(areaVm);
                        foreach (var node in areaNodes) node.AreaCode = areaId;
                        foreach (var edge in areaEdges) edge.AreaCode = areaId;
                        RaisePropertyChanged(nameof(TotalAreas));
                    }));
                SelectedEditorArea = null;
            }
            RaiseUndoRedoState();
        }

        /// <summary>
        /// 整图保存草稿：只把当前画布投影保存为地图草稿，不切换当前运行地图。
        /// </summary>
        private void SaveAll()
        {
            if (!PassesPreSaveValidation()) return;

            try
            {
                SaveMapSettings();
                SaveCurrentDraft();
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

    public enum AreaDrawMode
    {
        None,
        Rectangle,
        Polygon,
        EncloseElements,
        DeleteElements
    }
}
