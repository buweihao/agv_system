using System.Collections.ObjectModel;
using System.Linq;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.MapManagement.Interfaces;
using AgvDispatcher.Core.Contracts.MapManagement.Requests;
using AgvDispatcher.Core.Contracts.MapManagement.Results;
using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Win32;
using ContractMap = AgvDispatcher.Core.Contracts.Map;

namespace AgvDispatcher.Modules.SystemConfigModule.ViewModels
{
    /// <summary>
    /// 系统设置 - 地图配置页的 ViewModel。
    /// <para>
    /// 提供地图三类核心对象的增删改查：节点 <see cref="MapNode"/>、边 <see cref="MapEdge"/>、
    /// 位置别名 <see cref="MapLocationAlias"/>，分别操作对应仓储；并集成地图校验
    /// （<see cref="IMapValidationService"/>）与 JSON 格式的整图导入/导出。
    /// </para>
    /// <para>
    /// 删除节点前会检查是否被边引用以保证拓扑一致；导入时先做前置校验，存在 Error 级问题则中止，
    /// 校验通过后清空旧数据再整体落库（节点/边/充电桩/别名）。
    /// </para>
    /// </summary>
    public partial class MapConfigViewModel : BindableBase
    {
        private readonly IMapRepository _mapRepository;
        private readonly IMapValidationService _validationService;
        private readonly IChargeStationRepository _chargeStationRepo;
        private readonly IMapLocationAliasRepository _aliasRepo;
        private readonly ISystemParameterRepository _parameterRepo;
        private readonly IMapManagementService _mapManagementService;

        /// <summary>地图节点列表。</summary>
        public ObservableCollection<MapNode> Nodes { get; } = new();

        /// <summary>地图边列表。</summary>
        public ObservableCollection<MapEdge> Edges { get; } = new();

        /// <summary>位置别名列表。</summary>
        public ObservableCollection<MapLocationAlias> Aliases { get; } = new();

        /// <summary>地图校验结果列表。</summary>
        public ObservableCollection<MapValidationResult> ValidationResults { get; } = new();

        /// <summary>地图版本列表。</summary>
        public ObservableCollection<MapVersionDto> MapVersions { get; } = new();

        /// <summary>节点类型可选值（供界面下拉绑定 <see cref="MapNodeType"/>）。</summary>
        public Array NodeTypes { get; } = System.Enum.GetValues(typeof(MapNodeType));

        /// <summary>边方向可选值（供界面下拉绑定 <see cref="EdgeDirection"/>）。</summary>
        public Array EdgeDirections { get; } = System.Enum.GetValues(typeof(EdgeDirection));

        /// <summary>节点类型显示项：保留枚举值，界面显示中文说明。</summary>
        public IReadOnlyList<EnumDisplayItem<MapNodeType>> NodeTypeOptions { get; } =
            Enum.GetValues<MapNodeType>()
                .Select(value => new EnumDisplayItem<MapNodeType>(value, FormatNodeType(value)))
                .ToList();

        /// <summary>路线方向显示项：保留枚举值，界面显示中文说明。</summary>
        public IReadOnlyList<EnumDisplayItem<EdgeDirection>> EdgeDirectionOptions { get; } =
            Enum.GetValues<EdgeDirection>()
                .Select(value => new EnumDisplayItem<EdgeDirection>(value, FormatEdgeDirection(value)))
                .ToList();

        public IReadOnlyList<EnumDisplayItem<ContractMap.MapAreaType>> AreaTypeOptions { get; } =
            Enum.GetValues<ContractMap.MapAreaType>()
                .Select(value => new EnumDisplayItem<ContractMap.MapAreaType>(value, FormatAreaType(value)))
                .ToList();

        private bool _isValidationResultsVisible;
        /// <summary>校验结果面板是否可见（执行校验后置为 true）。</summary>
        public bool IsValidationResultsVisible
        {
            get => _isValidationResultsVisible;
            set => SetProperty(ref _isValidationResultsVisible, value);
        }

        private MapNode? _selectedNode;
        /// <summary>当前选中的节点。</summary>
        public MapNode? SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        private MapEdge? _selectedEdge;
        /// <summary>当前选中的边。</summary>
        public MapEdge? SelectedEdge
        {
            get => _selectedEdge;
            set => SetProperty(ref _selectedEdge, value);
        }

        private MapLocationAlias? _selectedAlias;
        /// <summary>当前选中的别名。</summary>
        public MapLocationAlias? SelectedAlias
        {
            get => _selectedAlias;
            set => SetProperty(ref _selectedAlias, value);
        }

