using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;
using ShuatiKing.Services;

namespace ShuatiKing.Views;

public partial class PracticeView : UserControl
{
    private readonly LearningState state;
    private readonly IReadOnlyList<SampleLibrary> banks;
    private readonly Action save;
    private readonly Action openWrongs;
    private readonly Func<int> fontSize;
    private bool initializing = true;
    public PracticeView(LearningState state, IReadOnlyList<SampleLibrary> banks, Action save, Action openWrongs, Func<int> fontSize)
    {
        this.state = state; this.banks = banks; this.save = save; this.openWrongs = openWrongs; this.fontSize = fontSize;
        InitializeComponent(); initializing = false;
        SizeChanged += (_, _) => ApplySetupDensity(); Unloaded += (_, _) => RulesPopup.IsOpen = false;
        ShowSetup();
    }
    public void ShowSetup(SampleLibrary? bank = null)
    {
        Setup.Visibility = Visibility.Visible; SessionPanel.Visibility = CompletionPanel.Visibility = Visibility.Collapsed;
        BankPicker.ItemsSource = banks; BankPicker.SelectedItem = bank ?? banks.FirstOrDefault();
        ResumeCard.Visibility = state.Practice is null ? Visibility.Collapsed : Visibility.Visible;
        if (state.Practice is { } p)
        {
            ResumeText.Text = $"{p.BankName} · {p.Items.Count(i => i.Submitted)} / {p.Items.Count} 题";
            ResumeButton.Content = p.Complete ? "查看结果 ›" : "继续练习 ›";
        }
        Feedback.Text = ""; UpdateSetup(); ApplySetupDensity();
    }
    private string SelectedMode => WrongMode.IsChecked == true ? "错题重练" : RandomMode.IsChecked == true ? "随机练习" : "顺序练习";
    private void SetupChanged(object sender, RoutedEventArgs e)
    {
        if (initializing) return;
        Feedback.Text = ""; UpdateSetup();
    }
    private void UpdateSetup()
    {
        var bank = BankPicker.SelectedItem as SampleLibrary;
        var questions = bank?.Questions.Where(q => q.IsConfirmed && q.ValidationError is null).ToArray() ?? [];
        var wrongs = bank is null ? [] : state.Wrongs.Where(w => w.BankId == bank.Id).Select(w => w.Question).ToArray();
        BankSource.Text = bank is null ? "" : $"{bank.Source} · {questions.Length} 题";
        SequentialCount.Text = RandomCount.Text = $"{questions.Length} 题"; WrongCount.Text = $"{wrongs.Length} 题";
        var round = WrongMode.IsChecked == true ? wrongs : questions;
        ModeName.Text = SelectedMode; RoundCount.Text = round.Length.ToString();
        var labels = new[] { SingleCount, MultipleCount, JudgmentCount, BlankCount };
        for (var i = 0; i < labels.Length; i++) labels[i].Text = round.Count(q => (int)q.Type == i).ToString();
    }
    private void ApplySetupDensity()
    {
        if (Setup.Visibility != Visibility.Visible) return;
        bool compact = ActualHeight < 660 || ActualWidth < 1200;
        PageLayout.Margin = compact ? new Thickness(24, 16, 24, 16) : new Thickness(30, 24, 30, 20);
        PageHeading.FontSize = compact ? 28 : 32;
        BankToolbar.Margin = compact ? new Thickness(20, 16, 20, 16) : new Thickness(28, 24, 28, 24);
        BankPicker.Width = compact ? 300 : 380;
        ModesSection.Padding = new Thickness(compact ? 16 : 24);
        RoundSummarySection.Padding = new Thickness(compact ? 16 : 24);
        ModesHeading.Margin = new Thickness(0, 0, 0, compact ? 14 : 22);
        ModeChoices.RowDefinitions[1].Height = ModeChoices.RowDefinitions[3].Height = new GridLength(compact ? 12 : 16);
        RoundHeading.Margin = new Thickness(0, 0, 0, compact ? 10 : 20);
        RoundCount.FontSize = compact ? 32 : 44;
        ModeName.FontSize = compact ? 16 : 18;
        SummaryDivider.Margin = new Thickness(0, compact ? 12 : 20, 0, compact ? 12 : 20);
        TypeDistribution.Height = compact ? 88 : 112;
        SummaryActions.Margin = new Thickness(0, compact ? 12 : 24, 0, 0);
        StartButton.Height = compact ? 44 : 52;
        RulesButton.Margin = new Thickness(0, compact ? 6 : 14, 0, 0);
    }
    private void Start(object sender, RoutedEventArgs e)
    {
        if (BankPicker.SelectedItem is not SampleLibrary bank) { Feedback.Text = "请先选择题库。"; return; }
        var mode = SelectedMode;
        var session = LearningRules.StartPractice(bank, mode, state);
        if (session.Items.Count == 0) { Feedback.Text = mode == "错题重练" ? "这个题库暂时没有错题，可选择顺序或随机练习。" : "没有可练习题目，请先完成题目审核。"; return; }
        if (state.Practice is { Complete: false } && MessageBox.Show("新一轮将替换当前练习进度，已记录错题会保留。是否继续？", "开始新一轮", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        state.Practice = session; save(); ContinuePractice();
    }
    public void StartWrong(Guid bankId, string bankName)
    {
        var items = state.Wrongs.Where(w => w.BankId == bankId).Select(w => new AnswerItem { Question = LearningRules.Snapshot(w.Question) }).ToList();
        if (items.Count == 0) { ShowSetup(); Feedback.Text = "这些错题已经移出，可以选择其他题库。"; return; }
        if (state.Practice is { Complete: false } && MessageBox.Show("错题重练将替换当前练习进度，错题记录保留。是否继续？", "错题重练", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        state.Practice = new() { BankId = bankId, BankName = bankName, Mode = "错题重练", Items = items }; save(); ContinuePractice();
    }
    private void Continue(object sender, RoutedEventArgs e) => ContinuePractice();
    public void ContinuePractice()
    {
        if (state.Practice is not { } p) { ShowSetup(); return; }
        Setup.Visibility = Visibility.Collapsed; RulesPopup.IsOpen = false; Feedback.Text = "";
        PageLayout.Margin = new Thickness(30, 24, 30, 20); PageHeading.FontSize = 32;
        if (p.Complete)
        {
            SessionPanel.Visibility = Visibility.Collapsed; CompletionPanel.Visibility = Visibility.Visible;
            CompletionText.Text = $"共 {p.Items.Count} 题 · 答对 {p.Items.Count(i => i.Correct)} 题 · 答错 {p.Items.Count(i => !i.Correct)} 题。错题记录已更新。"; return;
        }
        CompletionPanel.Visibility = Visibility.Collapsed; SessionPanel.Visibility = Visibility.Visible; RenderQuestion();
    }
    private void RenderQuestion()
    {
        var p = state.Practice!; var item = p.Items[p.Index];
        QuestionHost.Content = new AnswerEditor(item, fontSize(), item.Submitted);
        SessionTitle.Text = p.BankName; SessionMode.Text = p.Mode; Position.Text = $"{p.Index + 1} / {p.Items.Count} 题";
        Progress.Maximum = p.Items.Count; Progress.Value = p.Items.Count(i => i.Submitted);
        CompletedCount.Text = $"已完成 {Progress.Value} 题 · 本机保存";
        SubmitButton.IsEnabled = !item.Submitted; NextButton.IsEnabled = item.Submitted;
        NextButton.Content = p.Index == p.Items.Count - 1 ? "完成本轮" : "下一题"; PreviousButton.IsEnabled = p.Index > 0;
        AnswerResult.Visibility = item.Submitted ? Visibility.Visible : Visibility.Collapsed;
        if (item.Submitted)
        {
            ResultTitle.Text = item.Correct ? "回答正确" : "回答错误 · 已加入错题本";
            ResultTitle.SetResourceReference(TextBlock.ForegroundProperty, item.Correct ? "SuccessBrush" : "ErrorBrush");
            CorrectAnswer.Text = "正确答案：" + LearningRules.CorrectText(item.Question);
            Explanation.Text = "解析：" + (string.IsNullOrWhiteSpace(item.Question.Explanation) ? "暂无解析" : item.Question.Explanation);
        }
    }
    private void Submit(object sender, RoutedEventArgs e)
    {
        var p = state.Practice!; var item = p.Items[p.Index]; if (item.Submitted) return;
        if (!item.Answered || (item.Question.Type == QuestionType.Blank && item.Blanks.Any(string.IsNullOrWhiteSpace)))
        { Feedback.Text = "请完成当前题目作答后再提交。"; return; }
        bool removed = LearningRules.Record(state, p.BankId, p.BankName, item);
        save(); RenderQuestion(); Dispatcher.InvokeAsync(() => AnswerResult.BringIntoView(), System.Windows.Threading.DispatcherPriority.Loaded); Feedback.Text = removed ? "回答正确，已从错题本移出；历史作答不受影响。" : "答案与进度已保存。";
    }
    private void Next(object sender, RoutedEventArgs e)
    {
        var p = state.Practice!; if (!p.Items[p.Index].Submitted) return;
        if (p.Index < p.Items.Count - 1) { p.Index++; save(); Feedback.Text = ""; RenderQuestion(); }
        else ContinuePractice();
    }
    private void Previous(object sender, RoutedEventArgs e) { var p = state.Practice!; if (p.Index > 0) { p.Index--; save(); Feedback.Text = ""; RenderQuestion(); } }
    private void Back(object sender, RoutedEventArgs e) { save(); ShowSetup(); }
    private void OpenWrongs(object sender, RoutedEventArgs e) => openWrongs();
}
