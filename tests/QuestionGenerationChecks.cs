using System.Text.Json;
using System.Text.Json.Nodes;
using ShuatiKing.Models;
using ShuatiKing.Services;

internal static class QuestionGenerationChecks
{
    internal static void Run() => RunAsync().GetAwaiter().GetResult();
    private static async Task RunAsync()
    {
        using var fixture = new GenerationFixture();
        var source = DocumentReader.FromText("TCP 是传输层协议，面向连接。UDP 也是传输层协议。TCP 中文名为传输控制协议。");
        var types = Enum.GetValues<QuestionType>();
        var longSource = DocumentReader.FromText("第一章\nTCP 是面向连接的协议。\n第二章\nUDP 是无连接的协议。");
        var plan = MaterialPlanner.Create(longSource, false, types, fixture.Configuration);
        var session = new QuestionImportSession(plan);
        var submittedUnits = new List<int>();
        var reply = fixture.Server.ReplyFactory!;
        fixture.Server.ReplyFactory = request =>
        {
            var material = request.GetProperty("messages")[1].GetProperty("content").GetString()!;
            Program.Check(material.Contains("协议", StringComparison.Ordinal), "请求正文直接使用中文，避免将汉字变成模型可见的转义字符");
            using var data = JsonDocument.Parse(material);
            var root = data.RootElement;
            Program.Check(!root.TryGetProperty("targets", out _) &&
                root.GetProperty("selectedTypes").EnumerateArray().Select(value => value.GetString()).SequenceEqual(types.Select(type => type.ToString())),
                "知识资料请求只发送题型选项，没有指定题数");
            submittedUnits.AddRange(root.GetProperty("units").EnumerateArray().Select(unit => unit.GetProperty("unitId").GetInt32()));
            return reply(request);
        };
        await new QuestionImportService().RunAsync(session, fixture.Configuration);
        Program.Check(fixture.Server.RequestCount == plan.Batches.Length && submittedUnits.Distinct().Count() == submittedUnits.Count, "各章节只请求一次，不因选择多个题型重复发送资料");
        Program.Check(session.Questions.Select(q => q.Type).Distinct().Count() == types.Length &&
            session.Questions.All(q => !q.IsConfirmed && q.ValidationError is null && q.SourceEvidence.Length > 0), "HTTP 响应的八种题型均可进入审核");
        Program.Check(session.Questions.Where(q => q.Type == QuestionType.MaterialAnalysis)
            .All(q => q.Stem.StartsWith(q.SourceEvidence.Split('\n')[1], StringComparison.Ordinal)), "材料分析题显示可核对的材料原文");
        var one = MaterialPlanner.Create(source, false, [QuestionType.Single], fixture.Configuration);
        object Item(string quote, int[] correct) => new { unitId = 1, type = "Single", stem = "TCP 属于哪一层？", options = new[] { "传输层", "网络层" }, correctOptions = correct, explanation = "依据资料 TCP 是传输层协议。", quote };
        string Payload(object[] items) => JsonSerializer.Serialize(new { items, reasons = Array.Empty<string>() });
        var valid = Item(source.Text, [0]);
        var duplicates = new QuestionImportSession(one);
        QuestionImportService.ParseGenerated(duplicates, one.Batches[0], Payload([valid, valid]));
        Program.Check(duplicates.Questions.Count == 1 && duplicates.Issues.Count > 0, "完全重复的生成结果只保留一份");
        var invalid = new QuestionImportSession(one);
        QuestionImportService.ParseGenerated(invalid, one.Batches[0], Payload([Item("不存在的资料", [0]), Item(source.Text, [4]), Item(source.Text, [0, 1])]));
        Program.Check(invalid.Questions.Count == 0 && invalid.Issues.Count == 3, "伪造来源、越界答案和单选多答案被拒绝并报告");
        var missing = new QuestionImportSession(one);
        QuestionImportService.ParseGenerated(missing, one.Batches[0], Payload([]));
        Program.Check(missing.Questions.Count == 0 && missing.Issues.Count == 0, "模型允许返回零题，不产生缺额");
        var manyResults = new QuestionImportSession(one);
        var many = Enumerable.Range(1, 12).Select(i => new
        {
            unitId = 1, type = "Single", stem = $"第 {i} 个问题", options = new[] { "传输层", "网络层" },
            correctOptions = new[] { 0 }, explanation = "测试解析", quote = source.Text
        }).ToArray();
        QuestionImportService.ParseGenerated(manyResults, one.Batches[0], Payload(many));
        Program.Check(manyResults.Questions.Count == 12 && manyResults.Issues.Count == 0, "单批超过十道的有效结果按实际数量保留");
        CheckPdfStyleResults();
        var forbidden = JsonNode.Parse(Payload([valid]))!; forbidden["items"]![0]!["type"] = "Judgment";
        forbidden["items"]![0]!["judgmentAnswer"] = true;
        var zeroType = new QuestionImportSession(one);
        QuestionImportService.ParseGenerated(zeroType, one.Batches[0], forbidden.ToJsonString());
        Program.Check(zeroType.Questions.Count == 0, "未请求的题型不能混入结果");
        var crossBatch = new QuestionImportSession(one); crossBatch.Questions.Add(new QuestionDraft { Type = QuestionType.Single, Stem = "TCP 属于哪一层？", Options = ["传输层", "网络层"], CorrectOptions = [0] });
        QuestionImportService.ParseGenerated(crossBatch, one.Batches[0], Payload([valid]));
        Program.Check(crossBatch.Questions.Count == 1 && crossBatch.Issues.Any(issue => issue.Reason.Contains("重复")), "跨批次完全重复不再加入");
        var blank = new QuestionDraft { Type = QuestionType.Blank, Stem = "____和____", BlankAnswers = [["甲"], []] };
        Program.Check(blank.ValidationError is not null && new QuestionDraft { Type = (QuestionType)77, Stem = "非法题型" }.ValidationError is not null, "缺失填空答案和非法题型不能入库");
    }

