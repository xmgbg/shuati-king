using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;
using ShuatiKing.Services;

namespace ShuatiKing.Views;

public partial class AiSettingsView : UserControl
{
    private readonly LocalSettings settings;
    private readonly SettingsStore store;
    private readonly string runningModel;
    private readonly ObservableCollection<AiConfiguration> drafts;
    private bool loading = true;
    private bool checking;
    private readonly AiConnectionService connection = new();
    private CancellationTokenSource? checkCancellation;
    private AiConfiguration Selected => (AiConfiguration)ConfigurationTabs.SelectedItem;

    public AiSettingsView(LocalSettings settings, SettingsStore store, string runningModel)
    {
        this.settings = settings;
        this.store = store;
        this.runningModel = runningModel;
        drafts = new(settings.Configurations.Select(c => c.Copy()));
        InitializeComponent();
        ConfigurationTabs.ItemsSource = drafts;
        ConfigurationTabs.SelectedIndex = Math.Max(0, drafts.ToList().FindIndex(c => c.Id == settings.SelectedConfigurationId));
        loading = false;
        LoadSelected();
        Unloaded += (_, _) => checkCancellation?.Cancel();
    }

    private void ConfigurationSelected(object sender, SelectionChangedEventArgs e)
    { if (!loading && ConfigurationTabs.SelectedItem is not null) LoadSelected(); }

    private void LoadSelected()
    {
        loading = true;
        NameInput.Text = Selected.Name; EndpointInput.Text = Selected.Endpoint;
        KeyInput.Password = Selected.ApiKey; ModelInput.Text = Selected.Model;
        loading = false;
        ConfigurationFeedback.Text = "";
        ResetConnection(); UpdateSummary();
    }

    private void ConfigurationEdited(object sender, RoutedEventArgs e)
    {
        if (loading || ConfigurationTabs.SelectedItem is null) return;
        Selected.Name = NameInput.Text; Selected.Endpoint = EndpointInput.Text;
        Selected.ApiKey = KeyInput.Password; Selected.Model = ModelInput.Text;
        ConfigurationFeedback.Text = "有未保存的修改。";
        ResetConnection();
    }

    private void ResetConnection()
    {
        if (ConnectionResult is null) return;
        ConnectionResult.Visibility = Visibility.Collapsed;
        CheckButton.IsEnabled = ValidationError() is null && !checking;
    }

    private string? ValidationError()
    {
        if (string.IsNullOrWhiteSpace(NameInput.Text)) return "请填写配置名称，便于区分三套配置。";
        try { AiConnectionService.CompletionUri(EndpointInput.Text); }
        catch (ArgumentException error) { return error.Message; }
        if (string.IsNullOrWhiteSpace(KeyInput.Password)) return "请填写 API 密钥。";
        if (string.IsNullOrWhiteSpace(ModelInput.Text)) return "请填写模型名称。";
        return null;
    }

    private bool Persist()
    {
        try { store.Save(settings); return true; }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        { ConfigurationFeedback.Text = "保存失败，请检查本地设置目录的写入权限后重试。"; return false; }
    }

    private void AddConfiguration(object sender, RoutedEventArgs e)
    {
        if (drafts.Count >= 3 || checking) return;
        var configuration = new AiConfiguration { Name = $"配置 {drafts.Count + 1}" };
        settings.Configurations.Add(configuration);
        if (!Persist()) { settings.Configurations.Remove(configuration); return; }
        drafts.Add(configuration.Copy());
        ConfigurationTabs.SelectedIndex = drafts.Count - 1;
        UpdateSummary();
    }

    private bool SaveSelected()
    {
        if (ValidationError() is { } error) { ConfigurationFeedback.Text = error; return false; }
        var index = settings.Configurations.FindIndex(c => c.Id == Selected.Id);
        var previous = settings.Configurations[index];
        var saved = Selected.Copy();
        saved.Name = saved.Name.Trim(); saved.Endpoint = saved.Endpoint.Trim(); saved.Model = saved.Model.Trim(); saved.ApiKey = saved.ApiKey.Trim();
        settings.Configurations[index] = saved;
        if (!Persist()) { settings.Configurations[index] = previous; return false; }
        Selected.Name = saved.Name; Selected.Endpoint = saved.Endpoint; Selected.Model = saved.Model;
        ConfigurationTabs.Items.Refresh(); UpdateSummary();
        ConfigurationFeedback.Text = "配置已加密保存到本机。";
        return true;
    }

    private void SaveConfiguration(object sender, RoutedEventArgs e) => SaveSelected();

    private void UseConfiguration(object sender, RoutedEventArgs e)
    {
        if (!SaveSelected()) return;
        var previous = settings.SelectedConfigurationId;
        settings.SelectedConfigurationId = Selected.Id;
        if (!Persist()) { settings.SelectedConfigurationId = previous; return; }
        UpdateSummary();
        ConfigurationFeedback.Text = "已保存当前配置选择。";
    }

    private void UpdateSummary()
    {
        var current = settings.Configurations.Single(c => c.Id == settings.SelectedConfigurationId);
        ConfigurationCount.Text = $"{drafts.Count} / 3 套";
        AddButton.IsEnabled = drafts.Count < 3 && !checking;
        AddButton.ToolTip = drafts.Count == 3 ? "已达到三套上限，可直接编辑已有配置。" : "新增一套 AI 配置";
        ActiveConfiguration.Text = $"当前选用：{current.Name} · {(string.IsNullOrWhiteSpace(current.Model) ? "未设置模型" : current.Model)}";
        RunningModel.Text = $"本次启动模型：{(string.IsNullOrWhiteSpace(runningModel) ? "未设置" : runningModel)}";
        RestartNotice.Visibility = current.Model.Trim() != runningModel ? Visibility.Visible : Visibility.Collapsed;
        UseConfigurationButton.Content = Selected.Id == current.Id ? "已选为当前" : "设为当前配置";
        UseConfigurationButton.IsEnabled = Selected.Id != current.Id;
    }

    private async void CheckConnection(object sender, RoutedEventArgs e) => await CheckConnectionAsync();

    public async Task CheckConnectionAsync()
    {
        if (checking) return;
        if (ValidationError() is { } error) { ConfigurationFeedback.Text = error; return; }
        var configuration = Selected.Copy();
        checking = true;
        using var cancellation = new CancellationTokenSource();
        checkCancellation = cancellation;
        Editor.IsEnabled = ConfigurationTabs.IsEnabled = AddButton.IsEnabled = CheckButton.IsEnabled = false;
        ConnectionResult.Visibility = CheckingProgress.Visibility = Visibility.Visible;
        ConnectionFeedback.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        ConnectionFeedback.Text = "正在检查模型连接…";
        CheckButton.Content = "正在检查…";
        try
        {
            var result = await connection.CheckAsync(configuration, cancellation.Token);
            ConnectionFeedback.Text = result.Message;
            ConnectionFeedback.SetResourceReference(TextBlock.ForegroundProperty, result.Success ? "SuccessBrush" : "ErrorBrush");
        }
        catch (OperationCanceledException) { ConnectionResult.Visibility = Visibility.Collapsed; }
        finally
        {
            checkCancellation = null;
            CheckingProgress.Visibility = Visibility.Collapsed;
            checking = false;
            Editor.IsEnabled = ConfigurationTabs.IsEnabled = true;
            CheckButton.IsEnabled = ValidationError() is null;
            CheckButton.Content = "检查连接";
            UpdateSummary();
        }
    }
}
