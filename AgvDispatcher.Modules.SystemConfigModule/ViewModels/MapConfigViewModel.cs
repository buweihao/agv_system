using System.Collections.ObjectModel;
using System.Linq;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
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
    public class MapConfigViewModel : BindableBase
    {
        private readonly IMapRepository _mapRepository;
        private readonly IMapValidationService _validationService;
        private readonly IChargeStationRepository _chargeStationRepo;
        private readonly IMapLocationAliasRepository _aliasRepo;

        /// <summary>地图节点列表。</summary>
        public ObservableCollection<MapNode> Nodes { get; } = new();

        /// <summary>地图边列表。</summary>
        public ObservableCollection<MapEdge> Edges { get; } = new();

        /// <summary>位置别名列表。</summary>
        public ObservableCollection<MapLocationAlias> Aliases { get; } = new();

        /// <summary>地图校验结果列表。</summary>
        public ObservableCollection<MapValidationResult> ValidationResults { get; } = new();

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

        /// <summary>刷新（重新加载全部地图数据）命令。</summary>
        public DelegateCommand RefreshCommand { get; }

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

        /// <summary>构造函数：注入仓储与校验服务，绑定全部命令并首次加载数据。</summary>
        /// <param name="mapRepository">地图仓储（节点/边）。</param>
        /// <param name="validationService">地图校验服务。</param>
        /// <param name="chargeStationRepo">充电桩仓储（导入/导出时一并处理）。</param>
        /// <param name="aliasRepo">位置别名仓储。</param>
        public MapConfigViewModel(
            IMapRepository mapRepository,
            IMapValidationService validationService,
            IChargeStationRepository chargeStationRepo,
            IMapLocationAliasRepository aliasRepo)
        {
            _mapRepository = mapRepository;
            _validationService = validationService;
            _chargeStationRepo = chargeStationRepo;
            _aliasRepo = aliasRepo;

            RefreshCommand = new DelegateCommand(LoadData);

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

            LoadData();
        }

        /// <summary>从仓储重新加载节点、边、别名三类数据并刷新计数。</summary>
        private void LoadData()
        {
            Nodes.Clear();
            var nodes = _mapRepository.GetNodesAsync().GetAwaiter().GetResult();
            foreach (var n in nodes) Nodes.Add(n);
            
            Edges.Clear();
            var edges = _mapRepository.GetEdgesAsync().GetAwaiter().GetResult();
            foreach (var e in edges) Edges.Add(e);

            Aliases.Clear();
            var aliases = _aliasRepo.GetAllAsync().GetAwaiter().GetResult();
            foreach (var a in aliases) Aliases.Add(a);

            RaisePropertyChanged(nameof(TotalNodes));
            RaisePropertyChanged(nameof(TotalEdges));
            RaisePropertyChanged(nameof(TotalAliases));
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

        /// <summary>保存当前节点：校验节点 ID 非空后写入仓储并刷新。</summary>
        private void SaveNode()
        {
            if (SelectedNode == null) return;
            if (string.IsNullOrWhiteSpace(SelectedNode.NodeId))
            {
                System.Windows.MessageBox.Show("节点ID不能为空", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            _mapRepository.SaveNodeAsync(SelectedNode).GetAwaiter().GetResult();
            LoadData();
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
            _mapRepository.DeleteNodeAsync(nodeId).GetAwaiter().GetResult();
            LoadData();
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

        /// <summary>保存当前边：校验边 ID、起点、终点均非空后写入仓储并刷新。</summary>
        private void SaveEdge()
        {
            if (SelectedEdge == null) return;
            if (string.IsNullOrWhiteSpace(SelectedEdge.EdgeId) || string.IsNullOrWhiteSpace(SelectedEdge.FromNodeId) || string.IsNullOrWhiteSpace(SelectedEdge.ToNodeId))
            {
                System.Windows.MessageBox.Show("路径ID、起点和终点不能为空", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            _mapRepository.SaveEdgeAsync(SelectedEdge).GetAwaiter().GetResult();
            LoadData();
        }

        /// <summary>删除当前选中的边并刷新列表。</summary>
        private void DeleteEdge()
        {
            if (SelectedEdge == null) return;
            _mapRepository.DeleteEdgeAsync(SelectedEdge.EdgeId).GetAwaiter().GetResult();
            LoadData();
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

            _aliasRepo.SaveAsync(SelectedAlias).GetAwaiter().GetResult();
            LoadData();
        }

        /// <summary>删除当前选中的别名并刷新列表。</summary>
        private void DeleteAlias()
        {
            if (SelectedAlias == null) return;
            _aliasRepo.DeleteAsync(SelectedAlias.AliasId).GetAwaiter().GetResult();
            LoadData();
        }

        /// <summary>
        /// 执行整图校验：调用校验服务，将结果填入 <see cref="ValidationResults"/> 并显示面板，
        /// 最后弹窗汇总 Error/Warning/总数。
        /// </summary>
        private async void ValidateMapAsync()
        {
            ValidationResults.Clear();

            var results = await _validationService.ValidateMapAsync();
            foreach (var result in results)
            {
                ValidationResults.Add(result);
            }

            IsValidationResultsVisible = true;

            var errorCount = results.Count(x => x.Level == MapValidationLevel.Error);
            var warningCount = results.Count(x => x.Level == MapValidationLevel.Warning);

            System.Windows.MessageBox.Show($"校验完成：Error={errorCount}, Warning={warningCount}, Total={results.Count}");
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
                var data = JsonSerializer.Deserialize<MapExportData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (data == null || data.MapNodes == null || data.MapEdges == null)
                {
                    System.Windows.MessageBox.Show("导入失败：文件格式不正确或为空", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                // 前置校验
                var chargeStations = data.ChargeStations ?? new List<ChargeStation>();
                var aliases = data.MapLocationAliases ?? new List<MapLocationAlias>();
                
                var results = await _validationService.ValidateMapDataAsync(data.MapNodes, data.MapEdges, chargeStations, aliases);
                if (results.Any(r => r.Level == MapValidationLevel.Error))
                {
                    var firstError = results.First(r => r.Level == MapValidationLevel.Error).Message;
                    System.Windows.MessageBox.Show($"导入失败：检测到冲突或无效数据。\n{firstError}", "校验错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                // 校验通过，清理旧数据并保存新数据
                var oldNodes = await _mapRepository.GetNodesAsync();
                foreach (var n in oldNodes) await _mapRepository.DeleteNodeAsync(n.NodeId);
                var oldEdges = await _mapRepository.GetEdgesAsync();
                foreach (var e in oldEdges) await _mapRepository.DeleteEdgeAsync(e.EdgeId);
                var oldCharges = await _chargeStationRepo.GetAllAsync();
                foreach (var c in oldCharges) await _chargeStationRepo.DeleteAsync(c.StationId);
                var oldAliases = await _aliasRepo.GetAllAsync();
                foreach (var a in oldAliases) await _aliasRepo.DeleteAsync(a.AliasId);

                // 保存新数据
                foreach (var n in data.MapNodes) await _mapRepository.SaveNodeAsync(n);
                foreach (var e in data.MapEdges) await _mapRepository.SaveEdgeAsync(e);
                foreach (var c in chargeStations) await _chargeStationRepo.SaveAsync(c);
                foreach (var a in aliases) await _aliasRepo.SaveAsync(a);

                LoadData();
                System.Windows.MessageBox.Show("导入地图配置成功", "成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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
                var data = new MapExportData
                {
                    MapNodes = (await _mapRepository.GetNodesAsync()).ToList(),
                    MapEdges = (await _mapRepository.GetEdgesAsync()).ToList(),
                    ChargeStations = (await _chargeStationRepo.GetAllAsync()).ToList(),
                    MapLocationAliases = (await _aliasRepo.GetAllAsync()).ToList(),
                    MapId = "default",
                    Version = "1.0",
                    ExportTime = DateTime.Now
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(data, options);

                await File.WriteAllTextAsync(saveFileDialog.FileName, json);
                System.Windows.MessageBox.Show("导出地图配置成功", "成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"导出地图失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
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
