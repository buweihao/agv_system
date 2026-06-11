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

        public DelegateCommand RefreshCommand { get; }
        
        public int TotalNodes => Nodes.Count;
        public int TotalEdges => Edges.Count;

        public MapConfigViewModel(IMapRepository mapRepository)
        {
            _mapRepository = mapRepository;

            RefreshCommand = new DelegateCommand(LoadData);

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
    }
}
