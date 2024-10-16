namespace S1FileSync.Services.Interface;

public interface IPopupService
{
    void ShowMessage(string message, string title = "Warning", object? buttons = null, object? icon = null);
}