        private MapVersionDto? _selectedMapVersion;
        public MapVersionDto? SelectedMapVersion
        {
            get => _selectedMapVersion;
            set
            {
                if (SetProperty(ref _selectedMapVersion, value))
                {
                    RollbackVersionCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _currentDraftId = string.Empty;
        public string CurrentDraftId
        {
            get => _currentDraftId;
            set => SetProperty(ref _currentDraftId, value);
        }

        private string _currentMapId = "MAIN";
        public string CurrentMapId
        {
            get => _currentMapId;
            set => SetProperty(ref _currentMapId, value);
        }

        private string _currentMapName = "AGV 主地图";
        public string CurrentMapName
        {
            get => _currentMapName;
            set => SetProperty(ref _currentMapName, value);
        }

        private string _currentMapVersion = "draft";
        public string CurrentMapVersion
        {
            get => _currentMapVersion;
            set => SetProperty(ref _currentMapVersion, value);
        }

        /// <summary>刷新（重新加载全部地图数据）命令。</summary>
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand NewDraftCommand { get; }
        public DelegateCommand SaveDraftCommand { get; }
        public DelegateCommand PublishDraftCommand { get; }
        public DelegateCommand RollbackVersionCommand { get; }

        /// <summary>新增节点命令。</summary>
        public DelegateCommand AddNodeCommand { get; }
        /// <summary>保存节点命令（需有选中节点）。</summary>
        public DelegateCommand SaveNodeCommand { get; }
        /// <summary>删除节点命令（需有选中节点）。</summary>
        public DelegateCommand DeleteNodeCommand { get; }

        /// <summary>新增边命令。</summary>
        public DelegateCommand AddEdgeCommand { get; }
        /// <summary>保存边命令（需有选中边）。</summary>
        public DelegateCommand SaveEdgeCommand { get; }
        /// <summary>删除边命令（需有选中边）。</summary>
        public DelegateCommand DeleteEdgeCommand { get; }

        /// <summary>新增别名命令。</summary>
        public DelegateCommand AddAliasCommand { get; }
        /// <summary>保存别名命令（需有选中别名）。</summary>
        public DelegateCommand SaveAliasCommand { get; }
        /// <summary>删除别名命令（需有选中别名）。</summary>
        public DelegateCommand DeleteAliasCommand { get; }

        /// <summary>执行整图校验命令。</summary>
        public DelegateCommand ValidateCommand { get; }
        /// <summary>从 JSON 文件导入地图配置命令。</summary>
        public DelegateCommand ImportCommand { get; }
        /// <summary>导出地图配置到 JSON 文件命令。</summary>
        public DelegateCommand ExportCommand { get; }

        /// <summary>节点总数。</summary>
        public int TotalNodes => Nodes.Count;
        /// <summary>边总数。</summary>
        public int TotalEdges => Edges.Count;
        /// <summary>别名总数。</summary>
        public int TotalAliases => Aliases.Count;
        public int TotalAreas => EditorAreas.Count;

        /// <summary>构造函数：注入仓储与校验服务，绑定全部命令并首次加载数据。</summary>
        /// <param name="mapRepository">地图仓储（节点/边）。</param>
        /// <param name="validationService">地图校验服务。</param>
        /// <param name="chargeStationRepo">充电桩仓储（导入/导出时一并处理）。</param>
        /// <param name="aliasRepo">位置别名仓储。</param>
        /// <param name="parameterRepo">系统参数仓储（持久化地图比例尺/原点）。</param>
        public MapConfigViewModel(
            IMapRepository mapRepository,
            IMapValidationService validationService,
            IChargeStationRepository chargeStationRepo,
            IMapLocationAliasRepository aliasRepo,
            ISystemParameterRepository parameterRepo,
            IMapManagementService mapManagementService)
        {
            _mapRepository = mapRepository;
            _validationService = validationService;
            _chargeStationRepo = chargeStationRepo;
            _aliasRepo = aliasRepo;
            _parameterRepo = parameterRepo;
            _mapManagementService = mapManagementService;

            RefreshCommand = new DelegateCommand(LoadData);
            NewDraftCommand = new DelegateCommand(CreateNewDraft);
            SaveDraftCommand = new DelegateCommand(SaveCurrentDraft);
            PublishDraftCommand = new DelegateCommand(PublishCurrentDraft, () => !string.IsNullOrWhiteSpace(CurrentDraftId))
                .ObservesProperty(() => CurrentDraftId);
            RollbackVersionCommand = new DelegateCommand(RollbackSelectedVersion, () => SelectedMapVersion != null)
                .ObservesProperty(() => SelectedMapVersion);

            AddNodeCommand = new DelegateCommand(AddNode);
            SaveNodeCommand = new DelegateCommand(SaveNode, () => SelectedNode != null).ObservesProperty(() => SelectedNode);
            DeleteNodeCommand = new DelegateCommand(DeleteNode, () => SelectedNode != null).ObservesProperty(() => SelectedNode);

            AddEdgeCommand = new DelegateCommand(AddEdge);
            SaveEdgeCommand = new DelegateCommand(SaveEdge, () => SelectedEdge != null).ObservesProperty(() => SelectedEdge);
            DeleteEdgeCommand = new DelegateCommand(DeleteEdge, () => SelectedEdge != null).ObservesProperty(() => SelectedEdge);

            AddAliasCommand = new DelegateCommand(AddAlias);
            SaveAliasCommand = new DelegateCommand(SaveAlias, () => SelectedAlias != null).ObservesProperty(() => SelectedAlias);
            DeleteAliasCommand = new DelegateCommand(DeleteAlias, () => SelectedAlias != null).ObservesProperty(() => SelectedAlias);

            ValidateCommand = new DelegateCommand(ValidateMapAsync);
            ImportCommand = new DelegateCommand(ImportMapAsync);
            ExportCommand = new DelegateCommand(ExportMapAsync);

            InitializeEditorCommands();

            LoadData();
        }

        /// <summary>从地图草稿加载静态点位、路线和厂商映射，UI 编辑不直接读取运行态交通状态。</summary>
        private void LoadData()
        {
            var draftResult = EnsureDraft();
            if (draftResult.Success && draftResult.Data is not null)
            {
                ApplyDraft(draftResult.Data);
                LoadVersions();
                return;
            }

            Nodes.Clear();
            var nodes = _mapRepository.GetNodesAsync().GetAwaiter().GetResult();
            foreach (var n in nodes) Nodes.Add(n);
            
            Edges.Clear();
            var edges = _mapRepository.GetEdgesAsync().GetAwaiter().GetResult();
            foreach (var e in edges) Edges.Add(e);

            Aliases.Clear();
            var aliases = _aliasRepo.GetAllAsync().GetAwaiter().GetResult();
            foreach (var a in aliases) Aliases.Add(a);

            Stations.Clear();
            var stations = _chargeStationRepo.GetAllAsync().GetAwaiter().GetResult();
            foreach (var s in stations) Stations.Add(s);

            RaisePropertyChanged(nameof(TotalNodes));
            RaisePropertyChanged(nameof(TotalEdges));
            RaisePropertyChanged(nameof(TotalAliases));

            RebuildEditorProjections();
        }

        /// <summary>新增一个带默认参数的节点（编号 N001 递增），并选中它。</summary>
        private void AddNode()
        {
            var nextNumber = Nodes.Count + 1;
            var newNode = new MapNode
            {
                NodeId = $"N{nextNumber:000}",
                MapId = "MAIN",
                NodeCode = $"N{nextNumber:000}",
                Name = $"Node {nextNumber:000}",
                Position = new MapPosition { MapId = "MAIN", NodeId = $"N{nextNumber:000}", X = 0, Y = 0 },
                IsEnabled = true
            };
            Nodes.Add(newNode);
            SelectedNode = newNode;
            RaisePropertyChanged(nameof(TotalNodes));
        }

        /// <summary>保存当前节点：校验节点 ID 非空且不与其他节点重复后写入仓储并刷新。</summary>
        private void SaveNode()
        {
            if (SelectedNode == null) return;
            if (string.IsNullOrWhiteSpace(SelectedNode.NodeId))
            {
                System.Windows.MessageBox.Show("节点ID不能为空", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 校验 NodeId 不与其他节点重复（同一引用对象除外，避免编辑已有节点时误报）
            var isDuplicate = Nodes.Any(n =>
                !ReferenceEquals(n, SelectedNode) &&
                string.Equals(n.NodeId, SelectedNode.NodeId, System.StringComparison.OrdinalIgnoreCase));
            if (isDuplicate)
            {
                System.Windows.MessageBox.Show($"节点ID '{SelectedNode.NodeId}' 已存在，不能重复", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!PassesPreSaveValidation()) return;

            SaveCurrentDraft();
        }

        /// <summary>删除当前节点：若该节点仍被任意边引用则阻止删除并提示，否则删除并刷新。</summary>
        private void DeleteNode()
        {
            if (SelectedNode == null) return;
            var nodeId = SelectedNode.NodeId;
            if (Edges.Any(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId))
            {
                System.Windows.MessageBox.Show($"节点 {nodeId} 被路径引用，无法删除。请先删除相关路径。", "删除失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            Nodes.Remove(SelectedNode);
            SelectedNode = null;
            RaisePropertyChanged(nameof(TotalNodes));
            SaveCurrentDraft();
        }

        /// <summary>新增一条带默认参数的边（编号 E001 递增，限速 1.0），并选中它。</summary>
        private void AddEdge()
        {
            var nextNumber = Edges.Count + 1;
            var newEdge = new MapEdge
            {
                EdgeId = $"E{nextNumber:000}",
                MapId = "MAIN",
                MaxSpeed = 1.0,
                IsEnabled = true
            };
            Edges.Add(newEdge);
            SelectedEdge = newEdge;
            RaisePropertyChanged(nameof(TotalEdges));
        }

        /// <summary>
        /// 保存当前边：校验边 ID/起点/终点非空，起止节点须为已存在节点且不能相同（禁止自环），通过后写入仓储并刷新。
        /// </summary>
        private void SaveEdge()
        {
            if (SelectedEdge == null) return;
            if (string.IsNullOrWhiteSpace(SelectedEdge.EdgeId) || string.IsNullOrWhiteSpace(SelectedEdge.FromNodeId) || string.IsNullOrWhiteSpace(SelectedEdge.ToNodeId))
            {
                System.Windows.MessageBox.Show("路径ID、起点和终点不能为空", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 禁止自环
            if (string.Equals(SelectedEdge.FromNodeId, SelectedEdge.ToNodeId, System.StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show("起点和终点不能是同一个节点", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 起止节点必须是已存在的地图节点，避免产生悬空引用
            if (Nodes.All(n => n.NodeId != SelectedEdge.FromNodeId))
            {
                System.Windows.MessageBox.Show($"起点节点 '{SelectedEdge.FromNodeId}' 不存在，请从已有节点中选择", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            if (Nodes.All(n => n.NodeId != SelectedEdge.ToNodeId))
            {
                System.Windows.MessageBox.Show($"终点节点 '{SelectedEdge.ToNodeId}' 不存在，请从已有节点中选择", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!PassesPreSaveValidation()) return;

            SaveCurrentDraft();
        }

        /// <summary>删除当前选中的边并刷新列表。</summary>
        private void DeleteEdge()
        {
            if (SelectedEdge == null) return;
            Edges.Remove(SelectedEdge);
            SelectedEdge = null;
            RaisePropertyChanged(nameof(TotalEdges));
            SaveCurrentDraft();
        }

        /// <summary>
        /// 新增一条别名：默认绑定到首个节点、品牌为空（全局别名）、生成 GUID 作为 AliasId，并选中它。
        /// </summary>
        private void AddAlias()
        {
            var nextNumber = Aliases.Count + 1;
            var newAlias = new MapLocationAlias
            {
                AliasId = System.Guid.NewGuid().ToString("N"),
                MapId = "MAIN",
                NodeId = Nodes.FirstOrDefault()?.NodeId ?? "",
                AliasType = "Custom",
                AliasValue = $"A{nextNumber:000}",
                Brand = "", // Global alias by default
                IsEnabled = true
            };
            Aliases.Add(newAlias);
            SelectedAlias = newAlias;
            RaisePropertyChanged(nameof(TotalAliases));
        }

        /// <summary>
        /// 保存当前别名：校验节点 ID 与别名值非空，并检查同一品牌下别名值不重复，通过后写入仓储并刷新。
        /// </summary>
        private void SaveAlias()
        {
            if (SelectedAlias == null) return;
            if (string.IsNullOrWhiteSpace(SelectedAlias.NodeId) || string.IsNullOrWhiteSpace(SelectedAlias.AliasValue))
            {
                System.Windows.MessageBox.Show("节点ID和别名值不能为空", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var isDuplicate = Aliases.Any(a => 
                a.AliasId != SelectedAlias.AliasId &&
                a.AliasValue == SelectedAlias.AliasValue &&
                string.Equals(a.Brand ?? "", SelectedAlias.Brand ?? "", System.StringComparison.OrdinalIgnoreCase));
            
            if (isDuplicate)
            {
                System.Windows.MessageBox.Show("同一品牌下的别名值不能重复", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!PassesPreSaveValidation()) return;

            SaveCurrentDraft();
        }

        /// <summary>删除当前选中的别名并刷新列表。</summary>
        private void DeleteAlias()
        {
            if (SelectedAlias == null) return;
            Aliases.Remove(SelectedAlias);
            SelectedAlias = null;
            RaisePropertyChanged(nameof(TotalAliases));
            SaveCurrentDraft();
        }

        /// <summary>
        /// 保存前整图级前置校验：基于当前界面内存中的节点/边/别名（充电桩留空）调用
        /// <see cref="IMapValidationService.ValidateMapDataAsync"/>，若存在 <see cref="MapValidationLevel.Error"/>
        /// 级问题（如错误点位、悬空路径、重复别名）则弹窗提示首条并返回 <c>false</c> 以中止落库；
        /// 否则返回 <c>true</c> 放行。作为各 Save 命令逐条快速校验之后的第二道关，防止脏数据进库。
        /// </summary>
        private bool PassesPreSaveValidation()
        {
            if (!SaveCurrentDraft(showMessage: false))
            {
                System.Windows.MessageBox.Show(
                    "保存草稿失败，已中止后续校验。请检查当前地图数据或稍后重试。",
                    "保存草稿失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }

            var validation = _mapManagementService.ValidateDraftAsync(new ValidateMapDraftRequest
            {
                Context = new RequestContext(),
                DraftId = CurrentDraftId
            }).GetAwaiter().GetResult();

            if (!validation.Success || validation.Data?.IsValid != true)
            {
                var firstError = validation.Data?.Messages.FirstOrDefault() ?? validation.Message;
                System.Windows.MessageBox.Show(
                    $"保存前校验未通过：地图数据存在错误。\n{firstError}",
                    "保存前校验未通过", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 执行整图校验：调用校验服务，将结果填入 <see cref="ValidationResults"/> 并显示面板，
        /// 最后弹窗汇总 Error/Warning/总数。
        /// </summary>
        private async void ValidateMapAsync()
        {
            ValidationResults.Clear();

            if (!SaveCurrentDraft(showMessage: true))
            {
                return;
            }

            var validation = await _mapManagementService.ValidateDraftAsync(new ValidateMapDraftRequest
            {
                Context = new RequestContext(),
                DraftId = CurrentDraftId
            });

            if (!validation.Success)
            {
                System.Windows.MessageBox.Show($"校验失败：{validation.Message}", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var messages = validation.Data?.Messages ?? Array.Empty<string>();
            foreach (var result in ToLegacyValidationResults(messages))
            {
                ValidationResults.Add(result);
            }

            IsValidationResultsVisible = true;

            var errorCount = ValidationResults.Count(x => x.Level == MapValidationLevel.Error);
            var warningCount = ValidationResults.Count(x => x.Level == MapValidationLevel.Warning);

            System.Windows.MessageBox.Show($"校验完成：Error={errorCount}, Warning={warningCount}, Total={ValidationResults.Count}");
        }

        /// <summary>
        /// 从 JSON 文件导入整图配置。流程：选择文件 → 反序列化 → 前置校验（有 Error 则中止）→
        /// 清空旧数据（节点/边/充电桩/别名）→ 整体落库 → 刷新。任何异常均弹窗提示。
        /// </summary>
        private async void ImportMapAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "导入地图配置"
            };

            if (openFileDialog.ShowDialog() != true) return;

            try
            {
                var json = await File.ReadAllTextAsync(openFileDialog.FileName);
                var import = await _mapManagementService.ImportMapAsync(new ImportMapRequest
                {
                    Context = new RequestContext(),
                    Payload = json,
                    Format = "json",
                    Comment = "UI import"
                });
                if (!import.Success || import.Data is null)
                {
                    System.Windows.MessageBox.Show($"导入失败：{import.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                ApplyDraft(import.Data.Draft);
                LoadVersions();
                System.Windows.MessageBox.Show("导入成功：已生成地图草稿，尚未切换当前运行地图。", "成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"导入地图失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出整图配置到 JSON 文件：汇总节点/边/充电桩/别名及元信息，缩进序列化后写入用户选择的文件。
        /// </summary>
        private async void ExportMapAsync()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json",
                Title = "导出地图配置",
                FileName = $"MapConfig_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (saveFileDialog.ShowDialog() != true) return;

            try
            {
                if (!SaveCurrentDraft(showMessage: true))
                {
                    return;
                }

                var export = await _mapManagementService.ExportMapAsync(new ExportMapRequest
                {
                    Context = new RequestContext(),
                    MapId = CurrentMapId,
                    Version = CurrentDraftId,
                    Format = "json"
                });
                if (!export.Success || export.Data is null)
                {
                    System.Windows.MessageBox.Show($"导出失败：{export.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                await File.WriteAllTextAsync(saveFileDialog.FileName, export.Data.Payload);
                System.Windows.MessageBox.Show("导出地图配置成功", "成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"导出地图失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void CreateNewDraft()
        {
            var confirm = System.Windows.MessageBox.Show(
                "新建草稿会先保存当前草稿内容，然后清空画布生成一个新的空白草稿。当前运行地图不会被切换，确认继续吗？",
                "新建空白草稿",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(CurrentDraftId))
            {
                if (!SaveCurrentDraft(showMessage: true))
                {
                    return;
                }
            }

            var result = _mapManagementService.CreateDraftAsync(new CreateMapDraftRequest
            {
                Context = new RequestContext(),
                MapName = string.IsNullOrWhiteSpace(CurrentMapName) ? "AGV 空白草稿地图" : $"{CurrentMapName}-新草稿"
            }).GetAwaiter().GetResult();

            if (!result.Success || result.Data is null)
            {
                System.Windows.MessageBox.Show($"新建草稿失败：{result.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ApplyDraft(result.Data);
            ClearEditorForBlankDraft();
            if (!SaveCurrentDraft(showMessage: true))
            {
                return;
            }

            System.Windows.MessageBox.Show("已创建空白地图草稿。当前运行地图未切换，原地图已保留，后续发布才会切换运行地图。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearEditorForBlankDraft()
        {
            Nodes.Clear();
            Edges.Clear();
            Aliases.Clear();
            Stations.Clear();
            ApplyAreas(Array.Empty<ContractMap.MapAreaDto>());
            RaiseEditorCounts();
            RebuildEditorProjections();
        }
        private void SaveCurrentDraft()
        {
            _ = SaveCurrentDraft(showMessage: true);
        }

        /// <summary>
        /// 保存草稿只更新编辑草稿，不影响当前运行图，也不会触发地图发布事件。
        /// </summary>
        private bool SaveCurrentDraft(bool showMessage)
        {
            try
            {
                var draft = EnsureDraft();
                if (draft.Success
                    && draft.Data is not null
                    && !string.Equals(CurrentDraftId, draft.Data.DraftId, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyDraftMetadata(draft.Data);
                }

                if (!draft.Success || string.IsNullOrWhiteSpace(CurrentDraftId))
                {
                    if (showMessage)
                    {
                        System.Windows.MessageBox.Show($"保存草稿失败：{draft.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    return false;
                }

                var save = _mapManagementService.SaveDraftAsync(new SaveMapDraftRequest
                {
                    Context = new RequestContext(),
                    DraftId = CurrentDraftId,
                    Map = BuildSnapshotFromEditor(),
                    Comment = "UI save draft"
                }).GetAwaiter().GetResult();

                if (!save.Success || save.Data is null)
                {
                    if (showMessage)
                    {
                        System.Windows.MessageBox.Show($"保存草稿失败：{save.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    return false;
                }

                ApplyDraftMetadata(save.Data);
                if (showMessage)
                {
                    System.Windows.MessageBox.Show("地图草稿已保存，当前运行地图未切换。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (showMessage)
                {
                    System.Windows.MessageBox.Show($"保存草稿失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return false;
            }
        }

        /// <summary>
        /// 发布草稿会切换当前运行图，服务层发布 MapPublishedEvent 通知其他模块重新读取 IMapService。
        /// </summary>
        private void PublishCurrentDraft()
        {
            if (!SaveCurrentDraft(showMessage: true))
            {
                return;
            }

            var validation = _mapManagementService.ValidateDraftAsync(new ValidateMapDraftRequest
            {
                Context = new RequestContext(),
                DraftId = CurrentDraftId
            }).GetAwaiter().GetResult();

            if (!validation.Success || validation.Data?.IsValid != true)
            {
                var message = validation.Data is null ? validation.Message : string.Join("\n", validation.Data.Messages);
                System.Windows.MessageBox.Show($"发布失败：地图校验未通过。\n{message}", "发布失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                "发布地图会切换当前运行地图，确认发布当前草稿吗？",
                "确认发布",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var publish = _mapManagementService.PublishDraftAsync(new PublishMapDraftRequest
            {
                Context = new RequestContext(),
                DraftId = CurrentDraftId,
                Comment = "UI publish"
            }).GetAwaiter().GetResult();

            if (!publish.Success || publish.Data is null)
            {
                System.Windows.MessageBox.Show($"发布失败：{publish.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CurrentMapVersion = publish.Data.Version;
            LoadVersions();
            System.Windows.MessageBox.Show($"地图已发布为版本 {publish.Data.Version}。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 回滚版本会切换当前运行图，并触发运行图刷新事件。
        /// </summary>
        private void RollbackSelectedVersion()
        {
            if (SelectedMapVersion is null)
            {
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                $"回滚会把当前运行地图切换到版本 {SelectedMapVersion.Version}，确认继续吗？",
                "确认回滚",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var rollback = _mapManagementService.RollbackToVersionAsync(new RollbackMapVersionRequest
            {
                Context = new RequestContext(),
                MapId = SelectedMapVersion.MapId,
                Version = SelectedMapVersion.Version,
                Reason = "UI rollback"
            }).GetAwaiter().GetResult();

            if (!rollback.Success || rollback.Data is null)
            {
                System.Windows.MessageBox.Show($"回滚失败：{rollback.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CurrentMapVersion = rollback.Data.Version;
            var draft = _mapManagementService.CreateDraftAsync(new CreateMapDraftRequest
            {
                Context = new RequestContext(),
                MapName = $"{CurrentMapName}-回滚草稿",
                SourceMapId = rollback.Data.MapId,
                SourceVersion = rollback.Data.Version
            }).GetAwaiter().GetResult();

            if (!draft.Success || draft.Data is null)
            {
                LoadVersions();
                System.Windows.MessageBox.Show(
                    $"已回滚到版本 {rollback.Data.Version}，但创建回滚草稿失败：{draft.Message}",
                    "回滚完成，草稿同步失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ApplyDraft(draft.Data);
            LoadVersions();
            SelectedMapVersion = MapVersions.FirstOrDefault(version =>
                string.Equals(version.MapId, rollback.Data.MapId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(version.Version, rollback.Data.Version, StringComparison.OrdinalIgnoreCase));
            System.Windows.MessageBox.Show($"已回滚到版本 {rollback.Data.Version}，并已基于该版本创建新的编辑草稿。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private AgvResult<MapDraftDto> EnsureDraft()
        {
            if (!string.IsNullOrWhiteSpace(CurrentDraftId))
            {
                var existing = _mapManagementService.GetDraftAsync(new GetMapDraftRequest
                {
                    Context = new RequestContext(),
                    DraftId = CurrentDraftId
                }).GetAwaiter().GetResult();

                if (existing.Success)
                {
                    return existing;
                }
            }

            return _mapManagementService.CreateDraftAsync(new CreateMapDraftRequest
            {
                Context = new RequestContext(),
                MapName = CurrentMapName
            }).GetAwaiter().GetResult();
        }

        private void ApplyDraft(MapDraftDto draft)
        {
            ApplyDraftMetadata(draft);

            Nodes.Clear();
            foreach (var node in draft.Map.Nodes.Select(ToLegacyNode))
            {
                Nodes.Add(node);
            }

            Edges.Clear();
            foreach (var edge in draft.Map.Edges.Select(ToLegacyEdge))
            {
                Edges.Add(edge);
            }

            Aliases.Clear();
            foreach (var alias in draft.Map.VendorNodeMappings.Select(ToLegacyAlias))
            {
                Aliases.Add(alias);
            }

            Stations.Clear();
            ApplyAreas(draft.Map.Areas);
            RaiseEditorCounts();
            RebuildEditorProjections();
        }

        private void ApplyDraftMetadata(MapDraftDto draft)
        {
            CurrentDraftId = draft.DraftId;
            CurrentMapId = draft.Map.MapId;
            CurrentMapName = draft.Map.MapName;
            CurrentMapVersion = draft.Map.Version;
            PublishDraftCommand.RaiseCanExecuteChanged();
        }

        private void LoadVersions()
        {
            var previousMapId = SelectedMapVersion?.MapId;
            var previousVersion = SelectedMapVersion?.Version;

            MapVersions.Clear();
            // Rollback is a running-map switch, so the candidate list must span all
            // published map ids. Otherwise rolling back to MAIN/v1 hides later maps
            // that were published under another MapId.
            var versions = LoadVersionDtos(null);

            foreach (var version in versions
                .Where(IsRollbackCandidate)
                .OrderByDescending(version => version.IsCurrent)
                .ThenByDescending(version => version.PublishedAt))
            {
                MapVersions.Add(version);
            }

            var selected = MapVersions.FirstOrDefault(version => version.IsCurrent)
                ?? MapVersions.FirstOrDefault(version =>
                    string.Equals(version.MapId, previousMapId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(version.Version, previousVersion, StringComparison.OrdinalIgnoreCase))
                ?? MapVersions.FirstOrDefault();

            SelectedMapVersion = selected;
        }

        private IReadOnlyList<MapVersionDto> LoadVersionDtos(string? mapId)
        {
            var versions = _mapManagementService.GetMapVersionsAsync(new GetMapVersionsRequest
            {
                Context = new RequestContext(),
                MapId = mapId
            }).GetAwaiter().GetResult();

            return versions.Success && versions.Data is not null
                ? versions.Data
                : Array.Empty<MapVersionDto>();
        }

        private static bool IsRollbackCandidate(MapVersionDto version) =>
            version.State is MapState.Published or MapState.Active or MapState.Archived;

        private ContractMap.MapSnapshotDto BuildSnapshotFromEditor()
        {
            var areas = EditorAreas
                .Select(area => area.ToDto())
                .Where(area => !string.IsNullOrWhiteSpace(area.AreaId))
                .ToDictionary(area => area.AreaId, StringComparer.OrdinalIgnoreCase);

            foreach (var area in Nodes.Select(node => node.AreaCode)
                .Concat(Edges.Select(edge => edge.AreaCode))
                .Where(area => !string.IsNullOrWhiteSpace(area))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(area => !areas.ContainsKey(area))
                .Select(area => new ContractMap.MapAreaDto
                {
                    AreaId = area,
                    AreaName = area,
                    AreaType = ContractMap.MapAreaType.Normal,
                    Properties = new Dictionary<string, string> { ["Capacity"] = "0", ["Color"] = "#5A7FA6" }
                }))
            {
                areas[area.AreaId] = area;
            }

            return new ContractMap.MapSnapshotDto
            {
                MapId = string.IsNullOrWhiteSpace(CurrentMapId) ? "MAIN" : CurrentMapId,
                MapName = string.IsNullOrWhiteSpace(CurrentMapName) ? "AGV 主地图" : CurrentMapName,
                Version = "draft",
                Nodes = Nodes.Select(ToContractNode).ToList(),
                Edges = Edges.Select(ToContractEdge).ToList(),
                Areas = areas.Values.ToList(),
                VendorNodeMappings = Aliases.Where(alias => alias.IsEnabled).Select(ToContractMapping).ToList(),
                UpdatedAt = DateTimeOffset.Now
            };
        }

        private static ContractMap.MapNodeDto ToContractNode(MapNode node)
        {
            var properties = new Dictionary<string, string>
            {
                ["Capacity"] = node.ParkingCapacity.ToString(),
                ["AllowedBrands"] = node.AllowedBrands,
                ["RequiredCapabilities"] = ((int)node.RequiredCapabilities).ToString()
            };
            foreach (var tag in node.Tags)
            {
                properties[tag.Key] = tag.Value;
            }

            return new ContractMap.MapNodeDto
            {
                NodeId = node.NodeId,
                NodeCode = node.NodeCode,
                NodeName = node.Name,
                NodeType = ToContractNodeType(node.NodeType),
                X = node.Position.X,
                Y = node.Position.Y,
                Angle = node.Heading,
                AreaId = string.IsNullOrWhiteSpace(node.AreaCode) ? null : node.AreaCode,
                Enabled = node.IsEnabled,
                Properties = properties
            };
        }

        private ContractMap.MapEdgeDto ToContractEdge(MapEdge edge)
        {
            var distance = ResolveEdgeDistance(edge);
            var cost = edge.Cost > 0 ? edge.Cost : Math.Max(1, (int)Math.Round(distance));

            return new ContractMap.MapEdgeDto
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Distance = distance,
                Direction = edge.Direction == EdgeDirection.Bidirectional
                    ? ContractMap.MapEdgeDirection.Bidirectional
                    : ContractMap.MapEdgeDirection.OneWay,
                Cost = cost,
                SpeedLimit = edge.MaxSpeed,
                AreaId = string.IsNullOrWhiteSpace(edge.AreaCode) ? null : edge.AreaCode,
                Enabled = edge.IsEnabled && edge.Direction != EdgeDirection.Closed,
                Properties = new Dictionary<string, string>
                {
                    ["AllowedBrands"] = edge.AllowedBrands,
                    ["MaxVehicleFlow"] = edge.MaxVehicleFlow.ToString(),
                    ["Remark"] = edge.Remark
                }
            };
        }

        private double ResolveEdgeDistance(MapEdge edge)
        {
            if (edge.Length > 0)
            {
                return edge.Length;
            }

            var from = Nodes.FirstOrDefault(node =>
                string.Equals(node.NodeId, edge.FromNodeId, StringComparison.OrdinalIgnoreCase));
            var to = Nodes.FirstOrDefault(node =>
                string.Equals(node.NodeId, edge.ToNodeId, StringComparison.OrdinalIgnoreCase));
            if (from is null || to is null)
            {
                return 0;
            }

            var dx = to.Position.X - from.Position.X;
            var dy = to.Position.Y - from.Position.Y;
            var pixelDistance = Math.Sqrt(dx * dx + dy * dy);
            if (pixelDistance <= 0)
            {
                return 0;
            }

            var pixelsPerMeter = Settings.PixelsPerMeter <= 0 ? 1 : Settings.PixelsPerMeter;
            var distance = pixelDistance / pixelsPerMeter;

            // Repair old drafts created before canvas-created edges wrote their physical length.
            edge.Length = distance;
            if (edge.Cost <= 0)
            {
                edge.Cost = Math.Max(1, (int)Math.Round(distance));
            }

            return distance;
        }

        private static ContractMap.VendorNodeMappingDto ToContractMapping(MapLocationAlias alias)
        {
            return new ContractMap.VendorNodeMappingDto
            {
                VendorCode = string.IsNullOrWhiteSpace(alias.Brand) ? "GLOBAL" : alias.Brand,
                SystemNodeId = alias.NodeId,
                VendorNodeCode = alias.AliasValue
            };
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
                AllowedBrands = TryReadString(node.Properties, "AllowedBrands"),
                RequiredCapabilities = (VehicleCapability)TryReadInt(node.Properties, "RequiredCapabilities", 0)
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

        private static MapLocationAlias ToLegacyAlias(ContractMap.VendorNodeMappingDto mapping)
        {
            return new MapLocationAlias
            {
                AliasId = Guid.NewGuid().ToString("N"),
                MapId = "MAIN",
                NodeId = mapping.SystemNodeId,
                AliasType = "Vendor",
                AliasValue = mapping.VendorNodeCode,
                Brand = string.Equals(mapping.VendorCode, "GLOBAL", StringComparison.OrdinalIgnoreCase) ? string.Empty : mapping.VendorCode,
                IsEnabled = true
            };
        }

        private static IEnumerable<MapValidationResult> ToLegacyValidationResults(IEnumerable<string> messages)
        {
            foreach (var message in messages)
            {
                var isError = IsValidationError(message);
                yield return new MapValidationResult
                {
                    Level = isError ? MapValidationLevel.Error : MapValidationLevel.Warning,
                    ObjectType = message.Split(' ').Skip(1).FirstOrDefault() ?? "Map",
                    ObjectId = string.Empty,
                    Message = message
                };
            }
        }

        private static bool IsValidationError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return !message.StartsWith("P1", StringComparison.OrdinalIgnoreCase);
        }

        private void RaiseEditorCounts()
        {
            RaisePropertyChanged(nameof(TotalNodes));
            RaisePropertyChanged(nameof(TotalEdges));
            RaisePropertyChanged(nameof(TotalAliases));
            RaisePropertyChanged(nameof(TotalAreas));
        }

        private static ContractMap.MapNodeType ToContractNodeType(MapNodeType nodeType) => nodeType switch
        {
            MapNodeType.Station => ContractMap.MapNodeType.WorkStation,
            MapNodeType.Pickup => ContractMap.MapNodeType.PickPoint,
            MapNodeType.Dropoff => ContractMap.MapNodeType.PutPoint,
            MapNodeType.Charge => ContractMap.MapNodeType.ChargeStation,
            MapNodeType.Waiting => ContractMap.MapNodeType.WaitingPoint,
            MapNodeType.Elevator => ContractMap.MapNodeType.Elevator,
            MapNodeType.Door => ContractMap.MapNodeType.Door,
            _ => ContractMap.MapNodeType.Normal
        };

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

        private static string FormatNodeType(MapNodeType nodeType) => nodeType switch
        {
            MapNodeType.Normal => "Normal（普通点）",
            MapNodeType.Station => "Station（工位点）",
            MapNodeType.Pickup => "Pickup（取货点）",
            MapNodeType.Dropoff => "Dropoff（放货点）",
            MapNodeType.Charge => "Charge（充电点）",
            MapNodeType.Waiting => "Waiting（等待点）",
            MapNodeType.Intersection => "Intersection（路口点）",
            MapNodeType.Elevator => "Elevator（电梯点）",
            MapNodeType.Door => "Door（门禁点）",
            MapNodeType.Restricted => "Restricted（限制点）",
            _ => $"{nodeType}（未知类型）"
        };

        private static string FormatEdgeDirection(EdgeDirection direction) => direction switch
        {
            EdgeDirection.Bidirectional => "Bidirectional（双向）",
            EdgeDirection.ForwardOnly => "ForwardOnly（正向单行）",
            EdgeDirection.ReverseOnly => "ReverseOnly（反向单行）",
            EdgeDirection.Closed => "Closed（静态封闭）",
            _ => $"{direction}（未知方向）"
        };

        private static string FormatAreaType(ContractMap.MapAreaType areaType) => areaType switch
        {
            ContractMap.MapAreaType.Normal => "Normal（普通区域）",
            ContractMap.MapAreaType.WorkArea => "WorkArea（作业区域）",
            ContractMap.MapAreaType.ChargingArea => "ChargingArea（充电区域）",
            ContractMap.MapAreaType.WaitingArea => "WaitingArea（等待区域）",
            ContractMap.MapAreaType.NarrowArea => "NarrowArea（窄道区域）",
            ContractMap.MapAreaType.IntersectionArea => "IntersectionArea（路口区域）",
            ContractMap.MapAreaType.BlockedArea => "BlockedArea（封闭区域）",
            ContractMap.MapAreaType.Unknown => "Unknown（未知区域）",
            _ => $"{areaType}（未知区域）"
        };

        private static int TryReadInt(IReadOnlyDictionary<string, string> properties, string key, int fallback)
        {
            return properties.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;
        }

        private static string TryReadString(IReadOnlyDictionary<string, string> properties, string key)
        {
            return properties.TryGetValue(key, out var value) ? value : string.Empty;
        }
    }

    public sealed class EnumDisplayItem<T>
        where T : struct, Enum
    {
        public EnumDisplayItem(T value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public T Value { get; }

        public string DisplayName { get; }
    }

    /// <summary>
    /// 地图导入/导出的数据容器（对应 JSON 文件结构）。
    /// 汇总整张地图的节点、边、充电桩、别名，以及地图标识、版本与导出时间等元信息。
    /// </summary>
    public class MapExportData
    {
        /// <summary>全部地图节点。</summary>
        public List<MapNode> MapNodes { get; set; } = new();

        /// <summary>全部地图边。</summary>
        public List<MapEdge> MapEdges { get; set; } = new();

        /// <summary>全部充电桩。</summary>
        public List<ChargeStation> ChargeStations { get; set; } = new();

        /// <summary>全部位置别名。</summary>
        public List<MapLocationAlias> MapLocationAliases { get; set; } = new();

        /// <summary>地图标识。</summary>
        public string MapId { get; set; } = "default";

        /// <summary>数据格式版本。</summary>
        public string Version { get; set; } = "1.0";

        /// <summary>导出时间。</summary>
        public DateTime ExportTime { get; set; }
    }
}
