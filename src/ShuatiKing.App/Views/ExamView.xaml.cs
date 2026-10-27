using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ShuatiKing.Models;
using ShuatiKing.Services;

namespace ShuatiKing.Views;

public partial class ExamView : UserControl
{
    private readonly LearningState state;
    private readonly IReadOnlyList<SampleLibrary> banks;
    private readonly Action save;
    private readonly Action openWrongs;
    private readonly Func<int> fontSize;
    private readonly TextBox[] countInputs = new TextBox[4];
    private readonly TextBox[] scoreInputs = new TextBox[4];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch clock = new();
    private double lastTick, lastSave;
    private bool initializing = true;
    public ExamView(LearningState state, IReadOnlyList<SampleLibrary> banks, Action save, Action openWrongs, Func<int> fontSize)
    {
        this.state = state; this.banks = banks; this.save = save; this.openWrongs = openWrongs; this.fontSize = fontSize;
        InitializeComponent();
        var names = new[] { "单选题", "多选题", "判断题", "填空题" };
        InitializeTypeSettings();
        for (var i = 0; i < 4; i++) BuildTypeRow(i, names[i]);
        timer.Tick += (_, _) => Pulse(); initializing = false; ShowSetup();
    }
    public void ShowSetup(SampleLibrary? bank = null)
    {
        PauseForNavigation();
        Setup.Visibility = Visibility.Visible; ExamPanel.Visibility = ResultPanel.Visibility = Visibility.Collapsed;
        BankPicker.ItemsSource = banks; BankPicker.SelectedItem = bank ?? BankPicker.SelectedItem ?? banks.FirstOrDefault();
        ResumeCard.Visibility = state.Exam is null ? Visibility.Collapsed : Visibility.Visible;
        if (state.Exam is { } exam) ResumeText.Text = exam.BankName + (exam.Finished ? " · 已交卷" : " · 已暂停 · 剩余 " + FormatTime(exam.RemainingSeconds));
        Feedback.Text = ""; UpdateSettings();
    }
    private void SettingsChanged(object sender, RoutedEventArgs e) { if (!initializing) { Feedback.Text = ""; UpdateSettings(); } }
    private void UpdateSettings()
    {
        if (BankPicker.SelectedItem is SampleLibrary bank)
            AvailableCounts.Text = string.Join("\n", Enum.GetValues<QuestionType>().Select(t => $"{new[] { "单选题", "多选题", "判断题", "填空题" }[(int)t]}   {bank.Questions.Count(q => q.Type == t && q.IsConfirmed && q.ValidationError is null)} 道"));
        if (TrySettings(out var counts, out var scores)) PaperTotal.Text = $"共 {counts.Sum()} 题 · 满分 {counts.Select((c, i) => c * scores[i]).Sum():0.##} 分";
        else PaperTotal.Text = "请填写有效题量和分值";
    }
    private bool TrySettings(out int[] counts, out decimal[] scores)
    {
        counts = new int[4]; scores = new decimal[4];
        for (var i = 0; i < 4; i++)
        {
            if (typeSelections[i].IsChecked != true) { scores[i] = 1; continue; }
            if (!int.TryParse(countInputs[i].Text, out counts[i]) || counts[i] < 0 || !decimal.TryParse(scoreInputs[i].Text, out scores[i]) || scores[i] <= 0) return false;
        }
        return true;
    }
    private void StartExam(object sender, RoutedEventArgs e)
    {
        if (BankPicker.SelectedItem is not SampleLibrary bank) { Feedback.Text = "请选择题库。"; return; }
        if (!TrySettings(out var counts, out var scores) || counts.Sum() == 0 || !int.TryParse(MinutesInput.Text, out var minutes) || minutes <= 0)
        { Feedback.Text = "题量须为非负整数且至少一题，请选择至少一种题型，分值和考试时长须大于零。"; return; }
        if (LearningRules.Shortage(bank, counts) is { } missing) { Feedback.Text = missing; return; }
        if (state.Exam is { Finished: false } && MessageBox.Show("新考试会替换当前未交卷试卷。是否继续？", "开始新考试", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        state.Exam = LearningRules.CreateExam(bank, counts, scores, minutes, TypeRows.Children.Cast<Grid>().Where(row => typeSelections[(int)row.Tag].IsChecked == true).Select(row => (QuestionType)(int)row.Tag).ToArray()); QuestionMap.Children.Clear(); save(); OpenPaper(); BeginClock();
    }
    private void OpenPrevious(object sender, RoutedEventArgs e) => OpenPaper();
    public void OpenPaper()
    {
        if (state.Exam is not { } exam) return;
        Setup.Visibility = Visibility.Collapsed; Feedback.Text = ""; SubmitConfirmation.Visibility = Visibility.Collapsed;
        if (exam.Finished) { ShowResult(); return; }
        ResultPanel.Visibility = Visibility.Collapsed; ExamPanel.Visibility = Visibility.Visible; RenderQuestion(); PaintClock();
    }
    private void RenderQuestion()
    {
        var exam = state.Exam!;
        PaperName.Text = exam.BankName;
        var item = exam.Items[exam.Index]; QuestionHost.Content = new AnswerEditor(item, fontSize());
        QuestionPosition.Text = $"第 {exam.Index + 1} / {exam.Items.Count} 题 · 本题 {item.Points:0.##} 分";
        PreviousButton.IsEnabled = exam.Index > 0; NextButton.IsEnabled = exam.Index < exam.Items.Count - 1;
        UpdateMap(); PaintClock();
    }
    private void UpdateMap()
    {
        var exam = state.Exam!;
        AnsweredCount.Text = $"已作答 {exam.Items.Count(i => i.Answered)} / {exam.Items.Count} 题 · 满分 {exam.Total:0.##} 分";
        for (var i = 0; i < exam.Items.Count; i++)
        {
            if (QuestionMap.Children.Count <= i)
            {
                int index = i;
                var created = new Button { Padding = new Thickness(6, 8, 6, 8), Width = 68, Margin = new Thickness(0, 0, 6, 6), FontSize = 12 };
                created.Click += (_, _) => { exam.Index = index; save(); RenderQuestion(); };
                QuestionMap.Children.Add(created);
            }
            var button = (Button)QuestionMap.Children[i];
            button.Content = (i + 1) + (exam.Items[i].Answered ? " 已答" : "");
            button.SetResourceReference(BackgroundProperty, exam.Items[i].Answered ? "TintBrush" : "SurfaceBrush");
            button.SetResourceReference(BorderBrushProperty, i == exam.Index ? "AccentBrush" : "LineBrush");
        }
    }
    private void BeginClock()
    {
        if (state.Exam is not { Finished: false } exam) return;
        exam.Paused = false; lastTick = lastSave = 0; clock.Restart(); timer.Start(); PaintClock(); save();
    }
    public void Pulse()
    {
        if (state.Exam is not { Finished: false, Paused: false } exam) return;
        double now = clock.Elapsed.TotalSeconds; exam.Advance(now - lastTick); lastTick = now;
        if (exam.RemainingSeconds <= 0) { Finish(true); return; }
        PaintClock(); UpdateMap();
        if (now - lastSave >= 5) { lastSave = now; save(); }
    }
    public void PauseForNavigation()
    {
        if (state.Exam is not { Finished: false } exam) return;
        Pulse(); if (exam.Finished) return;
        exam.Paused = true; clock.Stop(); timer.Stop(); SubmitConfirmation.Visibility = Visibility.Collapsed; PaintClock(); save();
    }
    private void PaintClock()
    {
        if (state.Exam is not { } exam) return;
        ClockText.Text = FormatTime(exam.RemainingSeconds);
        QuizGrid.Visibility = exam.Paused ? Visibility.Collapsed : Visibility.Visible;
        PausedPanel.Visibility = exam.Paused ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.IsEnabled = SubmitPaperButton.IsEnabled = !exam.Paused;
    }
    private static string FormatTime(double seconds) { var time = TimeSpan.FromSeconds(Math.Ceiling(seconds)); return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"; }
    private void PauseClick(object sender, RoutedEventArgs e) => PauseForNavigation();
    private void ResumeClick(object sender, RoutedEventArgs e) => BeginClock();
    private void Next(object sender, RoutedEventArgs e) { var exam = state.Exam!; if (exam.Index < exam.Items.Count - 1) { exam.Index++; save(); RenderQuestion(); } }
    private void Previous(object sender, RoutedEventArgs e) { var exam = state.Exam!; if (exam.Index > 0) { exam.Index--; save(); RenderQuestion(); } }
    private void AskSubmit(object sender, RoutedEventArgs e)
    {
        var exam = state.Exam!;
        ConfirmText.Text = $"已作答 {exam.Items.Count(i => i.Answered)} / {exam.Items.Count} 题，未作答 {exam.Items.Count(i => !i.Answered)} 题。未作答计零分，交卷后不能修改。";
        SubmitConfirmation.Visibility = Visibility.Visible;
    }
    private void CancelSubmit(object sender, RoutedEventArgs e) => SubmitConfirmation.Visibility = Visibility.Collapsed;
    private void ConfirmSubmit(object sender, RoutedEventArgs e) { Pulse(); Finish(false); }
    private void Finish(bool timeout)
    {
        if (state.Exam is not { Finished: false } exam) return;
        timer.Stop(); clock.Stop(); LearningRules.FinishExam(state, exam, timeout); save(); ShowResult();
    }
    private void ShowResult()
    {
        var exam = state.Exam!; ExamPanel.Visibility = Setup.Visibility = Visibility.Collapsed; ResultPanel.Visibility = Visibility.Visible;
        ResultHeading.Text = exam.TimedOut ? "时间已到，已自动交卷" : "已交卷";
        ResultScore.Text = $"{exam.Score:0.##} / {exam.Total:0.##} 分";
        ResultSummary.Text = $"共 {exam.Items.Count} 题 · 答对 {exam.Items.Count(i => i.Correct)} 题 · 未作答 {exam.Items.Count(i => !i.Answered)} 题\n用时 {FormatTime(exam.DurationSeconds - exam.RemainingSeconds)}";
        Feedback.Text = "作答和评分已保存，重复打开不会重复交卷。";
    }
    private void BackToSetup(object sender, RoutedEventArgs e) => ShowSetup();
    private void OpenWrongs(object sender, RoutedEventArgs e) => openWrongs();
}
