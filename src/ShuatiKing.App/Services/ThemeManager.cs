using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ShuatiKing.Services;

public static class ThemeManager
{
    public static void Apply(string theme)
    {
        var dark = theme == "深色" || (theme == "跟随系统" && (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) == 0);
        var colors = dark
            ? new[] { "#101826", "#192438", "#EDF3FF", "#B6C5DC", "#36475F", "#397BFF", "#FFFFFF", "#203659", "#8EE5BA", "#F4C474", "#FFADB5", "#193D34", "#423421" }
            : new[] { "#FCFDFF", "#FFFFFF", "#182844", "#52647D", "#DCE4EF", "#075AF5", "#FFFFFF", "#EAF2FF", "#19704F", "#99620C", "#B12B37", "#DCF5E8", "#FFF1DA" };
        string[] keys = ["CanvasBrush", "SurfaceBrush", "TextBrush", "MutedBrush", "LineBrush", "AccentBrush", "AccentTextBrush", "TintBrush", "SuccessBrush", "WarningBrush", "ErrorBrush", "SuccessTintBrush", "WarningTintBrush"];
        for (var i = 0; i < keys.Length; i++) Application.Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
    }
}
