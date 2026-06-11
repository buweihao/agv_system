using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel;
using System.Windows.Data;

namespace AgvDispatcher.Modules.SystemConfigModule.ViewModels
{
    public class MapConfigViewModel : BindableBase
    {
        private readonly IMapRepository _mapRepository;
        
        public ObservableCollection<MapNode> Nodes { get; } = new();
        public ObservableCollection<MapEdge> Edges { get; } = new();

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

        public int TotalNodes => Nodes.Count;
        public int TotalEdges => Edges.Count;

        public MapConfigViewModel(IMapRepository mapRepository)
        {
            _mapRepository = mapRepository;

            RefreshCommand = new DelegateCommand(LoadData);

            AddNodeCommand = new DelegateCommand(AddNode);
            SaveNodeCommand = new DelegateCommand(SaveNode, () => SelectedNode != null).ObservesProperty(() => SelectedNode);
            DeleteNodeCommand = new DelegateCommand(DeleteNode, () => SelectedNode != null).ObservesProperty(() => SelectedNode);

            AddEdgeCommand = new DelegateCommand(AddEdge);
            SaveEdgeCommand = new DelegateCommand(SaveEdge, () => SelectedEdge != null).ObservesProperty(() => SelectedEdge);
            DeleteEdgeCommand = new DelegateCommand(DeleteEdge, () => SelectedEdge != null).ObservesProperty(() => SelectedEdge);

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
            _mapRepository.SaveNodeAsync(SelectedNode).GetAwaiter().GetResult();
            LoadData();
        }

        private void DeleteNode()
        {
            if (SelectedNode == null) return;
            _mapRepository.DeleteNodeAsync(SelectedNode.NodeId).GetAwaiter().GetResult();
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
            _mapRepository.SaveEdgeAsync(SelectedEdge).GetAwaiter().GetResult();
            LoadData();
        }

        private void DeleteEdge()
        {
            if (SelectedEdge == null) return;
            _mapRepository.DeleteEdgeAsync(SelectedEdge.EdgeId).GetAwaiter().GetResult();
            LoadData();
        }
    }
}
