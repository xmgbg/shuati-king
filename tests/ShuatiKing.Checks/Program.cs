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
    private static int assertions;
    private static bool screenshots;
    private static readonly string checksDirectory = Path.GetFullPath(".artifacts/checks");
    private static readonly string runDirectory = Path.Combine(checksDirectory, Guid.NewGuid().ToString("N"));

    internal static string TestPath(string name)
    {
        Directory.CreateDirectory(runDirectory);
        return Path.Combine(runDirectory, name);
    }

    private static string ScreenshotPath(string name)
    {
        var directory = Path.GetFullPath(".scratch/screenshots");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name + ".png");
    }
    [STAThread]
    private static void Main(string[] args)
    {
        screenshots = args.Contains("--screenshots");
        try { Run(args); }
        finally
        {
            if (Directory.Exists(runDirectory) &&
                string.Equals(Path.GetDirectoryName(runDirectory), checksDirectory, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(runDirectory, recursive: true);
        }
    }

    private static void Run(string[] args)
    {
        if (args.Contains("--preview-history")) { InteractivePreview.Run(); return; }
        QuestionChecks.Run();
        LearningChecks.Models();
        ExamHistoryChecks.Models();
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var settingsPath = Program.TestPath("check-settings.dat");
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
        var profileScroll = Descendants<ScrollViewer>(profile).First();
        profileScroll.ScrollToBottom(); window.UpdateLayout();
        Capture(window, "profile-connection-light");
        CaptureFullProfile((FrameworkElement)profileScroll.Content, "profile-full-light");
        ((RadioButton)profile.FindName("DarkTheme")).IsChecked = true;
        Capture(window, "profile-connection-dark");
        ((RadioButton)profile.FindName("LightTheme")).IsChecked = true;
        profileScroll.ScrollToTop();
        window.Height = 1150;
        Capture(window, "profile-complete-light");
        ((RadioButton)profile.FindName("DarkTheme")).IsChecked = true;
        Capture(window, "profile-complete-dark");
        window.Width = 1120; window.Height = 720;
        Capture(window, "profile-compact");
        profileScroll.ScrollToBottom(); window.UpdateLayout();
        Check(profileScroll.VerticalOffset > 0, "小窗口可滚动至下方连接结果");
        Capture(window, "profile-compact-bottom");
        ((RadioButton)profile.FindName("LightTheme")).IsChecked = true;
        ImportChecks.Run(window);
        ReviewChecks.Run(window);
        WorkflowChecks.Run(window);
        PracticeChecks.Run(window);
        PracticeLayoutChecks.Run(window);
        ExamChecks.Run(window);
        ExamTypeChecks.Run(window);
        ExamLayoutChecks.Run(window);
        ExamHistoryChecks.Views(window);
        window.Close();
        var withAvatar = store.Load();
        withAvatar.Avatar = Convert.ToBase64String(File.ReadAllBytes("assets/logo.png"));
        store.Save(withAvatar);
        var reopened = new MainWindow(store);
        reopened.Show();
        ((Button)reopened.FindName("ProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        reopened.UpdateLayout();
        var restoredProfile = (ProfileView)((ContentControl)reopened.FindName("PageHost")).Content;
        var restoredAi = (AiSettingsView)((ContentControl)restoredProfile.FindName("AiHost")).Content;
        Check(((Border)restoredAi.FindName("RestartNotice")).Visibility == Visibility.Collapsed, "重开后模型生效且不再提示重启");
        Check(((TextBlock)reopened.FindName("NicknameText")).Text == "小马", "重开恢复昵称");
        Check(((System.Windows.Media.Imaging.BitmapImage)((Image)restoredProfile.FindName("ProfileAvatar")).Source).PixelWidth > 0, "重开后读取本地头像图片");
        Descendants<Button>(restoredProfile).Single(b => b.Content?.ToString() == "恢复默认").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Descendants<Button>(restoredProfile).Single(b => b.Content?.ToString() == "保存个人资料").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(store.Load().Avatar is null, "恢复默认头像后保存");
        reopened.Close();
        File.Delete(settingsPath);
        File.Delete(store.LearningPath);
        LearningNavigationChecks.Run();
        EndToEndChecks.Run();
        Console.WriteLine($"通过 {assertions} 项校验：首页、个人设置、导入审核、练习错题与模拟考试。");
    }

    private static void CaptureFullProfile(FrameworkElement content, string name)
    {
        if (!screenshots) return;
        var width = (int)Math.Ceiling(content.ActualWidth);
        var height = (int)Math.Ceiling(content.ActualHeight);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle((Brush)Application.Current.FindResource("CanvasBrush"), null, new Rect(0, 0, width, height));
            context.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.Fill }, null, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        using var file = File.Create(ScreenshotPath(name));
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
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
        assertions++;
    }

    internal static void Capture(Window window, string name)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        if (!screenshots) return;
        var surface = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        using var file = File.Create(ScreenshotPath(name));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
    }
}
