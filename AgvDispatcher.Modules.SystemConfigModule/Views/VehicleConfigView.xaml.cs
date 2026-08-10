using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AgvDispatcher.Modules.SystemConfigModule.ViewModels;

namespace AgvDispatcher.Modules.SystemConfigModule.Views
{
    public partial class VehicleConfigView : UserControl
    {
        public VehicleConfigView()
        {
            InitializeComponent();
        }

        private void VehicleGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is null)
            {
                return;
            }

            if (DataContext is VehicleConfigViewModel viewModel && viewModel.OpenCommand.CanExecute())
            {
                viewModel.OpenCommand.Execute();
                e.Handled = true;
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
