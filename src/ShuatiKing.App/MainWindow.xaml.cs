using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Views;

namespace ShuatiKing;

public partial class MainWindow : Window
{
    private readonly LibraryView library = new();
    public MainWindow()
    {
        InitializeComponent();
        LibraryTab.IsChecked = true;
    }

    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (PageHost is null) return;
        var title = ((RadioButton)sender).Content.ToString()!;
        if (title == "题库") PageHost.Content = library;
        else ShowPlaceholder(title);
    }

    private void ProfileClick(object sender, RoutedEventArgs e)
    {
        foreach (var tab in NavigationTabs.Children.OfType<RadioButton>()) tab.IsChecked = false;
        ShowPlaceholder("我的");
    }

    private void ShowPlaceholder(string title) => PageHost.Content = new TextBlock
    {
        Text = title + "页面将在对应步骤完成", FontSize = 24, Margin = new Thickness(36)
    };
}
