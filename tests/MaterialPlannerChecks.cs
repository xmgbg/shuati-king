using ShuatiKing.Models;
using ShuatiKing.Services;

internal static class MaterialPlannerChecks
{
    internal static void Run()
    {
        var source = new SourceDocument("跨页题目", null, [new("第 1 页", "第一章 网络\n1. 哪项正确？\nA. TCP"), new("第 2 页", "B. UDP\n答案：A\n2. 判断题。")]);
        var plan = MaterialPlanner.Create(source, true, [0, 0, 0, 0]);
        var question = plan.Batches.SelectMany(batch => batch.Units).Single(unit => unit.Text.StartsWith("1."));
        Program.Check(question.Sources.Length == 2 && question.Text.Contains("答案：A"), "跨页题干选项与答案不因页界拆开");
        Program.Check(question.Sources[1].Location == "第 2 页" && question.Scope == "第一章", "切分保留来源位置和章节范围");
        source.Sections[0].Text = "后续修改";
        Program.Check(question.Text.Contains("哪项正确") && plan.SourceVersion.Length == 64, "计划冻结资料版本，不受后续预览修改影响");
        var longText = string.Concat(Enumerable.Repeat("一段知识。", 4000));
        var longSource = new SourceDocument("长文", null, [new("段落 1", longText), new("段落 2", "第二章\n新章节的知识点。")]);
        var targets = new[] { 17, 8, 3, 1 };
        var config = new AiConfiguration { Model = "model-a" };
        var knowledge = MaterialPlanner.Create(longSource, false, targets, config);
        Program.Check(knowledge.Batches.All(batch => batch.Units.Sum(unit => unit.Length) <= MaterialPlanner.BatchCharacters), "超长内容的各批大小有边界");
        Program.Check(string.Concat(knowledge.Batches.SelectMany(batch => batch.Units).Select(unit => unit.Text)) == longText + "\n第二章\n新章节的知识点。\n", "切分完整覆盖文字，无丢字或重复片段");
        Program.Check(Enumerable.Range(0, 4).All(type => knowledge.Batches.Sum(batch => batch.Targets[type]) == targets[type]), "整份资料各题型目标分配后严格守恒");
        Program.Check(knowledge.Batches.SelectMany(batch => batch.Units).Any(unit => unit.Split), "超长内容分段有标记，便于拦截残缺题目");
        targets[0] = 99; config.Model = "model-b";
        Program.Check(knowledge.Targets[0] == 17 && knowledge.Model == "model-a", "题量与模型配置也使用创建时的快照");
        var small = MaterialPlanner.Create(DocumentReader.FromText("第一章\n知识一\n第二章\n知识二"), false, [1, 0, 0, 0]);
        Program.Check(small.Batches.Sum(batch => batch.Targets.Sum()) == 1 && small.Batches.Any(batch => batch.Targets.Sum() == 0), "少量题不强迫每章出题，也不倍增目标");
    }
}
