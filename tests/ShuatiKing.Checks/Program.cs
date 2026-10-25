using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShuatiKing;
using ShuatiKing.Views;
using ShuatiKing.Services;
using ShuatiKing.Models;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        var settingsPath = Path.GetFullPath($".artifacts/check-settings-{Guid.NewGuid()}.dat");
        var store = new SettingsStore(settingsPath);
        var window = new MainWindow(store);
        window.Show();
        window.UpdateLayout();
        var host = (ContentControl)window.FindName("PageHost");
        var library = (LibraryView)host.Content;
        var list = (ListBox)library.FindName("LibraryList");
        foreach (var index in new[] { 0, 1, 2, 3 })
        {
            list.SelectedIndex = index;
            window.UpdateLayout();
            Check(library.SelectedLibrary?.Count == new[] { 128, 86, 64, 32 }[index], "选中题库数量正确");
        }
        Check(!library.SelectedLibrary!.IsReady, "待审核题库不可练习");
        list.SelectedIndex = 0;
        Capture(window, "home-light");
        window.Width = 1120; window.Height = 720;
        Capture(window, "home-compact");
        window.Width = 1440; window.Height = 900;
        ((Button)window.FindName("ProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
        var profile = (ProfileView)host.Content;
        ((TextBox)profile.FindName("NicknameInput")).Text = "小马";
        Descendants<Button>(profile).Single(b => b.Content?.ToString() == "保存个人资料").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(((TextBlock)window.FindName("NicknameText")).Text == "小马", "昵称同步顶部入口");
        ((RadioButton)profile.FindName("Font22")).IsChecked = true;
        Check(((TextBlock)profile.FindName("QuestionPreview")).FontSize == 22, "字号预览即时生效");
        ((RadioButton)profile.FindName("DarkTheme")).IsChecked = true;
        Check(store.Load().Theme == "深色" && store.Load().QuestionFontSize == 22, "主题和字号保存");
        Check(!File.ReadAllText(settingsPath).Contains("小马"), "个人设置加密保存");
        Capture(window, "profile-dark");
        ((RadioButton)profile.FindName("LightTheme")).IsChecked = true;
        Capture(window, "profile-light");
        window.Close();
        File.Delete(settingsPath);
        Console.WriteLine("通过：首页切换、窗口渲染、昵称同步、字号、深浅主题及加密设置往返。");
    }

    internal static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    internal static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Capture(Window window, string name)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var surface = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        Directory.CreateDirectory(".artifacts");
        using var file = File.Create($".artifacts/{name}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
    }
}
