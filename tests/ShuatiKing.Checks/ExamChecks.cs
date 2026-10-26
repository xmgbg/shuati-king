using System.IO;
using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class ExamChecks
{
    internal static void Run(Window window)
    {
        window.Width = 1440; window.Height = 900;
        var host = (ContentControl)window.FindName("PageHost");
        var bank = new SampleLibrary("考试验收题库", "text", 1, 1, 1, 1, true);
        var state = new LearningState();
        var path = Path.GetFullPath($".artifacts/exam-ui-{Guid.NewGuid()}.json");
        var store = new LearningStore(path);
        var view = new ExamView(state, new[] { bank }, () => store.Save(state), () => { }, () => 20);
        host.Content = view; window.UpdateLayout();
        Click(view, "StartExamButton");
        Program.Check(state.Exam is null && ((TextBlock)view.FindName("Feedback")).Text.Contains("缺"), "题量不足时不能开考且提示缺题");
        var rows = ((StackPanel)view.FindName("TypeRows")).Children.Cast<Grid>().ToArray();
        for (var i = 0; i < 4; i++)
        {
            var inputs = rows[i].Children.OfType<TextBox>().ToArray(); inputs[0].Text = "1"; inputs[1].Text = (i + 2).ToString();
        }
        ((TextBox)view.FindName("MinutesInput")).Text = "1";
        Program.Capture(window, "exam-setup");
        Click(view, "StartExamButton"); window.UpdateLayout();
        Program.Check(state.Exam!.Total == 14 && ((Border)view.FindName("ResultPanel")).Visibility == Visibility.Collapsed, "开考后隐藏分数和解析");
        var item = state.Exam.Items[0]; AnswerCorrectly((AnswerEditor)((ContentControl)view.FindName("QuestionHost")).Content);
        Click(view, "NextButton"); Click(view, "PreviousButton"); window.UpdateLayout();
        Program.Check(item.Answered && !item.Submitted, "切题保留作答但未交卷不判分");
        Program.Capture(window, "exam-question");
        Click(view, "PauseButton");
        double remaining = state.Exam.RemainingSeconds;
        state.Exam.Advance(15);
        Program.Check(state.Exam.Paused && state.Exam.RemainingSeconds == remaining && ((Border)view.FindName("PausedPanel")).Visibility == Visibility.Visible, "暂停提示明确且不扣时");
        Program.Capture(window, "exam-paused");
        var restored = store.Load();
        Program.Check(restored.Exam!.Paused && restored.Exam.Items[0].Answered && restored.Exam.RemainingSeconds == remaining, "恢复考试保留未交卷答案和精确剩余时间");
        Click(view, "ContinueExamButton");
        Click(view, "SubmitPaperButton");
        Program.Check(!state.Exam.Finished && ((Border)view.FindName("SubmitConfirmation")).Visibility == Visibility.Visible, "交卷前显示确认和未作答数量");
        Click(view, "CancelSubmitButton"); Program.Check(!state.Exam.Finished, "取消交卷继续作答");
        Click(view, "SubmitPaperButton"); Click(view, "ConfirmSubmitButton");
        Program.Check(state.Exam.Finished && state.Exam.Score == item.Points && state.Wrongs.Count == 3, "正式交卷按题型分值计分且未作答零分加入错题");
        Program.Capture(window, "exam-submitted");
        var timeoutState = new LearningState { Exam = LearningRules.CreateExam(bank, [1, 1, 1, 1], [1, 1, 1, 1], 1) };
        timeoutState.Exam.RemainingSeconds = 0.05;
        var timeout = new ExamView(timeoutState, new[] { bank }, () => { }, () => { }, () => 18);
        host.Content = timeout; window.UpdateLayout(); Click(timeout, "ResumeExamButton"); Click(timeout, "ContinueExamButton");
        var timeoutFrame = new System.Windows.Threading.DispatcherFrame();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var guard = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        guard.Tick += (_, _) => { if (timeoutState.Exam.Finished || watch.Elapsed.TotalSeconds > 3) { guard.Stop(); timeoutFrame.Continue = false; } };
        guard.Start(); System.Windows.Threading.Dispatcher.PushFrame(timeoutFrame);
        Program.Check(timeoutState.Exam.Finished && timeoutState.Exam.TimedOut && timeoutState.Exam.Score == 0 && ((TextBlock)timeout.FindName("ResultHeading")).Text.Contains("自动交卷"), "计时归零自动交卷并展示未作答零分");
        Program.Capture(window, "exam-timeout");
        var resumeView = new ExamView(restored, new[] { bank }, () => { }, () => { }, () => 18);
        host.Content = resumeView; window.UpdateLayout(); Click(resumeView, "ResumeExamButton");
        Program.Check(((Border)resumeView.FindName("PausedPanel")).Visibility == Visibility.Visible, "重开后必须手动恢复，打开页面不自动计时");
        ThemeManager.Apply("深色"); window.Width = 1120; window.Height = 720; Program.Capture(window, "exam-compact-dark");
        ThemeManager.Apply("浅色"); resumeView.PauseForNavigation(); File.Delete(path);
    }
    private static void AnswerCorrectly(AnswerEditor editor)
    {
        var q = editor.Item.Question;
        if (q.Type == QuestionType.Single) Program.Descendants<RadioButton>(editor).ElementAt(q.CorrectOptions[0]).IsChecked = true;
        else if (q.Type == QuestionType.Multiple)
        { var options = Program.Descendants<CheckBox>(editor).ToArray(); foreach (var i in q.CorrectOptions) options[i].IsChecked = true; }
        else if (q.Type == QuestionType.Judgment) Program.Descendants<RadioButton>(editor).ElementAt(q.JudgmentAnswer == true ? 0 : 1).IsChecked = true;
        else { var blanks = Program.Descendants<TextBox>(editor).ToArray(); for (int i = 0; i < blanks.Length; i++) blanks[i].Text = q.BlankAnswers[i][0]; }
    }
    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
