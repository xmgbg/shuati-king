using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ShuatiKing.Models;
using ShuatiKing.Views;
using ShuatiKing.Services;

internal static class ImportChecks
{
    internal static void Run(Window window)
    {
        ((RadioButton)window.FindName("LibraryTab")).IsChecked = true;
        var host = (ContentControl)window.FindName("PageHost");
        using var fixture = new GenerationFixture();
        SampleLibrary? saved = null;
        var import = new ImportView(_ => { }, library => saved = library, () => { }, () => fixture.Configuration);
        host.Content = import; window.UpdateLayout();
        Click(import, "NextButton");
        Program.Check(import.CurrentStep == 0, "缺题库名称时不能继续");
        ((TextBox)import.FindName("LibraryNameInput")).Text = "网络资料测试";
        ((TextBox)import.FindName("SourceInput")).Text = "第一章\n测试原文\n第二章\n第二段测试原文";
        Click(import, "NextButton");
        ((TextBox)import.FindName("PreviewInput")).Text = "第一章\n已修正的原文\n第二章\n第二段测试原文";
        Click(import, "BackButton");
        Program.Check(((TextBox)import.FindName("SourceInput")).Text.Contains("已修正的原文"), "预览修改返回后保留");
        Click(import, "NextButton"); Click(import, "NextButton");
        var choices = ((System.Windows.Controls.Primitives.UniformGrid)import.FindName("GenerationTypes")).Children.Cast<CheckBox>().ToArray();
        Program.Check(choices.Length == 8, "出题页提供八种可独立选择的题型");
        foreach (var choice in choices) choice.IsChecked = false;
        Program.Check(!((Button)import.FindName("GenerateButton")).IsEnabled, "未选题型时不能生成");
        Complete(import.GenerateAsync(), window.Dispatcher);
        Program.Check(fixture.Server.RequestCount == 0, "未选题型时不调用 AI");
        foreach (var choice in choices) choice.IsChecked = true;
        window.Width = 1120; window.Height = 720;
        ThemeManager.Apply("深色"); Program.Capture(window, "import-types-compact-dark");
        ThemeManager.Apply("浅色"); Program.Capture(window, "import-types-compact-light");
        var reply = fixture.Server.ReplyFactory!;
        fixture.Server.ReplyFactory = request =>
        {
            fixture.Server.Status = fixture.Server.RequestCount == 2 ? 429 : 200;
            return reply(request);
        };
        var operation = import.GenerateAsync();
        Program.Check(!((Button)import.FindName("GenerateButton")).IsEnabled, "生成中避免重复操作");
        Complete(operation, window.Dispatcher);
        Program.Check(saved?.Count == 8 && saved.ConfirmedCount == 0 &&
            ((TextBlock)import.FindName("GenerationFeedback")).Text.Contains("429"), "第二批失败保留首批草稿和状态码");
        Complete(import.GenerateAsync(), window.Dispatcher);
        Program.Check(saved!.Count == 16 && saved.Questions.Count(q => q.IsSubjective) == 6 && fixture.Server.RequestCount == 3, "继续只处理未完成批次，包含三类主观题");
        var result = ((TextBlock)import.FindName("ActualCounts")).Text;
        Program.Check(result.Contains("草稿 16 道") && result.Contains("名词解释题 2") && result.Contains("输入 1000 / 输出 600") && !result.Contains("仍缺"), "结果显示实际草稿、题型分布与用量");
        Program.Check(((Button)import.FindName("GenerateButton")).Content.ToString() == "生成已完成", "全部资料处理完毕即完成");
        Complete(import.GenerateAsync(), window.Dispatcher);
        Program.Check(fixture.Server.RequestCount == 3, "已完成任务不会再次调用模型");
        Program.Capture(window, "import-generated-light");
        Click(import, "BackButton");
        Program.Check(import.CurrentLibrary!.Count == 16, "返回修正仍保留全部草稿");
        var empty = new ImportView(_ => { }, _ => throw new InvalidOperationException("空结果不能建库"), () => { }, () => fixture.Configuration);
        ((TextBox)empty.FindName("LibraryNameInput")).Text = "空结果测试";
        ((TextBox)empty.FindName("SourceInput")).Text = "仅供阅读的附录说明";
        Click(empty, "NextButton"); Click(empty, "NextButton");
        fixture.NoQuestions = true;
        Complete(empty.GenerateAsync(), window.Dispatcher);
        Program.Check(empty.CurrentLibrary is null && !((Button)empty.FindName("NextButton")).IsEnabled &&
            ((TextBlock)empty.FindName("GenerationFeedback")).Text.Contains("未生成"), "零题显示反馈且不创建空题库");
        var directory = new ImportView(_ => { }, _ => throw new InvalidOperationException("目录不能建库"), () => { }, () => fixture.Configuration);
        ((TextBox)directory.FindName("LibraryNameInput")).Text = "仅有目录";
        ((TextBox)directory.FindName("SourceInput")).Text = "目录\n第一章 网络............2";
        Click(directory, "NextButton"); Click(directory, "NextButton");
        var requests = fixture.Server.RequestCount;
        Complete(directory.GenerateAsync(), window.Dispatcher);
        Program.Check(fixture.Server.RequestCount == requests && directory.CurrentLibrary is null &&
            ((TextBlock)directory.FindName("GenerationFeedback")).Text.Contains("没有可用于出题的正文"), "只有目录时直接反馈且不请求模型");
        CheckSubjectiveReview(window, host);
    }

