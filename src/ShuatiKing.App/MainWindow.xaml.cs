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
        ThemeManager.Apply(settings.Theme);
        InitializeComponent();
        library.ActionRequested += HandleLibraryAction;
        RefreshProfile();
        LibraryTab.IsChecked = true;
        SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        Closed += (_, _) => SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
    }

    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (PageHost is null) return;
        if (PageHost.Content is ReviewView review) review.SaveDraft();
        ProfileButton.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        var title = ((RadioButton)sender).Content.ToString()!;
        if (title == "题库") PageHost.Content = library;
        else ShowPlaceholder(title);
    }

    private void ProfileClick(object sender, RoutedEventArgs e)
    {
        if (PageHost.Content is ReviewView review) review.SaveDraft();
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
        else if (action == "题目管理" && selected is not null) ShowReview(selected);
        else ShowPlaceholder(action);
    }

    private void ShowLibrary() { PageHost.Content = library; LibraryTab.IsChecked = true; }

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
