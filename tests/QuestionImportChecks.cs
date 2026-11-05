using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class QuestionImportChecks
{
    internal const string Source = "1. TCP 属于什么协议？\nA. 传输层\nB. 网络层\n答案：A\n2. 哪些属于传输层？\nA. TCP\nB. UDP\nC. IP\n答案：AB\n3. TCP 面向连接。（判断题）\n4. TCP 中文名为____。\n答案：传输控制协议\n5. 根据下图判断线路。\n6. 简述网络分层。";
    private static AiConfiguration Config(string endpoint = "https://api.example.test/v1") => new() { Endpoint = endpoint, ApiKey = "test-secret-only", Model = "test-model" };
    internal static string Content => JsonSerializer.Serialize(new
    {
        items = new object[]
        {
            new { unitId = 1, number = "1", type = "Single", stem = "TCP 属于什么协议？", options = new[] { "传输层", "网络层" }, correctOptions = new[] { 0 }, quote = "TCP 属于什么协议？\nA. 传输层\nB. 网络层\n答案：A", originalAnswer = "A", answerQuote = "答案：A", doubt = "请核对术语", suggestion = "保留原答案供审核" },
            new { unitId = 2, number = "2", type = "Multiple", stem = "哪些属于传输层？", options = new[] { "TCP", "UDP", "IP" }, correctOptions = new[] { 0, 1 }, quote = "哪些属于传输层？\nA. TCP\nB. UDP\nC. IP\n答案：AB", originalAnswer = "AB", answerQuote = "答案：AB" },
            new { unitId = 3, number = "3", type = "Judgment", stem = "TCP 面向连接。（判断题）", quote = "TCP 面向连接。（判断题）" },
            new { unitId = 4, number = "4", type = "Blank", stem = "TCP 中文名为____。", blankAnswers = new[] { new[] { "传输控制协议" } }, quote = "TCP 中文名为____。\n答案：传输控制协议", originalAnswer = "传输控制协议", answerQuote = "答案：传输控制协议" },
            new { unitId = 5, type = "Single", stem = "根据下图判断线路。", quote = "根据下图判断线路。", dependsOnImage = true },
            new { unitId = 6, type = "Short", stem = "简述网络分层。", quote = "简述网络分层。" }
        }, answers = Array.Empty<object>(), skipped = Array.Empty<object>()
    });
    internal static string Envelope(string content, string finish = "stop") => JsonSerializer.Serialize(new
    { choices = new[] { new { finish_reason = finish, message = new { role = "assistant", content } } }, usage = new { prompt_tokens = 500, completion_tokens = 300 } });
    private static HttpResponseMessage Response(string content, int status = 200) => new((HttpStatusCode)status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => action(request, token); }

    internal static void Run() => Models().GetAwaiter().GetResult();
    private static async Task Models()
    {
        var config = Config();
        QuestionImportSession Session() => new(MaterialPlanner.Create(DocumentReader.FromText(Source), true, [0, 0, 0, 0], config));
        var requests = 0;
        using var client = new HttpClient(new Handler(async (request, token) =>
        {
            requests++;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Program.Check(request.Headers.Authorization?.Parameter == config.ApiKey && payload.RootElement.GetProperty("max_tokens").GetInt32() == 8192, "识别使用 Bearer 鉴权并设置输出上限");
            Program.Check(payload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!.Contains("TCP"), "请求发送真实资料而非示例题目");
            return Response(Envelope(Content));
        }));
        var service = new QuestionImportService(client); var session = Session();
        await service.RunAsync(session, config);
        Program.Check(session.Complete && session.Questions.Count == 4 && session.Questions.All(question => !question.IsConfirmed), "真实响应四种题型进入未确认草稿");
        Program.Check(session.Questions.Count(question => question.ValidationError is not null) == 1 && session.Questions[2].JudgmentAnswer is null, "缺原答案不由 AI 猜填，保持待补全");
        Program.Check(session.Issues.Any(issue => issue.Reason.Contains("图片")) && session.Issues.Any(issue => issue.Reason.Contains("不支持")), "图片依赖及不支持题型保留原文和位置");
        Program.Check(session.Questions[0].OriginalAnswer == "A" && session.Questions[0].CorrectOptions.SequenceEqual(new[] { 0 }) && session.Questions[0].AiSuggestion.Length > 0, "原答案与 AI 疑点建议分开保存");
        Program.Check(session.InputTokens == 500 && session.OutputTokens == 300 && !session.UsageUnknown, "记录响应实际 Token 用量");
        await service.RunAsync(session, config);
        Program.Check(requests == 1, "已完成任务不重复调用服务");
        foreach (var body in new[] { "{}", "[]", "not json", Envelope("not json"), Envelope(Content, "length") })
        {
            using var badClient = new HttpClient(new Handler((_, _) => Task.FromResult(Response(body))));
            var failed = Session();
            try { await new QuestionImportService(badClient).RunAsync(failed, config); Program.Check(false, "无效响应必须失败"); }
            catch (InvalidOperationException) { Program.Check(failed.CompletedBatches == 0 && failed.Questions.Count == 0, "截断或无效结构不写入半批题目"); }
        }
        using (var denied = new HttpClient(new Handler((_, _) => Task.FromResult(Response("test-secret-only", 401)))))
        {
            try { await new QuestionImportService(denied).RunAsync(Session(), config); Program.Check(false, "鉴权应失败"); }
            catch (InvalidOperationException error) { Program.Check(error.Message.Contains("401") && !error.Message.Contains(config.ApiKey), "错误保留实际 HTTP 状态但不泄露密钥或响应体"); }
        }
        using (var cts = new CancellationTokenSource())
        using (var delayed = new HttpClient(new Handler(async (_, token) => { cts.Cancel(); await Task.Delay(5000, token); return Response(Envelope(Content)); })))
        {
            var canceled = Session();
            try { await new QuestionImportService(delayed).RunAsync(canceled, config, cancellationToken: cts.Token); Program.Check(false, "取消应终止"); }
            catch (OperationCanceledException) { Program.Check(canceled.CompletedBatches == 0 && canceled.UsageUnknown, "取消不推进批次，并标记消耗未知"); }
        }
        using (var timeout = new HttpClient(new Handler((_, _) => throw new TaskCanceledException())))
        {
            try { await new QuestionImportService(timeout).RunAsync(Session(), config); Program.Check(false, "超时应终止"); }
            catch (InvalidOperationException error) { Program.Check(error.Message.Contains("LOCAL_TIMEOUT"), "客户端超时反馈可读"); }
        }
        var forged = Session();
        var forgedJson = System.Text.Json.Nodes.JsonNode.Parse(Content)!;
        forgedJson["items"]![0]!["quote"] = "资料之外的题目";
        QuestionImportService.ParseBatch(forged, forged.Plan.Batches[0], forgedJson.ToJsonString());
        Program.Check(forged.Questions.Count == 3 && forged.Issues.Any(issue => issue.Reason.Contains("依据")), "伪造原文依据的题目不进入草稿");
        var changedAnswer = System.Text.Json.Nodes.JsonNode.Parse(Content)!;
        changedAnswer["items"]![0]!["correctOptions"] = new System.Text.Json.Nodes.JsonArray(1);
        var protectedAnswer = Session();
        QuestionImportService.ParseBatch(protectedAnswer, protectedAnswer.Plan.Batches[0], changedAnswer.ToJsonString());
        Program.Check(protectedAnswer.Questions[0].CorrectOptions.SequenceEqual(new[] { 0 }), "模型误映射原答案时由程序按原答案字母恢复");
        foreach (var endpoint in new[] { "https://api.deepseek.com", "https://api.xiaomimimo.com/v1" })
        {
            var official = Config(endpoint);
            using var provider = new HttpClient(new Handler(async (request, token) =>
            {
                using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                Program.Check(payload.RootElement.GetProperty("thinking").GetProperty("type").GetString() == "disabled" &&
                    payload.RootElement.GetProperty(endpoint.Contains("xiaomimimo") ? "max_completion_tokens" : "max_tokens").GetInt32() == 8192,
                    "DeepSeek 与 MiMo 各自适配输出上限并关闭深度思考");
                return Response(Envelope(Content));
            }));
            await new QuestionImportService(provider).RunAsync(new(MaterialPlanner.Create(DocumentReader.FromText(Source), true, [0, 0, 0, 0], official)), official);
        }
        var answerSource = DocumentReader.FromText(Source + "\n答案汇总\n3.对");
        var withAnswerTable = new QuestionImportSession(MaterialPlanner.Create(answerSource, true, [0, 0, 0, 0], config));
        var answerJson = System.Text.Json.Nodes.JsonNode.Parse(Content)!;
        answerJson["answers"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse("""{"unitId":7,"number":"3","type":"Judgment","originalAnswer":"对","answerQuote":"3.对","judgmentAnswer":true}"""));
        QuestionImportService.ParseBatch(withAnswerTable, withAnswerTable.Plan.Batches[0], answerJson.ToJsonString());
        QuestionImportService.ResolveAnswers(withAnswerTable);
        Program.Check(withAnswerTable.Questions[2].JudgmentAnswer == true && withAnswerTable.Questions[2].OriginalAnswer == "对", "文末答案表真实解析后补齐前文题目");
        AnswerAssociation(config);
        // 两批服务第二批失败后，重试只请求未完成批次。
        var twoPlan = MaterialPlanner.Create(DocumentReader.FromText("第一章\n1. 判断甲。\n第二章\n2. 判断乙。"), true, [0, 0, 0, 0], config);
        var resumed = new QuestionImportSession(twoPlan); var calls = 0;
        using var retry = new HttpClient(new Handler((_, _) => Task.FromResult(++calls == 2 ? Response("", 429) : Response(Envelope("{\"items\":[],\"answers\":[],\"skipped\":[]}")))));
        try { await new QuestionImportService(retry).RunAsync(resumed, config); } catch (InvalidOperationException) { }
        Program.Check(resumed.CompletedBatches == 1 && calls == 2, "部分失败保留完成批次，不自动重试");
        await new QuestionImportService(retry).RunAsync(resumed, config);
        Program.Check(resumed.Complete && calls == 3, "手动继续仅请求失败批次");
    }

    private static void AnswerAssociation(AiConfiguration config)
    {
        var session = new QuestionImportSession(MaterialPlanner.Create(DocumentReader.FromText(Source), true, [0, 0, 0, 0], config));
        var a = new QuestionDraft { Type = QuestionType.Single, Stem = "甲", SourceScope = "第一章", SourceNumber = "1" };
        session.Questions.Add(a);
        session.Answers.Add(new("第二章", "1", QuestionType.Single, "B", "第 8 页：B", [1], null, []));
        QuestionImportService.ResolveAnswers(session);
        Program.Check(a.OriginalAnswer == "", "不跨章节错误匹配同号答案");
        session.Answers.Add(new("第一章", "1", QuestionType.Single, "A", "第 9 页：A", [0], null, []));
        QuestionImportService.ResolveAnswers(session);
        Program.Check(a.OriginalAnswer == "A" && a.AnswerEvidence.Contains("第 9 页"), "文末答案按章节、题号和题型关联并保留依据");
        var b = new QuestionDraft { Type = QuestionType.Judgment, Stem = "乙", SourceScope = "全文", SourceNumber = "2" };
        session.Questions.Add(b);
        session.Answers.Add(new("全文", "2", null, "对", "对", [], true, []));
        session.Answers.Add(new("全文", "2", null, "错", "错", [], false, []));
        QuestionImportService.ResolveAnswers(session);
        Program.Check(b.OriginalAnswer == "", "冲突答案不擅自选取");
    }

    internal static void Views(Window window)
    {
        var duplicated = System.Text.Json.Nodes.JsonNode.Parse(Content)!;
        duplicated["items"]!.AsArray().Add(duplicated["items"]![0]!.DeepClone());
        using var server = new AiTestServer { Body = Envelope(duplicated.ToJsonString()) };
        var config = Config(server.Endpoint);
        var host = (ContentControl)window.FindName("PageHost"); SampleLibrary? saved = null;
        var view = new ImportView(_ => { }, bank => saved = bank, () => { }, () => config);
        host.Content = view; window.UpdateLayout();
        ((RadioButton)view.FindName("ExistingMode")).IsChecked = true;
        ((TextBox)view.FindName("LibraryNameInput")).Text = "真实识别验收";
        ((TextBox)view.FindName("SourceInput")).Text = Source;
        Click(view, "NextButton"); Click(view, "NextButton");
        var task = view.GenerateAsync();
        Program.Check(!((Button)view.FindName("GenerateButton")).IsEnabled, "真实识别期间禁用重复提交");
        Complete(task, window.Dispatcher);
        Program.Check(server.RequestCount == 1 && saved?.Count == 4 && saved.IncompleteCount == 1, "界面通过真实 HTTP 完成四种题型识别");
        Program.Check(((TextBox)view.FindName("ImportReport")).Text.Contains("依赖图片") && saved!.ImportIssues.Count >= 2, "界面可查看未导入明细");
        Complete(view.GenerateAsync(), window.Dispatcher);
        Program.Check(server.RequestCount == 1 && saved!.Count == 4, "重复点击已完成识别不重复请求或添加题目");
        Program.Check(((TextBox)view.FindName("ImportReport")).Text.Contains("完全重复"), "完全重复题跳过并列出明细");
        window.Width = 1440; window.Height = 900;
        ((Expander)view.FindName("ImportReportPanel")).IsExpanded = true;
        Program.Capture(window, "recognition-report-light");
        ThemeManager.Apply("深色"); Program.Capture(window, "recognition-report-dark"); ThemeManager.Apply("浅色");
        window.Width = 1120; window.Height = 720; Program.Capture(window, "recognition-report-compact");
        window.Width = 1440; window.Height = 900;
        var review = new ReviewView(saved!, () => { }, () => { }); host.Content = review; window.UpdateLayout();
        Program.Check(((TextBox)review.FindName("SourceEvidenceText")).Text.Contains("原答案：A") && ((TextBlock)review.FindName("AnswerDoubt")).Text.Contains("核对"), "审核页展示来源、原答案和 AI 疑点");
        ((Expander)review.FindName("SourceEvidencePanel")).IsExpanded = true;
        Program.Descendants<ScrollViewer>(review).LastOrDefault(scroll => scroll.Content is StackPanel)?.ScrollToBottom();
        Program.Capture(window, "recognition-review-light");
        var noConfig = new ImportView(_ => { }, _ => { }, () => { }); host.Content = noConfig;
        ((RadioButton)noConfig.FindName("ExistingMode")).IsChecked = true;
        ((TextBox)noConfig.FindName("LibraryNameInput")).Text = "未配置测试"; ((TextBox)noConfig.FindName("SourceInput")).Text = Source;
        Click(noConfig, "NextButton"); Click(noConfig, "NextButton"); Complete(noConfig.GenerateAsync(), window.Dispatcher);
        Program.Check(noConfig.CurrentLibrary is null && ((TextBlock)noConfig.FindName("FormFeedback")).Text.Contains("AI 配置"), "未配置 AI 时不调用服务或创建空题库");
        var empty = new ImportView(_ => { }, _ => { }, () => { }, () => config); host.Content = empty;
        server.Body = Envelope("{\"items\":[],\"answers\":[],\"skipped\":[]}");
        ((RadioButton)empty.FindName("ExistingMode")).IsChecked = true;
        ((TextBox)empty.FindName("LibraryNameInput")).Text = "空结果测试"; ((TextBox)empty.FindName("SourceInput")).Text = "只有资料，没有题目。";
        Click(empty, "NextButton"); Click(empty, "NextButton"); Complete(empty.GenerateAsync(), window.Dispatcher);
        Program.Check(empty.CurrentLibrary is null && ((TextBox)empty.FindName("ImportReport")).Text.Length > 0, "零候选结果保留处理报告，不创建空题库");
    }
    private static void Click(FrameworkElement view, string name) => ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Complete(Task task, Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame(); task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
    }
}
