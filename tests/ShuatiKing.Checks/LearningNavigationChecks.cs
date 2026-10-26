using System.IO;
using System.Windows;
using System.Windows.Controls;
using ShuatiKing;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class LearningNavigationChecks
{
    internal static void Run()
    {
        var settingsPath = Path.GetFullPath($".artifacts/navigation-{Guid.NewGuid()}.dat");
        var settings = new SettingsStore(settingsPath);
        var learningStore = new LearningStore(settings.LearningPath);
        var first = new MainWindow(settings); first.Show(); first.UpdateLayout();
        Navigate(first, "模拟考试");
        var host = (ContentControl)first.FindName("PageHost"); var exam = (ExamView)host.Content;
        foreach (var row in ((StackPanel)exam.FindName("TypeRows")).Children.Cast<Grid>()) row.Children.OfType<TextBox>().First().Text = "1";
        ((TextBox)exam.FindName("MinutesInput")).Text = "1";
        Click(exam, "StartExamButton"); first.UpdateLayout();
        var editor = (AnswerEditor)((ContentControl)exam.FindName("QuestionHost")).Content;
        if (editor.Item.Question.Type == ShuatiKing.Models.QuestionType.Blank) Program.Descendants<TextBox>(editor).First().Text = "关闭前的填空作答";
        else if (editor.Item.Question.Type == ShuatiKing.Models.QuestionType.Multiple) Program.Descendants<CheckBox>(editor).First().IsChecked = true;
        else Program.Descendants<RadioButton>(editor).First().IsChecked = true;
        var questionId = editor.Item.Question.Id;
        Navigate(first, "题库");
        var paused = learningStore.Load();
        Program.Check(paused.Exam!.Paused && paused.Exam.Items[0].Answered && paused.Exam.RemainingSeconds <= 60, "真实顶部导航离开考试时保存作答并暂停");
        Navigate(first, "刷题"); var practice = (PracticeView)host.Content; Click(practice, "StartButton"); first.UpdateLayout();
        var practiceEditor = (AnswerEditor)((ContentControl)practice.FindName("QuestionHost")).Content;
        Program.Descendants<RadioButton>(practiceEditor).ElementAt(1).IsChecked = true; Click(practice, "SubmitButton");
        first.Close();
        Program.Check(learningStore.Load().Practice!.Items[0].Submitted, "关闭窗口实际保存练习提交与进度");
        var reopened = new MainWindow(settings); reopened.Show(); reopened.UpdateLayout();
        Navigate(reopened, "模拟考试"); host = (ContentControl)reopened.FindName("PageHost"); exam = (ExamView)host.Content;
        Click(exam, "ResumeExamButton"); reopened.UpdateLayout();
        Program.Check(((Border)exam.FindName("PausedPanel")).Visibility == Visibility.Visible && ((AnswerEditor)((ContentControl)exam.FindName("QuestionHost")).Content).Item.Question.Id == questionId, "重开窗口恢复相同试卷并明确显示暂停");
        Program.Capture(reopened, "exam-reopened");
        Navigate(reopened, "刷题"); practice = (PracticeView)host.Content; Click(practice, "ResumeButton"); reopened.UpdateLayout();
        Program.Check(((Border)practice.FindName("AnswerResult")).Visibility == Visibility.Visible, "真实导航继续练习恢复已提交答案与解析");
        Program.Capture(reopened, "practice-reopened");
        Click(practice, "NextButton"); reopened.UpdateLayout();
        practiceEditor = (AnswerEditor)((ContentControl)practice.FindName("QuestionHost")).Content;
        Program.Descendants<RadioButton>(practiceEditor).First().IsChecked = true;
        reopened.Close();
        var saved = learningStore.Load();
        Program.Check(saved.Practice!.Index == 1 && saved.Practice.Items[1].Answered && !saved.Practice.Items[1].Submitted, "中途关闭也保存当前未提交的作答");
        File.Delete(settingsPath); File.Delete(settings.LearningPath);
    }
    private static void Navigate(MainWindow window, string name)
    {
        var tabs = (StackPanel)window.FindName("NavigationTabs");
        tabs.Children.OfType<RadioButton>().Single(t => t.Content?.ToString() == name).IsChecked = true;
        window.UpdateLayout();
    }
    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
