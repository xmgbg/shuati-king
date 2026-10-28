using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class ExamHistoryChecks
{
    internal static void Models()
    {
        var bank = new SampleLibrary("历史快照校验", "text", 1, 1, 1, 1, true);
        var state = new LearningState();
        var first = state.Exam = LearningRules.CreateExam(bank, [1, 1, 1, 1], [2, 3, 4, 5], 10);
        Answer(first.Items[0]); first.Items[1].Selected = [0]; Answer(first.Items[2]);
        first.Advance(37); LearningRules.FinishExam(state, first, false);
        Program.Check(state.ExamHistory.Count == 1 && first.FinishedAt is not null && first.StartedAt is not null, "交卷记录时间并归档");
        var saved = state.ExamHistory[0];
        Program.Check(saved.Score == 6 && saved.Total == 14 && saved.Items.Count(i => !i.Answered) == 1, "归档保留实际分值和未作答状态");
        LearningRules.FinishExam(state, first, true); LearningRules.ArchiveExam(state, first);
        Program.Check(state.ExamHistory.Count == 1 && !saved.TimedOut, "重复交卷和归档不重复添加或改判");
        var stem = saved.Items[0].Question.Stem; var option = saved.Items[0].Question.Options[0];
        first.Items[0].Question.Stem = "改动当前试卷"; first.Items[0].Question.Options[0] = "改动选项"; first.Items[0].Selected.Clear(); first.Items[0].Points = 999;
        bank.Questions.Clear();
        Program.Check(saved.Items[0].Question.Stem == stem && saved.Items[0].Question.Options[0] == option && saved.Items[0].Selected.SequenceEqual(new[] { 1 }) && saved.Total == 14 && saved.Score == 6, "原题删除及当前试卷改动不影响历史题目作答分值与成绩");
        var other = new SampleLibrary("第二次考试", "text", 1, 0, 0, 1, true);
        var second = state.Exam = LearningRules.CreateExam(other, [1, 0, 0, 1], [125, 1, 1, 50], 1);
        second.Advance(70); LearningRules.FinishExam(state, second, true);
        Program.Check(state.ExamHistory.Count == 2 && second.Id != first.Id && state.ExamHistory[1].TimedOut && state.ExamHistory[1].Total == 175, "新考试保留旧记录且到时交卷归档非百分制满分");
        var pending = LearningRules.CreateExam(other, [1, 0, 0, 0], [1, 1, 1, 1], 1);
        LearningRules.ArchiveExam(state, pending);
        Program.Check(state.ExamHistory.Count == 2, "未交卷试卷不进入历史记录");
        var path = Path.GetFullPath($".artifacts/history-model-{Guid.NewGuid()}.json");
        var store = new LearningStore(path); store.Save(state);
        var restored = store.Load();
        Program.Check(restored.ExamHistory.Count == 2 && restored.ExamHistory[0].Id == first.Id && restored.ExamHistory[0].Score == 6 && restored.ExamHistory[1].Total == 175, "重开后恢复全部历史及原评分");
        var legacy = JsonSerializer.SerializeToNode(new LearningState { Exam = saved })!.AsObject();
        legacy.Remove("ExamHistory");
        var oldExam = legacy["Exam"]!.AsObject(); oldExam.Remove("Id"); oldExam.Remove("StartedAt"); oldExam.Remove("FinishedAt");
        File.WriteAllText(path, legacy.ToJsonString());
        var migrated = store.Load();
        Program.Check(migrated.ExamHistory.Count == 1 && migrated.ExamHistory[0].Score == 6 && migrated.ExamHistory[0].FinishedAt is null, "兼容旧版已交卷试卷且不编造历史时间");
        store.Save(migrated); var reopened = store.Load();
        Program.Check(reopened.ExamHistory.Count == 1 && reopened.ExamHistory[0].Id == migrated.ExamHistory[0].Id, "兼容后再次打开不重复归档");
        File.Delete(path);
    }

    internal static void Views(Window window)
    {
        var host = (ContentControl)window.FindName("PageHost"); window.Width = 1440; window.Height = 900;
        bool opened = false; var empty = new ExamHistoryView(new(), () => opened = true, () => 18);
        host.Content = empty; window.UpdateLayout(); Program.Capture(window, "exam-history-empty");
        Program.Check(((Border)empty.FindName("EmptyPanel")).Visibility == Visibility.Visible, "历史记录空状态明确");
        Click(empty, "NewExamButton"); Program.Check(opened, "空记录页可进入模拟考试");
        var bank = new SampleLibrary("计算机网络基础", "计算机网络复习.pdf", 1, 1, 1, 1, true);
        var state = new LearningState();
        var exam = state.Exam = LearningRules.CreateExam(bank, [1, 1, 1, 1], [25, 25, 25, 25], 30);
        Answer(exam.Items[0]); exam.Items[1].Selected = [0]; Answer(exam.Items[2]); exam.Advance(323);
        LearningRules.FinishExam(state, exam, false);
        state.ExamHistory[0].FinishedAt = DateTimeOffset.Now.AddHours(-1);
        var other = new SampleLibrary("大学英语词汇", "英语词汇.docx", 1, 1, 1, 1, true);
        var second = state.Exam = LearningRules.CreateExam(other, [1, 0, 0, 1], [125, 1, 1, 50], 30);
        Answer(second.Items[0]); second.Advance(280); LearningRules.FinishExam(state, second, false);
        int font = 18; var view = new ExamHistoryView(state, () => { }, () => font);
        host.Content = view; window.UpdateLayout();
        var list = (ListBox)view.FindName("HistoryList");
        var result = (ExamResultView)((ContentControl)view.FindName("DetailHost")).Content;
        Program.Check(list.Items.Count == 2 && result.Record.Id == second.Id, "历史按交卷时间倒序并默认展示最近记录");
        Program.Check(((TextBlock)result.FindName("Score")).Text == "125 / 175 分", "历史成绩显示实际满分");
        list.SelectedIndex = 1; window.UpdateLayout();
        result = (ExamResultView)((ContentControl)view.FindName("DetailHost")).Content;
        Program.Check(result.Record.Id == exam.Id && ((TextBlock)result.FindName("Summary")).Text.Contains("答错 1 · 未答 1"), "切换记录同步展示答对答错及未答数量");
        Click(result, "NextButton"); window.UpdateLayout();
        Program.Check(((TextBlock)result.FindName("YourAnswer")).Text.Contains("A") && ((TextBlock)result.FindName("CorrectAnswer")).Text.Contains("A、C") && ((TextBlock)result.FindName("Outcome")).Text.Contains("答错"), "逐题回看展示用户答案正确答案和原得分");
        var before = JsonSerializer.Serialize(state.ExamHistory);
        var editor = (AnswerEditor)((ContentControl)result.FindName("QuestionHost")).Content;
        var checks = Program.Descendants<CheckBox>(editor).ToArray();
        Program.Check(checks.All(c => !c.IsEnabled), "历史回看禁止修改作答");
        checks[0].IsChecked = false;
        var map = (WrapPanel)result.FindName("QuestionMap");
        ((Button)map.Children[3]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Program.Check(((TextBlock)result.FindName("YourAnswer")).Text.Contains("未作答") && ((TextBlock)result.FindName("Outcome")).Text.Contains("未答") && ((TextBlock)result.FindName("Explanation")).Text.Length > 0, "未答题可定位并查看答案解析");
        Program.Check(JsonSerializer.Serialize(state.ExamHistory) == before, "回看与切题不改变历史快照");
        Click(result, "PreviousButton");
        Program.Capture(window, "exam-history-light");
        window.Width = 1120; window.Height = 720; font = 22; view.Refresh(); window.UpdateLayout();
        result = (ExamResultView)((ContentControl)view.FindName("DetailHost")).Content;
        var stem = result.Record.Items[0].Question.Stem;
        Program.Check(Program.Descendants<TextBlock>(result).Any(t => t.Text == stem && t.FontSize == 22), "历史回看应用当前题目字号");
        Program.Capture(window, "exam-history-compact");
        ThemeManager.Apply("深色"); Program.Capture(window, "exam-history-dark"); ThemeManager.Apply("浅色");
        var live = new ExamView(state, new[] { bank, other }, () => { }, () => { }, () => 18, () => opened = true);
        host.Content = live; live.OpenPaper(); window.UpdateLayout();
        Program.Check(((ContentControl)live.FindName("ResultDetailHost")).Content is ExamResultView && ((Border)live.FindName("ResultPanel")).Visibility == Visibility.Visible, "已交卷考试结果使用完整逐题回看");
        Click(live, "ResultHistoryButton"); Program.Check(opened, "结果页有考试记录入口");
        Program.Capture(window, "exam-result-review");
    }
    private static void Answer(AnswerItem item)
    {
        if (item.Question.Type is QuestionType.Single or QuestionType.Multiple) item.Selected = [.. item.Question.CorrectOptions];
        else if (item.Question.Type == QuestionType.Judgment) item.Judgment = item.Question.JudgmentAnswer;
        else item.Blanks = item.Question.BlankAnswers.Select(a => a[0]).ToList();
    }
    private static void Click(FrameworkElement view, string name) => ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}