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
    public class MapConfigViewModel : BindableBase
    {
        private readonly IMapRepository _mapRepository;
        private readonly IMapValidationService _validationService;
        private readonly IChargeStationRepository _chargeStationRepo;
        private readonly IMapLocationAliasRepository _aliasRepo;
        
        public ObservableCollection<MapNode> Nodes { get; } = new();
        public ObservableCollection<MapEdge> Edges { get; } = new();
        public ObservableCollection<MapValidationResult> ValidationResults { get; } = new();

        private bool _isValidationResultsVisible;
        public bool IsValidationResultsVisible
        {
            get => _isValidationResultsVisible;
            set => SetProperty(ref _isValidationResultsVisible, value);
        }

        private MapNode? _selectedNode;
        public MapNode? SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        private MapEdge? _selectedEdge;
        public MapEdge? SelectedEdge
        {
            get => _selectedEdge;
            set => SetProperty(ref _selectedEdge, value);
        }

        public DelegateCommand RefreshCommand { get; }
        
        public DelegateCommand AddNodeCommand { get; }
        public DelegateCommand SaveNodeCommand { get; }
        public DelegateCommand DeleteNodeCommand { get; }

        public DelegateCommand AddEdgeCommand { get; }
        public DelegateCommand SaveEdgeCommand { get; }
        public DelegateCommand DeleteEdgeCommand { get; }

        public DelegateCommand ValidateCommand { get; }
        public DelegateCommand ImportCommand { get; }
        public DelegateCommand ExportCommand { get; }

        public int TotalNodes => Nodes.Count;
        public int TotalEdges => Edges.Count;

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

            ValidateCommand = new DelegateCommand(ValidateMapAsync);
            ImportCommand = new DelegateCommand(ImportMapAsync);
            ExportCommand = new DelegateCommand(ExportMapAsync);

            LoadData();
        }

        private void LoadData()
        {
            Nodes.Clear();
            var nodes = _mapRepository.GetNodesAsync().GetAwaiter().GetResult();
            foreach (var n in nodes) Nodes.Add(n);
            
            Edges.Clear();
            var edges = _mapRepository.GetEdgesAsync().GetAwaiter().GetResult();
            foreach (var e in edges) Edges.Add(e);

            RaisePropertyChanged(nameof(TotalNodes));
            RaisePropertyChanged(nameof(TotalEdges));
        }

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

        private void DeleteEdge()
        {
            if (SelectedEdge == null) return;
            _mapRepository.DeleteEdgeAsync(SelectedEdge.EdgeId).GetAwaiter().GetResult();
            LoadData();
        }

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

    public class MapExportData
    {
        public List<MapNode> MapNodes { get; set; } = new();
        public List<MapEdge> MapEdges { get; set; } = new();
        public List<ChargeStation> ChargeStations { get; set; } = new();
        public List<MapLocationAlias> MapLocationAliases { get; set; } = new();
        public string MapId { get; set; } = "default";
        public string Version { get; set; } = "1.0";
        public DateTime ExportTime { get; set; }
    }
}
