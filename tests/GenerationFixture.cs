using System.Text.Json;
using ShuatiKing.Models;
using ShuatiKing.Services;

internal sealed class GenerationFixture : IDisposable
{
    public AiTestServer Server { get; } = new();
    public AiConfiguration Configuration { get; }
    public bool NoQuestions { get; set; }
    private int number;
    public GenerationFixture()
    {
        Configuration = new() { Endpoint = Server.Endpoint, ApiKey = "test-only", Model = "test-model" };
        Server.ReplyFactory = request =>
        {
            using var data = JsonDocument.Parse(request.GetProperty("messages")[1].GetProperty("content").GetString()!);
            var unit = data.RootElement.GetProperty("units")[0];
            var types = data.RootElement.GetProperty("selectedTypes").EnumerateArray()
                .Select(value => Enum.Parse<QuestionType>(value.GetString()!));
            var items = new List<object>();
            if (!NoQuestions)
                foreach (var type in types)
                {
                    var q = QuestionDraft.Example(type, "测试返回", ++number);
                    items.Add(new { unitId = unit.GetProperty("unitId").GetInt32(), type = q.Type.ToString(), stem = q.Stem,
                        options = q.Options, correctOptions = q.CorrectOptions, judgmentAnswer = q.JudgmentAnswer, blankAnswers = q.BlankAnswers,
                        calculationAnswer = q.CalculationAnswer, calculationTolerance = q.CalculationTolerance, referenceAnswer = q.ReferenceAnswer,
                        explanation = "测试服务返回的解析", quote = unit.GetProperty("text").GetString()!.Trim() });
                }
            return QuestionImportChecks.Envelope(JsonSerializer.Serialize(new { items, reasons = NoQuestions ? new[] { "没有适合所选题型的考点" } : Array.Empty<string>() }));
        };
    }
    public void Save(SettingsStore store) => store.Save(new LocalSettings { Configurations = [Configuration], SelectedConfigurationId = Configuration.Id });
    public void Dispose() => Server.Dispose();
}
