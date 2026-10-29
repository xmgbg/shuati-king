using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class ExamLayoutChecks
{
    internal static void Run(Window window)
    {
        var banks = SampleLibrary.CreateExamples();
        var state = new LearningState { Exam = LearningRules.CreateExam(banks[0], [1, 1, 1, 1], [1, 1, 1, 1], 30) };
        state.Exam.Paused = true; state.Exam.RemainingSeconds = 1781;
        var view = new ExamView(state, banks, () => { }, () => { }, () => 18);
        ((ContentControl)window.FindName("PageHost")).Content = view;
        window.Width = 1440; window.Height = 900; window.UpdateLayout();
        var panel = (StackPanel)view.FindName("TypeRows");
        var rows = panel.Children.Cast<Grid>().ToArray();
        var picker = (ComboBox)view.FindName("BankPicker");
        var setup = (ScrollViewer)view.FindName("Setup");
        var summary = (Border)view.FindName("ExamSummarySection");
        var start = (Button)view.FindName("StartExamButton");
        Program.Check(Text(view, "QuestionTotal") == "25" && Text(view, "PaperTotal") == "40", "独立显示总题量和实际满分");
        Program.Check(Cell(rows[0], 1).Text == "60" && Cell(rows[0], 4).Text == "20 分", "题型表包含可用题量与小计");
        Program.Check(Program.Descendants<Button>(summary).Contains(start), "开始考试按钮位于考试设置汇总区");
        Program.Check(((Border)view.FindName("ResumeCard")).TranslatePoint(new Point(), view).Y < setup.TranslatePoint(new Point(), view).Y, "继续考试入口位于页头而非组卷区域");
        picker.SelectedIndex = 1;
        Program.Check(Cell(rows[0], 1).Text == "40" && Text(view, "BankSource").Contains("86"), "切换题库同步刷新来源和可用题量");
        picker.SelectedIndex = 3;
        Program.Check(rows.All(row => Cell(row, 1).Text == "0"), "待审核题目不计入可用题量");
        picker.SelectedIndex = 0;
        var toggle = ((StackPanel)rows[1].Children[0]).Children.OfType<CheckBox>().Single();
        toggle.IsChecked = false;
        Program.Check(Text(view, "QuestionTotal") == "20" && Text(view, "PaperTotal") == "30" && Cell(rows[1], 4).Text == "—", "取消题型后同步更新行小计与考试汇总");
        toggle.IsChecked = true;
        var inputs = rows[0].Children.OfType<TextBox>().ToArray();
        inputs[0].Text = "3"; inputs[1].Text = "2.5";
        Program.Check(Cell(rows[0], 4).Text == "7.5 分" && Text(view, "PaperTotal") == "27.5", "题量和分值变化实时刷新小计与非百分制总分");
        inputs[0].Text = "10"; inputs[1].Text = "2";
        var rules = (ToggleButton)view.FindName("RulesButton");
        var popup = (Popup)view.FindName("RulesPopup");
        window.Activate(); rules.Focus(); rules.IsChecked = true;
        Program.Capture(window, "exam-redesign-rules");
        Program.Check(popup.IsOpen, "评分规则按需展开");
        rules.IsChecked = false;
        Program.Capture(window, "exam-redesign-light");
        window.Width = 1920; window.Height = 1080;
        Program.Capture(window, "exam-redesign-wide");
        window.Width = 1120; window.Height = 720;
        Program.Capture(window, "exam-redesign-compact");
        Program.Check(setup.ExtentHeight <= setup.ViewportHeight + 1 && setup.ExtentWidth <= setup.ViewportWidth + 1, "最小窗口下完整显示组卷面板且无滚动");
        Program.Check(start.TranslatePoint(new Point(), setup).Y + start.ActualHeight <= setup.ViewportHeight, "最小窗口下开始考试始终可见");
        var typeScroll = (ScrollViewer)view.FindName("TypeRowsScroll");
        rows[^1].BringIntoView(); window.UpdateLayout();
        Program.Check(typeScroll.VerticalOffset > 0 &&
            rows[^1].TranslatePoint(new Point(), typeScroll).Y + rows[^1].ActualHeight <= typeScroll.ActualHeight + 1,
            "小窗口下通过题型表内部滚动可完整访问最后一个新增题型");
        typeScroll.ScrollToTop(); window.UpdateLayout();
        ThemeManager.Apply("深色"); Program.Capture(window, "exam-redesign-dark");
        ThemeManager.Apply("浅色");
        rules.IsChecked = true;
        ((Button)view.FindName("ResumeExamButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
        Program.Check(!popup.IsOpen && !((Border)view.FindName("ResumeCard")).IsVisible && ((Border)view.FindName("PausedPanel")).Visibility == Visibility.Visible, "继续考试收起设置入口和规则弹层并保留暂停状态");
        view.PauseForNavigation();
    }
    private static string Text(FrameworkElement view, string name) => ((TextBlock)view.FindName(name)).Text;
    private static TextBlock Cell(Grid row, int column) => row.Children.OfType<TextBlock>().Single(text => Grid.GetColumn(text) == column);
}