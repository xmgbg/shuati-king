using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ShuatiKing;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class EndToEndChecks
{
    internal static void Run()
    {
        var settingsPath = Program.TestPath("full-flow.dat");
        var settings = new SettingsStore(settingsPath); var learningStore = new LearningStore(settings.LearningPath);
        var window = new MainWindow(settings); window.Show(); window.UpdateLayout();
        var host = (ContentControl)window.FindName("PageHost");
        Navigate(window, "考试记录");
        var history = (ExamHistoryView)host.Content; Click(history, "NewExamButton");
        Program.Check(host.Content is ExamView, "考试记录空状态可返回模拟考试");
        Navigate(window, "题库");
        var home = (LibraryView)host.Content;
        LibraryAction(home, "导入资料");
        var import = (ImportView)host.Content;
        ((TextBox)import.FindName("LibraryNameInput")).Text = "全流程验收资料";
        ((TextBox)import.FindName("SourceInput")).Text = "网络协议、传输层与局域网基础。";
        Click(import, "NextButton"); Click(import, "NextButton");
        var counts = new[] { "SingleCount", "MultipleCount", "JudgmentCount", "BlankCount" };
        for (var i = 0; i < counts.Length; i++) ((TextBox)import.FindName(counts[i])).Text = i == 0 ? "2" : "1";
        var generating = import.GenerateAsync(); var frame = new DispatcherFrame();
        generating.ContinueWith(_ => window.Dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); generating.GetAwaiter().GetResult();
        Click(import, "NextButton");
        var review = (ReviewView)host.Content;
        var all = (CheckBox)review.FindName("SelectAllCheck"); all.IsChecked = true; all.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        Click(review, "BatchConfirmButton"); Click(review, "HomeButton");
        var bank = home.SelectedLibrary!;
        Program.Check(bank.Name == "全流程验收资料" && bank.ConfirmedCount == 5, "导入示例经审核形成独立可用题库");
        LibraryAction(home, "开始练习"); var practice = (PracticeView)host.Content; Click(practice, "StartButton"); window.UpdateLayout();
        for (var i = 0; i < 5; i++)
        {
            var editor = (AnswerEditor)((ContentControl)practice.FindName("QuestionHost")).Content;
            Fill(editor, i != 0); Click(practice, "SubmitButton"); Click(practice, "NextButton"); window.UpdateLayout();
        }
        Program.Check(learningStore.Load().Practice!.Complete && learningStore.Load().Wrongs.Count == 1, "审核后的题库走通作答判分并收集错题");
        Navigate(window, "错题本"); var wrong = (WrongBookView)host.Content; Click(wrong, "RetryButton"); window.UpdateLayout();
        practice = (PracticeView)host.Content;
        Fill((AnswerEditor)((ContentControl)practice.FindName("QuestionHost")).Content, true); Click(practice, "SubmitButton"); Click(practice, "NextButton");
        Navigate(window, "错题本"); wrong = (WrongBookView)host.Content;
        Program.Check(learningStore.Load().Wrongs.Count == 0 && ((StackPanel)wrong.FindName("EmptyState")).Visibility == Visibility.Visible, "错题重练答对后自动移出且显示空状态");
        Click(wrong, "PracticeButton"); practice = (PracticeView)host.Content;
        Program.Check(((ComboBox)practice.FindName("BankPicker")).SelectedItem == bank, "错题本返回刷题仍保留当前题库");
        Navigate(window, "题库"); LibraryAction(home, "模拟考试"); var exam = (ExamView)host.Content;
        foreach (var row in ((StackPanel)exam.FindName("TypeRows")).Children.Cast<Grid>())
        {
            var inputs = row.Children.OfType<TextBox>().ToArray(); inputs[0].Text = "1"; inputs[1].Text = (int)row.Tag == 0 ? "125" : "25";
        }
        Click(exam, "StartExamButton"); window.UpdateLayout();
        for (var i = 0; i < 3; i++)
        {
            Fill((AnswerEditor)((ContentControl)exam.FindName("QuestionHost")).Content, i != 0);
            Click(exam, "NextButton"); window.UpdateLayout();
        }
        Click(exam, "SubmitPaperButton"); Click(exam, "ConfirmSubmitButton"); window.UpdateLayout();
        var result = (ExamResultView)((ContentControl)exam.FindName("ResultDetailHost")).Content;
        Program.Check(result.Record.Score == 50 && result.Record.Total == 200 && result.Record.Items.Count == 4, "模拟考试保留非百分制计分和未答题零分");
        Click(exam, "ResultHistoryButton"); window.UpdateLayout(); history = (ExamHistoryView)host.Content;
        var archived = (ExamSession)((ListBox)history.FindName("HistoryList")).SelectedItem;
        var originalStem = archived.Items[0].Question.Stem;
        Program.Check(archived.BankName == bank.Name && learningStore.Load().ExamHistory.Count == 1, "交卷结果入口进入对应历史详情并落盘");
        Navigate(window, "题库"); LibraryAction(home, "题目管理"); review = (ReviewView)host.Content;
        ((ListBox)review.FindName("QuestionList")).SelectedItem = bank.Questions.Single(q => q.Id == archived.Items[0].Question.Id);
        ((TextBox)review.FindName("StemInput")).Text = "考试后修改的原题";
        Click(review, "HomeButton"); Navigate(window, "考试记录"); history = (ExamHistoryView)host.Content;
        result = (ExamResultView)((ContentControl)history.FindName("DetailHost")).Content;
        Program.Check(result.Record.Items[0].Question.Stem == originalStem && result.Record.Score == 50, "审核修改原题后旧历史仍展示原题及原评分");
        var snapshot = JsonSerializer.Serialize(result.Record);
        foreach (var title in new[] { "题库", "刷题", "错题本", "模拟考试", "考试记录" })
        {
            Navigate(window, title);
            Program.Check(host.Content is not TextBlock, $"{title}导航已接通实际页面");
        }
        ((Button)window.FindName("ProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
        Program.Check(host.Content is ProfileView, "个人入口仍可到达");
        window.Close();
        var reopened = new MainWindow(settings); reopened.Show(); reopened.UpdateLayout(); Navigate(reopened, "考试记录");
        history = (ExamHistoryView)((ContentControl)reopened.FindName("PageHost")).Content;
        result = (ExamResultView)((ContentControl)history.FindName("DetailHost")).Content;
        Program.Check(JsonSerializer.Serialize(result.Record) == snapshot, "关闭重开后仍完整保留历史题干答案解析与评分");
        Navigate(reopened, "错题本");
        Program.Check(learningStore.Load().Wrongs.Count == 2, "历史回看和重开不改变考试错题状态");
        Program.Capture(reopened, "full-flow-wrongs"); reopened.Close();
        var noBank = new ExamView(new LearningState(), Array.Empty<SampleLibrary>(), () => { }, () => { }, () => 18);
        Click(noBank, "StartExamButton");
        Program.Check(((TextBlock)noBank.FindName("Feedback")).Text.Contains("请选择题库"), "无题库时考试入口给出可处理反馈");
        File.Delete(settingsPath); File.Delete(settings.LearningPath);
    }
    private static void Fill(AnswerEditor editor, bool correct)
    {
        var q = editor.Item.Question;
        if (q.Type == QuestionType.Single) Program.Descendants<RadioButton>(editor).ElementAt(correct ? q.CorrectOptions[0] : Enumerable.Range(0, q.Options.Count).First(i => !q.CorrectOptions.Contains(i))).IsChecked = true;
        else if (q.Type == QuestionType.Multiple)
        {
            var inputs = Program.Descendants<CheckBox>(editor).ToArray();
            foreach (var i in q.CorrectOptions.Take(correct ? q.CorrectOptions.Count : 1)) inputs[i].IsChecked = true;
        }
        else if (q.Type == QuestionType.Judgment) Program.Descendants<RadioButton>(editor).ElementAt((correct ? q.JudgmentAnswer == true : q.JudgmentAnswer != true) ? 0 : 1).IsChecked = true;
        else
        {
            var inputs = Program.Descendants<TextBox>(editor).ToArray();
            for (var i = 0; i < inputs.Length; i++) inputs[i].Text = correct ? q.BlankAnswers[i][0] : "错误答案";
        }
    }
    private static void LibraryAction(LibraryView library, string action) => Program.Descendants<Button>(library).Single(b => b.Tag?.ToString() == action && b.Name != "ResumeAction").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Navigate(MainWindow window, string title)
    {
        ((StackPanel)window.FindName("NavigationTabs")).Children.OfType<RadioButton>().Single(tab => tab.Content?.ToString() == title).IsChecked = true;
        window.UpdateLayout();
    }
    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}