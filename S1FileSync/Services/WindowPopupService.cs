using System.Windows;
using S1FileSync.Services.Interface;

namespace S1FileSync.Services;

public class WindowPopupService : IPopupService
{
    public void ShowMessage(string message, string title = "Warning", object? buttons = null, object? icon = null)
    {
        if (buttons != null && buttons is not MessageBoxButton)
        {
            buttons = MessageBoxButton.OK;
        }
        
        if (icon != null && icon is not MessageBoxImage)
        {
            icon = MessageBoxImage.None;
        }
        
        MessageBox.Show(message, title, (MessageBoxButton)buttons, (MessageBoxImage)icon);
    }
}