    private static void CheckSubjectiveReview(Window window, ContentControl host)
    {
        const string stem = "简述 TCP 的连接特点。";
        const string original = "面向连接。";
        var source = DocumentReader.FromText($"1. {stem}\n答案：{original}");
        var plan = MaterialPlanner.Create(source, true, []);
        var session = new QuestionImportSession(plan);
        object Item(string answer, string quote) => new
        {
            unitId = 1, type = "ShortAnswer", number = "1", stem, quote = stem,
            originalAnswer = answer, answerQuote = quote, referenceAnswer = "模型自行补写的答案"
        };
        string Response(object item) => JsonSerializer.Serialize(new { items = new[] { item }, answers = Array.Empty<object>(), skipped = Array.Empty<object>() });
        QuestionImportService.ParseBatch(session, plan.Batches[0], Response(Item(original, "答案：" + original)));
        Program.Check(session.Questions.Single().ReferenceAnswer == original, "识别主观题只使用可核对的原答案");
        var missing = new QuestionImportSession(plan);
        QuestionImportService.ParseBatch(missing, plan.Batches[0], Response(Item("", "")));
        var question = missing.Questions.Single();
        Program.Check(question.ReferenceAnswer == "" && question.Status == "待补全", "识别不到原答案时保持待补全");
        var database = new DatabaseStore(Program.TestPath("subjective/data/shuati.db")); database.Initialize();
        var bank = new SampleLibrary("主观题审核", "粘贴文字");
        bank.Questions.Add(question);
        var review = new ReviewView(bank, () => { }, () => { }, persist: database.SaveLibrary);
        host.Content = review; window.UpdateLayout();
        Click(review, "ConfirmQuestionButton");
        Program.Check(!question.IsConfirmed, "缺参考答案的主观题无法确认入库");
        ((TextBox)review.FindName("ReferenceAnswerInput")).Text = original;
        Click(review, "ConfirmQuestionButton");
        var restored = database.LoadLibraries().Single();
        Program.Check(restored.Questions.Single() is { IsConfirmed: true, ReferenceAnswer: original } && restored.Status == "已入库", "主观题审核及参考答案可持久化");
        Program.Check(!restored.IsReady && LearningRules.StartPractice(restored, "顺序练习", new()).Items.Count == 0 &&
            LearningRules.Shortage(restored, [0, 0, 0, 0, 0, 1]) is not null, "审核后的主观题不会进入练习或组卷");
    }

    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Complete(Task task, Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
    }
}
