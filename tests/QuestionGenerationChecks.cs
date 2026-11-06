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
        var plan = MaterialPlanner.ForGeneration(MaterialPlanner.Create(source, false, [13, 3, 2, 1], fixture.Configuration));
        Program.Check(plan.Batches.All(batch => batch.Targets.Sum() <= 10) && plan.Batches.Sum(batch => batch.Targets.Sum()) == 19, "大题量拆成最多十题的请求，整份总量不变");
        var session = new QuestionImportSession(plan);
        await new QuestionImportService().RunAsync(session, fixture.Configuration);
        Program.Check(session.Questions.Count == 19 && session.Questions.All(q => !q.IsConfirmed && q.ValidationError is null && q.SourceEvidence.Length > 0), "知识资料真实 HTTP 请求生成四种题型及来源，均为草稿");
        var one = MaterialPlanner.ForGeneration(MaterialPlanner.Create(source, false, [1, 0, 0, 0], fixture.Configuration));
        object Item(string quote, int[] correct) => new { unitId = 1, type = "Single", stem = "TCP 属于哪一层？", options = new[] { "传输层", "网络层" }, correctOptions = correct, explanation = "依据资料 TCP 是传输层协议。", quote };
        string Payload(object[] items) => JsonSerializer.Serialize(new { items, reasons = Array.Empty<string>() });
        var valid = Item(source.Text, [0]);
        var duplicates = new QuestionImportSession(one);
        QuestionImportService.ParseGenerated(duplicates, one.Batches[0], Payload([valid, valid]));
        Program.Check(duplicates.Questions.Count == 1 && duplicates.Issues.Count > 0, "生成结果超量或重复时只接收目标题量");
        var invalid = new QuestionImportSession(one);
        QuestionImportService.ParseGenerated(invalid, one.Batches[0], Payload([Item("不存在的资料", [0]), Item(source.Text, [4]), Item(source.Text, [0, 1])]));
        Program.Check(invalid.Questions.Count == 0 && invalid.Issues.Any(issue => issue.Reason == "题量不足"), "伪造来源、越界答案和单选多答案被拒绝，报告不足");
        var missing = new QuestionImportSession(one);
        QuestionImportService.ParseGenerated(missing, one.Batches[0], Payload([]));
        Program.Check(missing.Questions.Count == 0 && missing.Issues.Count == 1, "空返回保留不足说明，不凑题");
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
}
