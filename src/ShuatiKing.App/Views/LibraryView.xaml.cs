using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;

namespace ShuatiKing.Views;

public partial class LibraryView : UserControl
{
    public SampleLibrary? SelectedLibrary => LibraryList.SelectedItem as SampleLibrary;

    public LibraryView()
    {
        InitializeComponent();
        LibraryList.ItemsSource = SampleLibrary.CreateExamples();
        LibraryList.SelectedIndex = 0;
    }

    private void PreviewAction(object sender, RoutedEventArgs e) =>
        MessageBox.Show($"“{((Button)sender).Tag}”将在后续页面步骤实现。当前为题库首页预览。", "界面预览", MessageBoxButton.OK, MessageBoxImage.Information);
}
