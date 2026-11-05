namespace ShuatiKing.Models;

public sealed record ImportIssue(string Location, string Reason, string OriginalText);
public sealed class QuestionImportSession(MaterialPlan plan)
{
    public MaterialPlan Plan { get; } = plan;
    public int CompletedBatches { get; set; }
    public List<QuestionDraft> Questions { get; } = [];
    public List<ImportedAnswer> Answers { get; } = [];
    public List<ImportIssue> Issues { get; } = [];
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public bool UsageUnknown { get; set; }
    public bool Complete => CompletedBatches == Plan.Batches.Length;
}
public sealed record ImportedAnswer(string Scope, string Number, QuestionType? Type, string Original,
    string Evidence, List<int> CorrectOptions, bool? JudgmentAnswer, List<List<string>> BlankAnswers, string Doubt = "", string Suggestion = "");
