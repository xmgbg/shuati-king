using System.Windows;
using System.Windows.Controls;

namespace ShuatiKing;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LibraryTab.IsChecked = true;
    }

    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (PageHost is null) return;
        ShowPlaceholder(((RadioButton)sender).Tag.ToString()!);
    }

    private void ProfileClick(object sender, RoutedEventArgs e) => ShowPlaceholder("我的");

    private void ShowPlaceholder(string title) => PageHost.Content = new TextBlock
    {
        Text = title + "页面将在对应步骤完成", FontSize = 24, Margin = new Thickness(36)
    };
}
