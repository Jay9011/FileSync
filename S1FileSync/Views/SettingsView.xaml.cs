using System.Windows;
using System.Windows.Controls;
using NetConnectionHelper;
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

            this.Loaded += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_viewModel.Settings.Password))
                {
                    PasswordBox.Password = _viewModel.Settings.Password;
                }
            };
            
            // Settings가 변경되면 PasswordBox에도 반영
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.Settings))
                {
                    PasswordBox.Password = _viewModel.Settings.Password;
                }
            };
            
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
