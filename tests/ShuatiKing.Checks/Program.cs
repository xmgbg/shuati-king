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
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
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
        var ai = (AiSettingsView)((ContentControl)profile.FindName("AiHost")).Content;
        var tabs = (ListBox)ai.FindName("ConfigurationTabs");
        var add = (Button)ai.FindName("AddButton");
        Check(!((Button)ai.FindName("CheckButton")).IsEnabled, "未填完整不能检查连接");
        Fill(ai, "日常出题", "model-one");
        Click(ai, "SaveConfigurationButton");
        Check(store.Load().Configurations[0].ApiKey == "test-secret-only", "密钥加密后可恢复");
        Check(!File.ReadAllText(settingsPath).Contains("test-secret-only"), "磁盘中没有明文密钥");
        Check(((Border)ai.FindName("RestartNotice")).Visibility == Visibility.Visible, "当前模型变更提示重启");
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Fill(ai, "备用服务", "model-two"); Click(ai, "SaveConfigurationButton");
        Check(store.Load().SelectedConfigurationId != ((AiConfiguration)tabs.SelectedItem).Id, "保存备用配置不自动切换");
        Click(ai, "UseConfigurationButton");
        Check(store.Load().SelectedConfigurationId == ((AiConfiguration)tabs.SelectedItem).Id, "选择当前配置保存");
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Fill(ai, "长文资料", "model-three"); Click(ai, "SaveConfigurationButton");
        Check(tabs.Items.Count == 3 && !add.IsEnabled, "最多三套配置");
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(tabs.Items.Count == 3, "不能新增第四套");
        tabs.SelectedIndex = 1;
        var operation = ai.PreviewConnectionAsync();
        Check(!((Button)ai.FindName("CheckButton")).IsEnabled && ((TextBlock)ai.FindName("ConnectionFeedback")).Text.Contains("正在检查"), "正在检查状态与防重复点击");
        Check(ai.PreviewConnectionAsync().IsCompleted, "重复检查不会启动新任务");
        Complete(operation, window.Dispatcher);
        Check(((TextBlock)ai.FindName("ConnectionFeedback")).Text.Contains("HTTP 200"), "成功状态");
        ((TextBox)ai.FindName("ModelInput")).Text = "model-two-edited";
        Check(((Border)ai.FindName("ConnectionResult")).Visibility == Visibility.Collapsed, "修改后旧结果失效");
        var result = (ComboBox)ai.FindName("PreviewResult");
        result.SelectedIndex = 1;
        Complete(ai.PreviewConnectionAsync(), window.Dispatcher);
        Check(((TextBlock)ai.FindName("ConnectionFeedback")).Text.Contains("HTTP 401"), "失败状态含状态码");
        result.SelectedIndex = 5;
        Complete(ai.PreviewConnectionAsync(), window.Dispatcher);
        Check(((TextBlock)ai.FindName("ConnectionFeedback")).Text.Contains("LOCAL_TIMEOUT（本地错误码）"), "无响应显示本地错误码");
        Click(ai, "SaveConfigurationButton");
        window.Height = 1150;
        Capture(window, "profile-complete-light");
        ((RadioButton)profile.FindName("DarkTheme")).IsChecked = true;
        Capture(window, "profile-complete-dark");
        window.Width = 1120; window.Height = 720;
        Capture(window, "profile-compact");
        ((RadioButton)profile.FindName("LightTheme")).IsChecked = true;
        window.Close();
        var reopened = new MainWindow(store);
        reopened.Show();
        ((Button)reopened.FindName("ProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        reopened.UpdateLayout();
        var restoredProfile = (ProfileView)((ContentControl)reopened.FindName("PageHost")).Content;
        var restoredAi = (AiSettingsView)((ContentControl)restoredProfile.FindName("AiHost")).Content;
        Check(((Border)restoredAi.FindName("RestartNotice")).Visibility == Visibility.Collapsed, "重开后模型生效且不再提示重启");
        Check(((TextBlock)reopened.FindName("NicknameText")).Text == "小马", "重开恢复昵称");
        reopened.Close();
        File.Delete(settingsPath);
        Console.WriteLine("通过：首页切换、个人设置恢复、三配置上限、模型重启提示、连接状态和深浅主题渲染。");
    }

    private static void Fill(AiSettingsView ai, string name, string model)
    {
        ((TextBox)ai.FindName("NameInput")).Text = name;
        ((TextBox)ai.FindName("EndpointInput")).Text = "https://api.example.com/v1";
        ((PasswordBox)ai.FindName("KeyInput")).Password = "test-secret-only";
        ((TextBox)ai.FindName("ModelInput")).Text = model;
    }

    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void Complete(Task operation, Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        operation.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        operation.GetAwaiter().GetResult();
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
