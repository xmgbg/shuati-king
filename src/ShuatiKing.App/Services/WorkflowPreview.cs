using System.Text.Json;
using ShuatiKing.Models;

namespace ShuatiKing.Services;

// 仅供前端流程演示；不解析文件、不调用 AI、不写出题库包。
public static class WorkflowPreview
{
    public static List<QuestionDraft> MissingQuestions(SampleLibrary library, int[] targets)
    {
        var result = new List<QuestionDraft>();
        for (var i = 0; i < 4; i++)
        {
            var existing = library.Questions.Count(q => q.Type == (QuestionType)i);
            for (var n = existing; n < targets[i]; n++) result.Add(QuestionDraft.Example((QuestionType)i, library.Name, n + 1));
        }
        return result;
    }

    public static List<QuestionDraft> ExistingExamples(string topic)
    {
        var questions = Enum.GetValues<QuestionType>().Select(t => QuestionDraft.Example(t, topic)).ToList();
        questions[2].JudgmentAnswer = null;
        return questions;
    }

    // 通过白名单创建导出预览，排除原文、待审核草稿、选择状态与个人设置。
    public static string ExportPayload(SampleLibrary library) => JsonSerializer.Serialize(new
    {
        sourceName = library.Source,
        questions = library.Questions.Where(q => q.IsConfirmed && q.ValidationError is null).Select(q => new
        {
            type = q.TypeName, stem = q.Stem, options = q.Options, correctOptions = q.CorrectOptions,
            judgmentAnswer = q.JudgmentAnswer, blankAnswers = q.BlankAnswers, explanation = q.Explanation
        })
    }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
