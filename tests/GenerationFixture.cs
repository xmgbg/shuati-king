using System.Text.Json;
using ShuatiKing.Models;
using ShuatiKing.Services;

internal sealed class GenerationFixture : IDisposable
{
    public AiTestServer Server { get; } = new();
    public AiConfiguration Configuration { get; }
    public bool Partial { get; set; }
    private int number;
    public GenerationFixture()
    {
        Configuration = new() { Endpoint = Server.Endpoint, ApiKey = "test-only", Model = "test-model" };
        Server.ReplyFactory = request =>
        {
            using var data = JsonDocument.Parse(request.GetProperty("messages")[1].GetProperty("content").GetString()!);
            var unit = data.RootElement.GetProperty("units")[0];
            var targets = data.RootElement.GetProperty("targets").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var items = new List<object>();
            for (var type = 0; type < 4; type++)
                for (var i = 0; i < (Partial ? (targets[type] + 1) / 2 : targets[type]); i++)
                {
                    var q = QuestionDraft.Example((QuestionType)type, "测试返回", ++number);
                    items.Add(new { unitId = unit.GetProperty("unitId").GetInt32(), type = q.Type.ToString(), stem = q.Stem,
                        options = q.Options, correctOptions = q.CorrectOptions, judgmentAnswer = q.JudgmentAnswer, blankAnswers = q.BlankAnswers,
                        explanation = "测试服务返回的解析", quote = unit.GetProperty("text").GetString()!.Trim() });
                }
            return QuestionImportChecks.Envelope(JsonSerializer.Serialize(new { items, reasons = Partial ? new[] { "资料不足，部分题目未生成" } : Array.Empty<string>() }));
        };
    }
    public void Save(SettingsStore store) => store.Save(new LocalSettings { Configurations = [Configuration], SelectedConfigurationId = Configuration.Id });
    public void Dispose() => Server.Dispose();
}
