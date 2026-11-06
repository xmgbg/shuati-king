using System.Text.Json;
using ShuatiKing.Models;

namespace ShuatiKing.Services;

public static class QuestionRules
{
    public static bool Same(QuestionDraft left, QuestionDraft right) => Signature(left) == Signature(right);
    private static string Signature(QuestionDraft q) => JsonSerializer.Serialize(new
    {
        q.Type, q.Stem, q.Options, Correct = q.CorrectOptions.Order().ToArray(), q.JudgmentAnswer,
        Blank = q.BlankAnswers.Select(values => values.Order(StringComparer.Ordinal).ToArray()).ToArray()
    });
    public static List<ImportIssue> RemoveDuplicateDrafts(SampleLibrary library, IEnumerable<Guid> importedIds)
    {
        var candidates = importedIds.ToHashSet(); var retained = new List<QuestionDraft>(); var issues = new List<ImportIssue>();
        foreach (var question in library.Questions.OrderByDescending(question => question.IsConfirmed).ToList())
        {
            if (!question.IsConfirmed && candidates.Contains(question.Id) && retained.Any(other => Same(other, question)))
            {
                library.Questions.Remove(question);
                issues.Add(new(question.SourceEvidence.Split('\n')[0], "跳过完全重复题", question.Stem));
            }
            else retained.Add(question);
        }
        return issues;
    }
    public static string? ConfirmationError(SampleLibrary library, QuestionDraft question) => question.ValidationError ??
        (library.Questions.Any(other => other.Id != question.Id && other.IsConfirmed && Same(other, question)) ? "题库中已有完全相同的已入库题目。" : null);
}
