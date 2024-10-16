using System.Windows;
using System.Windows.Controls;
using NetConnectionService;
using S1FileSync.ViewModels;

namespace S1FileSync.Views
{
    public partial class SettingsView : Page
    {
        private readonly SettingsViewModel _viewModel;
        
        public SettingsView(SettingsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            PasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.Settings.Password = PasswordBox.Password;
            }
        }
    }
}
