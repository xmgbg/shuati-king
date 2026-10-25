using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;

namespace ShuatiKing.Views;

public partial class LibraryView : UserControl
{
    private readonly ObservableCollection<SampleLibrary> libraries = new(SampleLibrary.CreateExamples());
    public event Action<string, SampleLibrary?>? ActionRequested;
    public SampleLibrary? SelectedLibrary => LibraryList.SelectedItem as SampleLibrary;

    public LibraryView()
    {
        InitializeComponent();
        LibraryList.ItemsSource = libraries;
        LibraryList.SelectedIndex = 0;
    }

    public void AddLibrary(SampleLibrary added)
    {
        if (!libraries.Contains(added)) libraries.Add(added);
        LibraryList.SelectedItem = added;
    }

    private void PreviewAction(object sender, RoutedEventArgs e) => ActionRequested?.Invoke(((Button)sender).Tag.ToString()!, SelectedLibrary);
}
