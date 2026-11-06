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
    public static string? ConfirmationError(SampleLibrary library, QuestionDraft question) => question.ValidationError ??
        (library.Questions.Any(other => other.Id != question.Id && other.IsConfirmed && Same(other, question)) ? "题库中已有完全相同的已入库题目。" : null);
}
