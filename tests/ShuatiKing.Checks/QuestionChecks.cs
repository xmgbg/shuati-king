using System.Text.Json;
using ShuatiKing.Models;
using ShuatiKing.Services;

internal static class QuestionChecks
{
    internal static void Run()
    {
        foreach (var type in Enum.GetValues<QuestionType>())
            Program.Check(QuestionDraft.Example(type, "审核校验").ValidationError is null, "四种题型的完整答案可审核");
        var multiple = QuestionDraft.Example(QuestionType.Multiple, "测试");
        multiple.CorrectOptions = [0];
        Program.Check(multiple.ValidationError is not null, "多选缺少第二个答案不能确认");
        var blank = QuestionDraft.Example(QuestionType.Blank, "测试");
        blank.BlankAnswers[1] = [];
        Program.Check(blank.ValidationError!.Contains("第 2"), "多空题缺答案定位到具体空");
        blank.BlankAnswers.RemoveAt(1);
        Program.Check(blank.ValidationError!.Contains("2 个空"), "题干与答案空数不一致被拦截");
        var pending = new SampleLibrary("测试资料", "source.pdf") { SourceText = "SOURCE_TEXT_MUST_NOT_EXPORT" };
        pending.Questions.Add(QuestionDraft.Example(QuestionType.Single, "完整草稿"));
        Program.Check(!pending.IsReady, "草稿完整但未确认时不可练习");
        pending.Questions[0].IsConfirmed = true;
        pending.Questions.Add(blank);
        using var package = JsonDocument.Parse(WorkflowPreview.ExportPayload(pending));
        Program.Check(package.RootElement.GetProperty("questions").GetArrayLength() == 1, "导出预览只包含合格已入库题目");
        Program.Check(!WorkflowPreview.ExportPayload(pending).Contains("SOURCE_TEXT_MUST_NOT_EXPORT"), "导出预览不包含原文");
        var missing = WorkflowPreview.MissingQuestions(pending, [2, 1, 1, 1]);
        Program.Check(missing.Count == 3 && pending.Count == 2, "补充生成只计算缺少题目且不改变已有草稿");
    }
}
