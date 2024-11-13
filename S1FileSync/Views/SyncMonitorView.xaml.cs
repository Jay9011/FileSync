using System.Windows.Controls;
using S1FileSync.ViewModels;

namespace S1FileSync.Views
{
    public partial class SyncMonitorView : UserControl
    {
        public SyncMonitorView(MainViewModel mainViewModel)
        {
            InitializeComponent();
            DataContext = mainViewModel;
        }
    }
}