    private static void CheckPdfStyleResults()
    {
        var source = new SourceDocument("折行教材", null,
        [
            new("第 1 页", "第一章 网络\n\n分组交换把待传输的数据分成较小的"),
            new("第 2 页", "2\n单位，在网络中逐段转发。\n\n吞吐量是实际测得的单位时间传输速率。\n\n课后习题\n1. 试解释分组交换。")
        ]);
        var plan = MaterialPlanner.Create(source, false, [QuestionType.TermExplanation]);
        var session = new QuestionImportSession(plan);
        var batch = plan.Batches.Single();
        var exercise = batch.Units.Single(unit => unit.Exercise);
        var items = new[]
        {
            new { unitId = "2", type = "TermExplanation", stem = "什么是分组交换？", explanation = "正文给出了定义。", quote = "分组交换把待传输的数据分成较小的单位，在网络中逐段转发。", referenceAnswer = "把数据分成较小的单位，在网络中逐段转发。" },
            new { unitId = "999", type = "TermExplanation", stem = "解释吞吐量。", explanation = "正文给出了定义。", quote = "吞吐量是实际测得的单位时间传输速率。", referenceAnswer = "实际测得的单位时间传输速率。" },
            new { unitId = exercise.Id.ToString(), type = "TermExplanation", stem = "习题依据测试", explanation = "这里只引用题目。", quote = "试解释分组交换。", referenceAnswer = "无正文依据。" },
            new { unitId = "2", type = "TermExplanation", stem = "伪造引用测试", explanation = "不能改写原文。", quote = "分组交换把待传输的数据分成较大的单位，在网络中逐段转发。", referenceAnswer = "无正文依据。" }
        };
        var fence = new string((char)96, 3);
        var content = fence + "json\n" + JsonSerializer.Serialize(new { items, reasons = "测试说明" }) + "\n" + fence;
        QuestionImportService.ParseGenerated(session, batch, content);
        Program.Check(session.Questions.Count == 2 && session.Questions.All(q => q.Type == QuestionType.TermExplanation), "字符串编号、Markdown 包裹和普通说明文本不丢弃合格名词解释");
        Program.Check(session.Questions[0].SourceEvidence.StartsWith("第 1 页、第 2 页\n", StringComparison.Ordinal) &&
            session.Questions[1].SourceEvidence.StartsWith("第 2 页\n", StringComparison.Ordinal), "跨页原文和唯一的错号引用可核对到实际页码");
        Program.Check(session.Issues.Count(issue => issue.Reason.Contains("来源引用")) == 2, "仅引用习题或改动原文的候选题仍被拦截");
        var minimal = new QuestionImportSession(plan);
        QuestionImportService.ParseGenerated(minimal, batch, JsonSerializer.Serialize(new { items = items.Take(1) }));
        Program.Check(minimal.Questions.Count == 1, "省略说明字段时保留有效题目");
    }
}
