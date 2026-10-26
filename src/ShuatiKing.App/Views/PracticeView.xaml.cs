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
    public PracticeView(LearningState state, IReadOnlyList<SampleLibrary> banks, Action save, Action openWrongs, Func<int> fontSize)
    {
        this.state = state; this.banks = banks; this.save = save; this.openWrongs = openWrongs; this.fontSize = fontSize;
        InitializeComponent(); ShowSetup();
    }
    public void ShowSetup(SampleLibrary? bank = null)
    {
        Setup.Visibility = Visibility.Visible; SessionPanel.Visibility = CompletionPanel.Visibility = Visibility.Collapsed;
        BankPicker.ItemsSource = banks; BankPicker.SelectedItem = bank ?? banks.FirstOrDefault();
        ResumeCard.Visibility = state.Practice is null ? Visibility.Collapsed : Visibility.Visible;
        if (state.Practice is { } p) ResumeText.Text = $"{p.BankName} · {p.Items.Count(i => i.Submitted)} / {p.Items.Count} 题";
        Feedback.Text = "";
    }
    private void Start(object sender, RoutedEventArgs e)
    {
        if (BankPicker.SelectedItem is not SampleLibrary bank) { Feedback.Text = "请先选择题库。"; return; }
        var mode = WrongMode.IsChecked == true ? "错题重练" : RandomMode.IsChecked == true ? "随机练习" : "顺序练习";
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
        Setup.Visibility = Visibility.Collapsed; Feedback.Text = "";
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
