using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

namespace ShuatiKing;

public partial class MainWindow : Window
{
    private readonly LibraryView library = new();
    private readonly LocalSettings settings;
    private readonly SettingsStore store;
    private ProfileView? profile;
    private ImportView? importView;
    private readonly string runningModel;
    private readonly LearningState learning;
    private readonly LearningStore learningStore;
    private PracticeView? practiceView;
    private WrongBookView? wrongView;
    private ExamView? examView;
    private ExamHistoryView? historyView;
    private bool selectingTab;
    private readonly bool canSaveLearning = true;

    public MainWindow() : this(new SettingsStore()) { }

    public MainWindow(SettingsStore store)
    {
        this.store = store;
        try { settings = store.Load(); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException)
        {
            settings = new();
            MessageBox.Show("无法读取本机设置，本次先使用默认设置。原文件会保留，直到你重新保存。", "读取设置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        if (settings.SelectedConfigurationId == Guid.Empty) settings.SelectedConfigurationId = settings.Configurations[0].Id;
        runningModel = settings.Configurations.Single(c => c.Id == settings.SelectedConfigurationId).Model.Trim();
        learningStore = new LearningStore(store.LearningPath);
        try { learning = learningStore.Load(); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { learning = new(); canSaveLearning = false; MessageBox.Show("学习进度读取失败，本次暂停写入以保留原文件。请备份并检查本地学习记录。", "读取学习进度失败"); }
        ThemeManager.Apply(settings.Theme);
        InitializeComponent();
        library.ActionRequested += HandleLibraryAction;
        RefreshProfile();
        library.UpdateProgress(learning.Practice);
        LibraryTab.IsChecked = true;
        SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        Closing += (_, _) => { examView?.PauseForNavigation(); SaveLearning(); };
        Closed += (_, _) => SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
    }

    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (PageHost is null || selectingTab) return;
        if (PageHost.Content is ExamView exam) exam.PauseForNavigation();
        if (PageHost.Content is ReviewView review) review.SaveDraft();
        SaveLearning();
        ProfileButton.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        var title = ((RadioButton)sender).Content.ToString()!;
        if (title == "题库") { library.UpdateProgress(learning.Practice); PageHost.Content = library; }
        else if (title == "刷题") ShowPractice();
        else if (title == "错题本") ShowWrongs();
        else if (title == "模拟考试") ShowExam();
        else if (title == "考试记录") ShowHistory();
        else ShowPlaceholder(title);
    }

    private void ProfileClick(object sender, RoutedEventArgs e)
    {
        if (PageHost.Content is ExamView exam) exam.PauseForNavigation();
        if (PageHost.Content is ReviewView review) review.SaveDraft();
        SaveLearning();
        foreach (var tab in NavigationTabs.Children.OfType<RadioButton>()) tab.IsChecked = false;
        profile ??= new ProfileView(settings, store, RefreshProfile, runningModel);
        PageHost.Content = profile;
        ProfileButton.SetResourceReference(BackgroundProperty, "TintBrush");
    }

    private void ShowPlaceholder(string title) => PageHost.Content = new TextBlock
    {
        Text = title + "页面将在对应步骤完成", FontSize = 24, Margin = new Thickness(36)
    };

    private void HandleLibraryAction(string action, SampleLibrary? selected)
    {
        if (action == "导入资料")
        {
            importView ??= new ImportView(ShowReview, library.AddLibrary, ShowLibrary);
            PageHost.Content = importView;
        }
        else if (action == "导入题库")
        {
            var dialog = new PackageDialog(added => { library.AddLibrary(added); ShowLibrary(); }) { Owner = this };
            dialog.ShowDialog();
        }
        else if (action == "模拟考试") ShowExam(selected);
        else if (action == "开始练习") ShowPractice(selected);
        else if (action == "继续练习") { ShowPractice(); practiceView!.ContinuePractice(); }
        else if (action == "题目管理" && selected is not null) ShowReview(selected);
        else ShowPlaceholder(action);
    }

    private void ShowLibrary() { library.UpdateProgress(learning.Practice); PageHost.Content = library; LibraryTab.IsChecked = true; }

    private void ShowPractice(SampleLibrary? bank = null)
    {
        SelectTab("刷题");
        practiceView ??= new PracticeView(learning, library.Banks, SaveLearning, ShowWrongs, () => settings.QuestionFontSize);
        practiceView.ShowSetup(bank); PageHost.Content = practiceView;
    }
    private void ShowWrongs()
    {
        if (PageHost.Content is ExamView exam) exam.PauseForNavigation();
        SelectTab("错题本");
        wrongView ??= new WrongBookView(learning, (id, name) => { ShowPractice(); practiceView!.StartWrong(id, name); }, () => ShowPractice());
        wrongView.Refresh(); PageHost.Content = wrongView;
    }
    private void SelectTab(string name)
    {
        selectingTab = true;
        foreach (var tab in NavigationTabs.Children.OfType<RadioButton>()) tab.IsChecked = tab.Content.ToString() == name;
        ProfileButton.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        selectingTab = false;
    }
    private void ShowExam(SampleLibrary? bank = null)
    {
        SelectTab("模拟考试");
        examView ??= new ExamView(learning, library.Banks, SaveLearning, ShowWrongs, () => settings.QuestionFontSize, ShowHistory);
        examView.ShowSetup(bank); PageHost.Content = examView;
    }
    private void ShowHistory()
    {
        if (PageHost.Content is ExamView exam) exam.PauseForNavigation();
        SelectTab("考试记录");
        historyView ??= new ExamHistoryView(learning, () => ShowExam(), () => settings.QuestionFontSize);
        historyView.Refresh(); PageHost.Content = historyView;
    }
    private void SaveLearning()
    {
        if (learningStore is null) return;
        if (!canSaveLearning) { Title = "刷题KING · 学习记录无法读取，当前进度未保存"; return; }
        try { learningStore.Save(learning); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { Title = "刷题KING · 学习进度保存失败，请检查本机目录权限"; }
    }

    private void ShowReview(SampleLibrary selected)
    {
        var origin = importView?.CurrentLibrary == selected ? importView : null;
        var review = new ReviewView(selected,
            () => { if (origin is not null) PageHost.Content = origin; else ShowLibrary(); },
            () => { if (origin is not null) importView = null; ShowLibrary(); },
            settings.QuestionFontSize, origin is not null);
        review.ExportRequested += bank => new PackageDialog(bank) { Owner = this }.ShowDialog();
        PageHost.Content = review;
    }

    private void RefreshProfile()
    {
        NicknameText.Text = settings.Nickname;
        AvatarImage.Source = ProfileView.AvatarSource(settings.Avatar);
    }

    private void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (settings.Theme == "跟随系统") Dispatcher.InvokeAsync(() => ThemeManager.Apply(settings.Theme));
    }
}
