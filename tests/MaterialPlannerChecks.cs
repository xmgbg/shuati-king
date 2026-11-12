using ShuatiKing.Models;
using ShuatiKing.Services;

internal static class MaterialPlannerChecks
{
    internal static void Run()
    {
        var source = new SourceDocument("跨页题目", null, [new("第 1 页", "第一章 网络\n1. 哪项正确？\nA. TCP"), new("第 2 页", "B. UDP\n答案：A\n2. 判断题。")]);
        var plan = MaterialPlanner.Create(source, true, []);
        var question = plan.Batches.SelectMany(batch => batch.Units).Single(unit => unit.Text.StartsWith("1."));
        Program.Check(question.Sources.Length == 2 && question.Text.Contains("答案：A"), "跨页题干选项与答案不因页界拆开");
        Program.Check(question.Sources[1].Location == "第 2 页" && question.Scope == "第一章", "切分保留来源位置和章节范围");
        source.Sections[0].Text = "后续修改";
        Program.Check(question.Text.Contains("哪项正确") && plan.SourceVersion.Length == 64, "计划冻结资料版本，不受后续预览修改影响");
        var longText = string.Concat(Enumerable.Repeat("一段知识。", 4000));
        var longSource = new SourceDocument("长文", null, [new("段落 1", longText), new("段落 2", "第二章\n新章节的知识点。")]);
        var types = new[] { QuestionType.Single, QuestionType.TermExplanation };
        var config = new AiConfiguration { Model = "model-a" };
        var knowledge = MaterialPlanner.Create(longSource, false, types, config);
        Program.Check(knowledge.Batches.All(batch => batch.Units.Sum(unit => unit.Length) <= MaterialPlanner.GenerationBatchCharacters), "超长内容的各批大小有边界");
        Program.Check(string.Concat(knowledge.Batches.SelectMany(batch => batch.Units).Select(unit => unit.Text)) == longText + "\n第二章\n新章节的知识点。\n", "切分完整覆盖文字，无丢字或重复片段");
        Program.Check(knowledge.Batches.SelectMany(batch => batch.Units).Select(unit => unit.Id).Distinct().Count() == knowledge.Batches.Sum(batch => batch.Units.Length), "资料单元分批后仅出现一次");
        Program.Check(knowledge.Batches.SelectMany(batch => batch.Units).Any(unit => unit.Split), "超长内容分段有标记，便于拦截残缺题目");
        types[0] = QuestionType.Blank; config.Model = "model-b";
        Program.Check(knowledge.SelectedTypes.SequenceEqual([QuestionType.Single, QuestionType.TermExplanation]) && knowledge.Model == "model-a", "所选题型与模型配置也使用创建时的快照");
        var small = MaterialPlanner.Create(DocumentReader.FromText("第一章\n知识一\n第二章\n知识二"), false, [QuestionType.Single]);
        Program.Check(small.Batches.Length == 2 && small.Batches.Select(batch => batch.Units[0].Scope).SequenceEqual(["第一章", "第二章"]), "只选一个题型也覆盖各章资料");
        var textbook = new SourceDocument("含目录的教材", null,
        [
            new("第 1 页", "目录\n\n第一章 网络........2\n\n第二章 地址……3\n\n导读\n本书介绍基本术语。"),
            new("第 2 页", "第一章 网络\n\n分组交换把待传输的\n数据分成较小的单位。\n\n课后习题\n1. 什么是分组交换？\n\n2. 目的地址\n10.4.8.9 会匹配哪些路由？"),
            new("第 3 页", "第二章 地址\nIPv4 地址用于寻址。")
        ]);
        var revised = MaterialPlanner.Create(textbook, false, [QuestionType.TermExplanation]);
        var revisedUnits = revised.Batches.SelectMany(batch => batch.Units).ToArray();
        Program.Check(revised.Batches.Length == 3 && revisedUnits.All(unit => !unit.Text.Contains("........") && !unit.Text.Contains("……3")) &&
            revisedUnits.Any(unit => unit.Text.Contains("本书介绍基本术语")), "目录条目不单独请求，保留同页导读和后续正文");
        Program.Check(revisedUnits.Any(unit => unit.Text.Contains("待传输的\n数据")), "PDF 正文折行留在同一段");
        var exercises = revisedUnits.Where(unit => unit.Exercise).ToArray();
        Program.Check(exercises.Length == 2 && exercises[1].Text.Contains("目的地址\n10.4.8.9"), "课后习题单独标记并保留跨行题干");
    }
}
