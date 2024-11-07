using System.Windows.Controls;
using S1FileSync.ViewModels;

namespace S1FileSync.Views;

public partial class FileSyncProgressView : UserControl
{
    public FileSyncProgressView(FileSyncProgressViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}