using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;

namespace ShuatiKing.Views;

public partial class ExamHistoryView : UserControl
{
    private readonly LearningState state;
    private readonly Action openExam;
    private readonly Func<int> fontSize;
    private readonly ObservableCollection<ExamSession> records = [];
    public ExamHistoryView(LearningState state, Action openExam, Func<int> fontSize)
    {
        this.state = state; this.openExam = openExam; this.fontSize = fontSize;
        InitializeComponent(); HistoryList.ItemsSource = records; Refresh();
    }
    public void Refresh()
    {
        var selected = (HistoryList.SelectedItem as ExamSession)?.Id;
        records.Clear();
        foreach (var exam in state.ExamHistory.OrderByDescending(e => e.FinishedAt ?? e.StartedAt ?? DateTimeOffset.MinValue)) records.Add(exam);
        HistoryCount.Text = $"{records.Count} 场考试";
        EmptyPanel.Visibility = records.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryGrid.Visibility = records.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        HistoryList.SelectedItem = records.FirstOrDefault(e => e.Id == selected) ?? records.FirstOrDefault();
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => DetailHost.Content = HistoryList.SelectedItem is ExamSession exam ? new ExamResultView(exam, fontSize()) : null;
    private void NewExam(object sender, RoutedEventArgs e) => openExam();
}