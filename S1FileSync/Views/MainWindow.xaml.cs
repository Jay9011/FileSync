using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using S1FileSync.ViewModels;
using S1FileSync.Views;

namespace S1FileSync
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        
        public MainWindow(IServiceProvider serviceProvider, MainViewModel mainViewModel)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            DataContext = mainViewModel;
            
            var settingsView = _serviceProvider.GetService<SettingsView>();
            SettingsFrame.Navigate(settingsView);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}