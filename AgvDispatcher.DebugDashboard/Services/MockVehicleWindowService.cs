using System.Windows;
using AgvDispatcher.DebugDashboard.ViewModels;
using AgvDispatcher.DebugDashboard.Views;

namespace AgvDispatcher.DebugDashboard.Services;

public sealed class MockVehicleWindowService : IMockVehicleWindowService
{
    private readonly IMockSimulationService _mockSimulationService;
    private readonly Dictionary<string, MockVehicleDetailWindow> _windows = new(StringComparer.OrdinalIgnoreCase);

    public MockVehicleWindowService(IMockSimulationService mockSimulationService)
    {
        _mockSimulationService = mockSimulationService;
    }

    public void ShowVehicle(string vehicleId)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            return;
        }

        if (_windows.TryGetValue(vehicleId, out var existing))
        {
            existing.Activate();
            return;
        }

        var viewModel = new MockVehicleDetailViewModel(vehicleId, _mockSimulationService);
        var window = new MockVehicleDetailWindow
        {
            DataContext = viewModel,
            Owner = System.Windows.Application.Current?.Windows.OfType<DebugDashboardWindow>().FirstOrDefault()
        };

        window.Closed += (_, _) =>
        {
            viewModel.Dispose();
            _windows.Remove(vehicleId);
        };

        _windows[vehicleId] = window;
        window.Show();
    }
}
