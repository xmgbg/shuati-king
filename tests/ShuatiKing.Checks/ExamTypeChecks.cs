using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class ExamTypeChecks
{
    internal static void Run(Window window)
    {
        window.Width = 1440; window.Height = 900;
        var bank = new SampleLibrary("题型设置验收", "text", 2, 2, 2, 2, true);
        var state = new LearningState();
        var view = new ExamView(state, new[] { bank }, () => { }, () => { }, () => 18);
        ((ContentControl)window.FindName("PageHost")).Content = view; window.UpdateLayout();
        var panel = (StackPanel)view.FindName("TypeRows");
        var rows = panel.Children.Cast<Grid>().ToArray();
        foreach (var row in rows) Toggle(row).IsChecked = false;
        Click(view, "StartExamButton");
        Program.Check(state.Exam is null, "未选题型不能开考");
        Toggle(rows[0]).IsChecked = Toggle(rows[3]).IsChecked = true;
        Inputs(rows[0])[0].Text = "1"; Inputs(rows[0])[1].Text = "125";
        Inputs(rows[3])[0].Text = "1"; Inputs(rows[3])[1].Text = "50";
        Inputs(rows[1])[0].Text = "999"; Inputs(rows[1])[1].Text = "无效";
        Program.Check(!Inputs(rows[1])[0].IsEnabled && !Inputs(rows[1])[1].IsEnabled, "取消题型后禁用数量与分值");
        Program.Check(((TextBlock)view.FindName("PaperTotal")).Text.Contains("175"), "未选题型不影响满分统计或触发参数错误");
        var up = ((StackPanel)rows[3].Children[0]).Children.OfType<Button>().Single(b => b.Content?.ToString() == "↑");
        up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Program.Check((int)((Grid)panel.Children[2]).Tag == 3, "键盘可用上移操作调整题型位置");
        view.MoveTypeRow(3, 0);
        Program.Check((int)((Grid)panel.Children[0]).Tag == 3 && Inputs(rows[3])[1].Text == "50", "重排题型保留原分值");
        Program.Capture(window, "exam-type-settings");
        ThemeManager.Apply("深色"); window.Width = 1120; window.Height = 720; Program.Capture(window, "exam-type-settings-dark");
        ThemeManager.Apply("浅色");
        Click(view, "StartExamButton"); window.UpdateLayout();
        Program.Check(state.Exam is not null && state.Exam.Total == 175 && state.Exam.Items.Select(i => i.Question.Type).SequenceEqual(new[] { QuestionType.Blank, QuestionType.Single }), "实际开考只抽取已选题型且按照显示顺序出卷");
        view.PauseForNavigation();
    }
    private static CheckBox Toggle(Grid row) => ((StackPanel)row.Children[0]).Children.OfType<CheckBox>().Single();
    private static TextBox[] Inputs(Grid row) => row.Children.OfType<TextBox>().ToArray();
    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
