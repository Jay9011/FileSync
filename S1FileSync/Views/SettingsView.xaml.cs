using System.Windows;
using System.Windows.Controls;
using FileSyncApp.Core;
using S1FileSync.ViewModels;

namespace S1FileSync.Views
{
    public partial class SettingsView : Page
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel(new RemoteConnectionSMBService());
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
