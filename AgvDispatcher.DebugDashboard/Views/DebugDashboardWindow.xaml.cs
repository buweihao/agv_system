using AgvDispatcher.DebugDashboard.ViewModels;
using System.Windows;

namespace AgvDispatcher.DebugDashboard.Views;

public partial class DebugDashboardWindow : Window
{
    public DebugDashboardWindow(DebugDashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }
}
