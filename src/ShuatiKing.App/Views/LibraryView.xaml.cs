using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;

namespace ShuatiKing.Views;

public partial class LibraryView : UserControl
{
    private readonly ObservableCollection<SampleLibrary> libraries = new(SampleLibrary.CreateExamples());
    public IReadOnlyList<SampleLibrary> Banks => libraries;
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

    public void UpdateProgress(PracticeSession? session)
    {
        ResumeBank.Text = session?.BankName ?? "选择题库，开始一轮练习";
        ResumeFraction.Text = session is null ? "尚未开始" : $"{session.Items.Count(i => i.Submitted)} / {session.Items.Count} 题";
        ResumeProgress.Maximum = Math.Max(1, session?.Items.Count ?? 0);
        ResumeProgress.Value = session?.Items.Count(i => i.Submitted) ?? 0;
        ResumeAction.Content = session is null ? "开始练习" : "继续练习";
        ResumeAction.Tag = session is null ? "开始练习" : "继续练习";
    }

    private void PreviewAction(object sender, RoutedEventArgs e) => ActionRequested?.Invoke(((Button)sender).Tag.ToString()!, SelectedLibrary);
}
