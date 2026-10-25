using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ShuatiKing.Models;
using ShuatiKing.Services;

namespace ShuatiKing.Views;

public partial class ProfileView : UserControl
{
    private readonly LocalSettings settings;
    private readonly SettingsStore store;
    private readonly Action profileUpdated;
    private bool initializing = true;
    private string? avatar;

    public ProfileView(LocalSettings settings, SettingsStore store, Action profileUpdated, string runningModel)
    {
        this.settings = settings;
        this.store = store;
        this.profileUpdated = profileUpdated;
        InitializeComponent();
        AiHost.Content = new AiSettingsView(settings, store, runningModel);
        NicknameInput.Text = settings.Nickname;
        avatar = settings.Avatar;
        UpdateAvatar();
        (settings.Theme switch { "深色" => DarkTheme, "跟随系统" => SystemTheme, _ => LightTheme }).IsChecked = true;
        ((RadioButton)FindName("Font" + settings.QuestionFontSize)).IsChecked = true;
        QuestionPreview.FontSize = settings.QuestionFontSize;
        initializing = false;
    }

    private void ChooseAvatar(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "选择头像", Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp" };
        if (picker.ShowDialog() != true) return;
        try
        {
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 256;
            image.UriSource = new Uri(picker.FileName); image.EndInit(); image.Freeze();
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var data = new MemoryStream(); encoder.Save(data);
            avatar = Convert.ToBase64String(data.ToArray());
            UpdateAvatar(); ProfileFeedback.Text = "头像已选择，请保存个人资料。";
        }
        catch (Exception error) when (error is IOException or NotSupportedException or ArgumentException or System.IO.FileFormatException)
        { ProfileFeedback.Text = "无法读取这张图片，请选择有效的 PNG、JPG 或 BMP。"; }
    }

    private void ResetAvatar(object sender, RoutedEventArgs e)
    { avatar = null; UpdateAvatar(); ProfileFeedback.Text = "已恢复默认头像，请保存个人资料。"; }

    public static BitmapImage AvatarSource(string? avatar)
    {
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        if (avatar is null) image.UriSource = new Uri("pack://application:,,,/ShuatiKing;component/Assets/logo.png");
        else image.StreamSource = new MemoryStream(Convert.FromBase64String(avatar));
        image.EndInit(); image.Freeze(); image.StreamSource?.Dispose();
        return image;
    }

    private void UpdateAvatar() => ProfileAvatar.Source = AvatarSource(avatar);

    private void SaveProfile(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NicknameInput.Text))
        { ProfileFeedback.Text = "请填写昵称后再保存。"; NicknameInput.Focus(); return; }
        var previous = (settings.Nickname, settings.Avatar);
        settings.Nickname = NicknameInput.Text.Trim(); settings.Avatar = avatar;
        if (!TrySave(out var error)) { (settings.Nickname, settings.Avatar) = previous; ProfileFeedback.Text = error; return; }
        NicknameInput.Text = settings.Nickname; profileUpdated(); ProfileFeedback.Text = "个人资料已保存到本机。";
    }

    private bool TrySave(out string error)
    {
        try { store.Save(settings); error = ""; return true; }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        { error = "保存失败，请检查本地设置目录的写入权限后重试。"; return false; }
    }

    private void ThemeChanged(object sender, RoutedEventArgs e)
    {
        if (initializing) return;
        var previous = settings.Theme;
        settings.Theme = ((RadioButton)sender).Tag.ToString()!;
        if (!TrySave(out var error))
        {
            settings.Theme = previous;
            initializing = true;
            (previous switch { "深色" => DarkTheme, "跟随系统" => SystemTheme, _ => LightTheme }).IsChecked = true;
            initializing = false;
            PreferenceFeedback.Text = error; return;
        }
        ThemeManager.Apply(settings.Theme); PreferenceFeedback.Text = "主题已保存，即时生效。";
    }

    private void FontChanged(object sender, RoutedEventArgs e)
    {
        if (initializing) return;
        var previous = settings.QuestionFontSize;
        settings.QuestionFontSize = int.Parse(((RadioButton)sender).Tag.ToString()!);
        if (!TrySave(out var error))
        {
            settings.QuestionFontSize = previous;
            initializing = true;
            ((RadioButton)FindName("Font" + previous)).IsChecked = true;
            initializing = false;
            PreferenceFeedback.Text = error; return;
        }
        QuestionPreview.FontSize = settings.QuestionFontSize; PreferenceFeedback.Text = "题目字号已保存。";
    }
}
