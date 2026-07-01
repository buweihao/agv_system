using System.Windows.Controls;
using System.Windows.Input;

namespace AgvDispatcher.Modules.SystemConfigModule.Views
{
    public partial class MapConfigView : UserControl
    {
        public MapConfigView()
        {
            InitializeComponent();
        }

        private void RollbackVersionComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ComboBox comboBox)
            {
                return;
            }

            if (!comboBox.IsKeyboardFocusWithin)
            {
                comboBox.Focus();
            }

            if (comboBox.IsDropDownOpen)
            {
                return;
            }

            comboBox.IsDropDownOpen = true;
            e.Handled = true;
        }
    }
}
