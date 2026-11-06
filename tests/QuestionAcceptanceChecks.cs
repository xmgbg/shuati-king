using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class QuestionAcceptanceChecks
{
    internal static void Run(Window window)
    {
        QuestionDraft Choice(string stem) => new() { Type = QuestionType.Single, Stem = stem, Options = ["甲", "乙"], CorrectOptions = [0] };
        var library = new SampleLibrary("审核验收", "粘贴文字");
        var first = Choice("同一道题"); var duplicate = Choice("同一道题");
        var missing = Choice("缺答案题"); missing.CorrectOptions.Clear();
        library.Questions.Add(first); library.Questions.Add(duplicate); library.Questions.Add(missing);
        var view = new ReviewView(library, () => { }, () => { });
        ((ContentControl)window.FindName("PageHost")).Content = view; window.UpdateLayout();
        foreach (var question in library.Questions) question.BatchSelected = true;
        ((Button)view.FindName("BatchConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Program.Check(first.IsConfirmed && !duplicate.IsConfirmed && !missing.IsConfirmed && library.ConfirmedCount == 1, "批量审核只入库一份完整题，重复题和缺答案保留");
        ((ListBox)view.FindName("QuestionList")).SelectedItem = duplicate;
        ((Button)view.FindName("ConfirmQuestionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Program.Check(!duplicate.IsConfirmed && ((TextBlock)view.FindName("ReviewFeedback")).Text.Contains("相同"), "单题审核同样阻止完全重复题");
        ((ListBox)view.FindName("QuestionList")).SelectedItem = first;
        ((TextBox)view.FindName("StemInput")).Text = "已修改的题干";
        ((Button)view.FindName("SaveQuestionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Program.Check(!first.IsConfirmed, "已入库题目修改后必须重新审核");
        ((Button)view.FindName("ConfirmQuestionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Program.Check(first.IsConfirmed, "修改后的有效题可以重新确认");
        var conflict = Choice("字段冲突"); conflict.JudgmentAnswer = true;
        Program.Check(conflict.ValidationError is not null, "跨题型混入的答案字段不能通过校验");
        var a = new QuestionDraft { Type = QuestionType.Multiple, Stem = "多选", Options = ["甲", "乙"], CorrectOptions = [0, 1] };
        var b = new QuestionDraft { Type = QuestionType.Multiple, Stem = "多选", Options = ["甲", "乙"], CorrectOptions = [1, 0], Explanation = "解析不同" };
        Program.Check(QuestionRules.Same(a, b), "去重不受答案下标顺序与解析文字影响");
        var afterAnswer = new SampleLibrary("补齐后重复", "资料"); var original = Choice("补齐的题"); var filled = Choice("补齐的题");
        original.IsConfirmed = true; afterAnswer.Questions.Add(original); afterAnswer.Questions.Add(filled);
        var issues = QuestionRules.RemoveDuplicateDrafts(afterAnswer, [filled.Id]);
        Program.Check(afterAnswer.Questions.Count == 1 && afterAnswer.Questions[0].IsConfirmed && issues.Count == 1, "答案关联后再次去重，保留原有已确认题并列明");
        var letter = Choice("错选项下标"); letter.CorrectOptions = [3];
        Program.Check(letter.ValidationError is not null, "越界答案不能用于确认");
        var options = Choice("重复选项"); options.Options = ["甲", "甲"];
        Program.Check(options.ValidationError is not null, "重复选项无法通过校验");
        Program.Capture(window, "acceptance-review-light");
        ThemeManager.Apply("深色"); Program.Capture(window, "acceptance-review-dark"); ThemeManager.Apply("浅色");
    }
}
