using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class CalculationChecks
{
    internal static void Models()
    {
        var question = new QuestionDraft { Type = QuestionType.Calculation, Stem = "求数值结果。", CalculationAnswer = 1.25m, CalculationTolerance = 0.02m };
        var answer = new AnswerItem { Question = question, Calculation = " 1.27 " };
        Program.Check(LearningRules.IsCorrect(answer), "绝对误差边界包含等号");
        answer.Calculation = "1.27001";
        Program.Check(!LearningRules.IsCorrect(answer), "超出误差不能得分");
        question.CalculationTolerance = 0; answer.Calculation = "125e-2";
        Program.Check(LearningRules.IsCorrect(answer), "科学计数法与等值小数精确匹配");
        foreach (var invalid in new[] { "", "NaN", "Infinity", "1.25 MB", "1e-100" })
        {
            answer.Calculation = invalid;
            Program.Check(!answer.Answered && !LearningRules.IsCorrect(answer), "非有效数值或下溢不能作答得分");
        }
        question.CalculationAnswer = decimal.MinValue; question.CalculationTolerance = decimal.MaxValue;
        answer.Calculation = NumericAnswer.Format(decimal.MaxValue);
        Program.Check(!LearningRules.IsCorrect(answer), "数值差超出范围时正常判错");
        var config = new AiConfiguration();
        const string stem = "每份资料 2.5 MB，4 份资料共多少 MB？";
        var source = DocumentReader.FromText($"1. {stem}\n答案：10 MB");
        var plan = MaterialPlanner.Create(source, true, [], config);
        var session = new QuestionImportSession(plan);
        var item = new { unitId = 1, type = "Calculation", number = "1", stem, quote = source.Text, originalAnswer = "10 MB", answerQuote = "答案：10 MB", calculationAnswer = 999 };
        QuestionImportService.ParseBatch(session, plan.Batches[0], JsonSerializer.Serialize(new { items = new[] { item }, answers = Array.Empty<object>(), skipped = Array.Empty<object>() }));
        Program.Check(session.Questions.Single().CalculationAnswer == 10 && session.Questions[0].OriginalAnswer == "10 MB", "识题从原答案提取数值，保留单位原文");
        var withoutAnswer = new QuestionImportSession(plan);
        QuestionImportService.ParseBatch(withoutAnswer, plan.Batches[0], JsonSerializer.Serialize(new { items = new[] { item with { originalAnswer = "", answerQuote = "" } }, answers = Array.Empty<object>(), skipped = Array.Empty<object>() }));
        Program.Check(withoutAnswer.Questions.Single().CalculationAnswer is null && withoutAnswer.Questions[0].ValidationError is not null, "缺原答案时即使模型猜了数值仍待补全");
        var tablePlan = MaterialPlanner.Create(DocumentReader.FromText($"1. {stem}\n参考答案\n1. 10 MB"), true, [], config);
        var table = new QuestionImportSession(tablePlan);
        QuestionImportService.ParseBatch(table, tablePlan.Batches[0], JsonSerializer.Serialize(new
        {
            items = new[] { item with { quote = stem, originalAnswer = "", answerQuote = "" } },
            answers = new[] { new { unitId = 2, number = "1", type = "Calculation", originalAnswer = "10 MB", answerQuote = "1. 10 MB" } }, skipped = Array.Empty<object>()
        }));
        QuestionImportService.ResolveAnswers(table);
        Program.Check(table.Questions.Single().CalculationAnswer == 10 && table.Questions[0].AnswerEvidence.Contains("1. 10 MB"), "文末答案关联到计算题且保留出处");
        using var fixture = new GenerationFixture();
        var generation = MaterialPlanner.Create(source, false, [QuestionType.Calculation], fixture.Configuration);
        var generated = new QuestionImportSession(generation);
        new QuestionImportService().RunAsync(generated, fixture.Configuration).GetAwaiter().GetResult();
        Program.Check(fixture.Server.RequestCount == 1 && generated.Questions.Single() is { Type: QuestionType.Calculation, CalculationAnswer: 10, ValidationError: null, IsConfirmed: false }, "真实 HTTP 流程仅选择计算题时生成可审核草稿");
        var malformed = new QuestionImportSession(generation);
        QuestionImportService.ParseGenerated(malformed, generation.Batches[0], JsonSerializer.Serialize(new
        {
            items = new[] { new { unitId = 1, type = "Calculation", stem, quote = stem, explanation = "相乘得到结果", calculationAnswer = 10, calculationTolerance = -1 } }, reasons = Array.Empty<string>()
        }));
        Program.Check(malformed.Questions.Count == 0 && malformed.Issues.Any(issue => issue.Reason.Contains("误差")), "AI 返回的负误差被拦截");
    }

    internal static void Views(Window window)
    {
        var database = new DatabaseStore(Program.TestPath("calculation/data/shuati.db")); database.Initialize();
        var bank = new SampleLibrary("计算题验收", "粘贴文字");
        var question = new QuestionDraft { Type = QuestionType.Calculation, Stem = "一份资料 2.5 MB，求其大小（单位 MB）。", Explanation = "资料大小为 2.5 MB。" };
        bank.Questions.Add(question);
        var host = (ContentControl)window.FindName("PageHost");
        var review = new ReviewView(bank, () => { }, () => { }, persist: database.SaveLibrary);
        host.Content = review; window.UpdateLayout();
        Click(review, "ConfirmQuestionButton");
        Program.Check(!question.IsConfirmed, "缺参考结果不能审核通过");
        Input(review, "CalculationAnswerInput").Text = "2.5";
        Input(review, "CalculationToleranceInput").Text = "-0.1";
        Click(review, "ConfirmQuestionButton");
        Program.Check(!question.IsConfirmed, "负误差不能审核通过");
        Input(review, "CalculationToleranceInput").Text = "0.1";
        Click(review, "ConfirmQuestionButton");
        var restored = database.LoadLibraries().Single();
        Program.Check(restored.Questions.Single() is { IsConfirmed: true, CalculationAnswer: 2.5m, CalculationTolerance: 0.1m }, "参考数值、误差和审核状态重启可读");
        window.Width = 1440; window.Height = 900;
        Program.Capture(window, "calculation-review");
        var state = new LearningState(); var practice = new PracticeView(state, [restored], () => { }, () => { }, () => 18);
        host.Content = practice; window.UpdateLayout(); Click(practice, "StartButton");
        Answer(practice).Text = "错误文本";
        Click(practice, "SubmitButton");
        Program.Check(!state.Practice!.Items[0].Submitted, "练习拦截非数值输入");
        Answer(practice).Text = "2.61";
        Click(practice, "SubmitButton");
        Program.Check(!state.Practice.Items[0].Correct && state.Wrongs.Count == 1, "计算题答错进入错题本");
        practice.StartWrong(restored.Id, restored.Name);
        Answer(practice).Text = "2.6";
        Click(practice, "SubmitButton");
        Program.Check(state.Practice.Items[0].Correct && state.Wrongs.Count == 0 && ((TextBlock)practice.FindName("CorrectAnswer")).Text.Contains("±0.1"), "错题重练按原误差判分并移出错题本");
        var exam = new ExamView(state, [restored], () => { }, () => { }, () => 18);
        host.Content = exam; window.UpdateLayout();
        foreach (Grid row in ((StackPanel)exam.FindName("TypeRows")).Children)
        {
            var selected = (int)row.Tag == (int)QuestionType.Calculation;
            ((StackPanel)row.Children[0]).Children.OfType<CheckBox>().Single().IsChecked = selected;
            if (selected) { var inputs = row.Children.OfType<TextBox>().ToArray(); inputs[0].Text = "1"; inputs[1].Text = "3"; }
        }
        Click(exam, "StartExamButton");
        Answer(exam).Text = "2.60";
        window.Width = 1120; window.Height = 720;
        ThemeManager.Apply("深色"); Program.Capture(window, "calculation-exam-compact"); ThemeManager.Apply("浅色");
        Click(exam, "SubmitPaperButton"); Click(exam, "ConfirmSubmitButton");
        var store = new LearningStore(Program.TestPath("calculation-history.json")); store.Save(state);
        var record = store.Load().ExamHistory.Single();
        Program.Check(record.Score == 3 && record.Items[0].Calculation == "2.60" && record.Items[0].Question.CalculationTolerance == 0.1m, "计算题按设置分值入成绩，历史保留原作答与误差快照");
        var result = new ExamResultView(record, 18); host.Content = result; window.UpdateLayout();
        Program.Check(((TextBlock)result.FindName("YourAnswer")).Text.Contains("2.60") && ((TextBlock)result.FindName("CorrectAnswer")).Text.Contains("2.5"), "考试结果显示作答数值与参考结果");
    }
    private static TextBox Input(FrameworkElement view, string name) => (TextBox)view.FindName(name);
    private static TextBox Answer(FrameworkElement view)
    {
        view.UpdateLayout();
        var editor = (AnswerEditor)((ContentControl)view.FindName("QuestionHost")).Content;
        return Program.Descendants<TextBox>(editor).Single();
    }
    private static void Click(FrameworkElement view, string name) => ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
