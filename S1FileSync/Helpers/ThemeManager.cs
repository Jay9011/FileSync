using System.Windows;

namespace S1FileSync.Helpers;

public enum Theme
{
    Light,
    Dark
}

public static class ThemeManager
{
    private static readonly Uri LightThemeUri = new("/S1FileSync;component/Views/Theme/LightTheme.xaml", UriKind.Relative);
    private static readonly Uri DarkThemeUri = new("/S1FileSync;component/Views/Theme/DarkTheme.xaml", UriKind.Relative);

    public static void ChangeTheme(Theme theme)
    {
        var oldTheme = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && 
            (d.Source == LightThemeUri || d.Source == DarkThemeUri));

        if (oldTheme != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(oldTheme);
        }
        
        var newTheme = new ResourceDictionary
        {
            Source = theme switch
            {
                Theme.Light => LightThemeUri,
                Theme.Dark => DarkThemeUri,
                _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null)
            }
        };
        
        Application.Current.Resources.MergedDictionaries.Add(newTheme);
    }
}