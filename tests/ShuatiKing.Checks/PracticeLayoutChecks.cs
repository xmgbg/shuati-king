using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class PracticeLayoutChecks
{
    internal static void Run(Window window)
    {
        var banks = SampleLibrary.CreateExamples();
        var state = new LearningState();
        foreach (var type in Enum.GetValues<QuestionType>())
            foreach (var question in banks[0].Questions.Where(q => q.Type == type).Take(3))
                state.Wrongs.Add(new() { BankId = banks[0].Id, BankName = banks[0].Name, Question = LearningRules.Snapshot(question) });
        state.Wrongs.Add(new() { BankId = banks[1].Id, BankName = banks[1].Name, Question = LearningRules.Snapshot(banks[1].Questions[0]) });
        var previous = state.Practice = LearningRules.StartPractice(banks[0], "顺序练习", state);
        previous.Index = 23;
        foreach (var item in previous.Items.Take(23)) item.Submitted = true;
        var view = new PracticeView(state, banks, () => { }, () => { }, () => 18);
        var host = (ContentControl)window.FindName("PageHost"); host.Content = view;
        window.Width = 1440; window.Height = 900; window.UpdateLayout();
        var picker = (ComboBox)view.FindName("BankPicker");
        var setup = (ScrollViewer)view.FindName("Setup");
        var sequential = (RadioButton)view.FindName("SequentialMode");
        var random = (RadioButton)view.FindName("RandomMode");
        var wrong = (RadioButton)view.FindName("WrongMode");
        var summary = (Border)view.FindName("RoundSummarySection");
        var start = (Button)view.FindName("StartButton");
        Program.Check(Text(view, "SequentialCount") == "128 题" && Text(view, "RandomCount") == "128 题" && Text(view, "WrongCount") == "12 题", "三种模式显示所选题库的实际题量");
        Program.Check(Text(view, "RoundCount") == "128" && Distribution(view).SequenceEqual(new[] { "60", "28", "20", "20" }), "顺序练习汇总与题库题型分布一致");
        Program.Check(Program.Descendants<Button>(summary).Contains(start), "开始练习按钮位于本轮练习汇总区");
        Program.Check(((Border)view.FindName("ResumeCard")).TranslatePoint(new Point(), view).Y < setup.TranslatePoint(new Point(), view).Y && Text(view, "ResumeText").Contains("23 / 128"), "页头继续入口显示已保存练习进度");
        wrong.IsChecked = true;
        Program.Check(sequential.IsChecked == false && random.IsChecked == false && Text(view, "ModeName") == "错题重练" && Text(view, "RoundCount") == "12", "整行单选切换为错题重练并更新本轮题量");
        Program.Check(Distribution(view).SequenceEqual(new[] { "3", "3", "3", "3" }), "错题模式显示当前题库错题的题型分布");
        Program.Capture(window, "practice-redesign-wrong");
        picker.SelectedIndex = 1;
        Program.Check(Text(view, "WrongCount") == "1 题" && Text(view, "RoundCount") == "1" && Text(view, "BankSource").Contains("86"), "切换题库后错题统计不混入其他题库");
        picker.SelectedIndex = 3;
        Click(view, "StartButton");
        Program.Check(ReferenceEquals(state.Practice, previous) && Text(view, "RoundCount") == "0" && Text(view, "Feedback").Contains("没有错题"), "空错题模式给出反馈并保留原练习");
        sequential.IsChecked = true; Click(view, "StartButton");
        Program.Check(Text(view, "SequentialCount") == "0 题" && Text(view, "Feedback").Contains("审核"), "待审核题目不计入可练习题量");
        picker.SelectedIndex = 0; random.IsChecked = true;
        Program.Check(Text(view, "ModeName") == "随机练习" && Text(view, "RoundCount") == "128", "随机模式同步更新名称与本轮题量");
        sequential.IsChecked = true;
        var rules = (ToggleButton)view.FindName("RulesButton"); var popup = (Popup)view.FindName("RulesPopup");
        window.Activate(); rules.Focus(); rules.IsChecked = true; Program.Capture(window, "practice-redesign-rules");
        Program.Check(popup.IsOpen, "练习规则按需展开");
        rules.IsChecked = false;
        Program.Capture(window, "practice-redesign-light");
        window.Width = 1920; window.Height = 1080; Program.Capture(window, "practice-redesign-wide");
        window.Width = 1120; window.Height = 720; Program.Capture(window, "practice-redesign-compact");
        Program.Check(setup.ExtentHeight <= setup.ViewportHeight + 1 && setup.ExtentWidth <= setup.ViewportWidth + 1, "最小窗口完整显示练习入口且无滚动");
        Program.Check(start.TranslatePoint(new Point(), setup).Y + start.ActualHeight <= setup.ViewportHeight, "最小窗口开始练习按钮完整可见");
        ThemeManager.Apply("深色"); Program.Capture(window, "practice-redesign-dark"); ThemeManager.Apply("浅色");
        rules.IsChecked = true; Click(view, "ResumeButton"); window.UpdateLayout();
        Program.Check(ReferenceEquals(state.Practice, previous) && previous.Index == 23 && !popup.IsOpen && !((Border)view.FindName("ResumeCard")).IsVisible, "继续练习保留原位置并收起入口及规则弹层");
        Click(view, "BackButton"); state.Practice = null; wrong.IsChecked = true; Click(view, "StartButton"); window.UpdateLayout();
        Program.Check(state.Practice is { Mode: "错题重练" } round && round.Items.Count == 12 && round.Items.GroupBy(i => i.Question.Type).All(g => g.Count() == 3), "实际开始错题练习的题量及分布与预览一致");
        var empty = new PracticeView(new LearningState(), Array.Empty<SampleLibrary>(), () => { }, () => { }, () => 18);
        host.Content = empty; window.UpdateLayout(); Click(empty, "StartButton");
        Program.Check(Text(empty, "RoundCount") == "0" && Text(empty, "Feedback").Contains("选择题库"), "无题库时显示零题并拦住开始操作");
        Program.Capture(window, "practice-redesign-empty");
    }
    private static string[] Distribution(FrameworkElement view) => new[] { "SingleCount", "MultipleCount", "JudgmentCount", "BlankCount" }.Select(name => Text(view, name)).ToArray();
    private static string Text(FrameworkElement view, string name) => ((TextBlock)view.FindName(name)).Text;
    private static void Click(FrameworkElement view, string name) => ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}