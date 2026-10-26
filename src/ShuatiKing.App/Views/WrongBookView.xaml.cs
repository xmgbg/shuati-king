using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;

namespace ShuatiKing.Views;

public partial class WrongBookView : UserControl
{
    private readonly LearningState state;
    private readonly Action<Guid, string> retry;
    private bool refreshing;
    private sealed record BankChoice(Guid Id, string Name);
    public WrongBookView(LearningState state, Action<Guid, string> retry)
    { this.state = state; this.retry = retry; InitializeComponent(); Refresh(); }
    public void Refresh()
    {
        var selected = BankFilter.SelectedItem as BankChoice;
        refreshing = true;
        var banks = state.Wrongs.GroupBy(w => w.BankId).Select(g => new BankChoice(g.Key, g.First().BankName)).ToList();
        banks.Insert(0, new(Guid.Empty, "全部题库")); BankFilter.ItemsSource = banks;
        BankFilter.SelectedItem = banks.Find(b => b.Id == selected?.Id) ?? banks[0];
        refreshing = false; UpdateList();
    }
    private void FilterChanged(object sender, SelectionChangedEventArgs e) { if (!refreshing) UpdateList(); }
    private void UpdateList()
    {
        if (WrongList is null) return;
        var bank = BankFilter.SelectedItem as BankChoice;
        var shown = state.Wrongs.Where(w => bank is null || bank.Id == Guid.Empty || bank.Id == w.BankId).ToList();
        WrongList.ItemsSource = shown; WrongList.SelectedIndex = shown.Count > 0 ? 0 : -1;
        WrongCount.Text = $"共 {shown.Count} 道错题"; RetryButton.IsEnabled = shown.Count > 0;
        EmptyState.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Retry(object sender, RoutedEventArgs e)
    { if (WrongList.SelectedItem is WrongEntry entry) retry(entry.BankId, entry.BankName); }
}
