using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class PracticeChecks
{
    internal static void Run(Window window)
    {
        window.Width = 1440; window.Height = 900;
        var bank = new SampleLibrary("练习验收题库", "text", 1, 1, 1, 1, true);
        var state = new LearningState();
        int saves = 0;
        var view = new PracticeView(state, new[] { bank }, () => saves++, () => { }, () => 20);
        ((ContentControl)window.FindName("PageHost")).Content = view; window.UpdateLayout();
        Program.Capture(window, "practice-setup");
        Click(view, "StartButton"); window.UpdateLayout();
        Program.Check(state.Practice!.Items.Count == 4 && ((Border)view.FindName("AnswerResult")).Visibility == Visibility.Collapsed, "练习开始不泄露答案");
        Click(view, "SubmitButton");
        Program.Check(!state.Practice.Items[0].Submitted && !((Button)view.FindName("NextButton")).IsEnabled, "空答案不提交也不能跳过");
        Program.Capture(window, "practice-question");
        CurrentRadios(view)[0].IsChecked = true; Click(view, "SubmitButton");
        Program.Check(state.Wrongs.Count == 1 && !((Button)view.FindName("SubmitButton")).IsEnabled, "答错后加入错题并锁定提交");
        Program.Capture(window, "practice-answer");
        Click(view, "SubmitButton"); Program.Check(state.Wrongs.Count == 1, "重复提交不重复加入错题");
        var wrong = new WrongBookView(state, (_, _) => { });
        ((ContentControl)window.FindName("PageHost")).Content = wrong; window.UpdateLayout(); Program.Capture(window, "wrong-book");
        ((ContentControl)window.FindName("PageHost")).Content = view;
        Click(view, "NextButton"); window.UpdateLayout();
        var editor = (AnswerEditor)((ContentControl)view.FindName("QuestionHost")).Content;
        var multiple = Program.Descendants<CheckBox>(editor).ToArray(); multiple[0].IsChecked = multiple[2].IsChecked = true;
        Click(view, "SubmitButton"); Click(view, "NextButton"); window.UpdateLayout();
        CurrentRadios(view)[0].IsChecked = true; Click(view, "SubmitButton"); Click(view, "NextButton"); window.UpdateLayout();
        editor = (AnswerEditor)((ContentControl)view.FindName("QuestionHost")).Content;
        var blanks = Program.Descendants<TextBox>(editor).ToArray();
        blanks[0].Text = " TRANSMISSION CONTROL PROTOCOL "; blanks[1].Text = "用户数据报协议";
        Click(view, "SubmitButton"); Click(view, "NextButton");
        Program.Check(state.Practice.Complete && state.Practice.Items.Count(i => i.Correct) == 3, "四种题型作答与完成统计正确");
        view.StartWrong(bank.Id, bank.Name); window.UpdateLayout();
        Program.Check(state.Practice.Items.Count == 1 && state.Practice.Mode == "错题重练", "错题重练仅取当前错题");
        CurrentRadios(view)[1].IsChecked = true; Click(view, "SubmitButton");
        Program.Check(state.Wrongs.Count == 0 && ((TextBlock)view.FindName("Feedback")).Text.Contains("移出"), "错题答对显示自动移出反馈");
        wrong.Refresh(); Program.Check(!((Button)wrong.FindName("RetryButton")).IsEnabled, "空错题本禁用重练入口");
        ThemeManager.Apply("深色"); window.Width = 1120; window.Height = 720; Program.Capture(window, "practice-compact-dark");
        ThemeManager.Apply("浅色"); Program.Check(saves > 0, "作答与进度操作触发本机保存");
    }
    private static RadioButton[] CurrentRadios(PracticeView view) => Program.Descendants<RadioButton>((AnswerEditor)((ContentControl)view.FindName("QuestionHost")).Content).ToArray();
    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
