using System.Windows;
using System.Windows.Controls;
using NetConnectionHelper;
using S1FileSync.ViewModels;

namespace S1FileSync.Views
{
    public partial class SettingsView : Page
    {
        #region 의존 주입

        private readonly SettingsViewModel _viewModel;

        #endregion
        
        public SettingsView(SettingsViewModel viewModel)
        {
            InitializeComponent();

            #region 의존 주입

            _viewModel = viewModel;

            #endregion
           
            DataContext = _viewModel;

            this.Loaded += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_viewModel.Settings.Password))
                {
                    PasswordBox.Password = _viewModel.Settings.Password;
                }
            };

            this.DataContextChanged += (s, e) =>
            {
                if (e.NewValue is SettingsViewModel vm)
                {
                    PasswordBox.Password = vm.Settings.Password;
                }
            };

            PasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
        }
        
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _viewModel.Settings.Password = PasswordBox.Password;
        }
    }
}
