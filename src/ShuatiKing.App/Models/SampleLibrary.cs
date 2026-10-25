using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ShuatiKing.Models;

public sealed class SampleLibrary : INotifyPropertyChanged
{
    public string Name { get; private set; }
    public string Source { get; private set; }
    public string Color { get; }
    public string SourceText { get; set; } = "";
    public ObservableCollection<QuestionDraft> Questions { get; } = [];
    public int Single => Questions.Count(q => q.Type == QuestionType.Single);
    public int Multiple => Questions.Count(q => q.Type == QuestionType.Multiple);
    public int Judgment => Questions.Count(q => q.Type == QuestionType.Judgment);
    public int Blank => Questions.Count(q => q.Type == QuestionType.Blank);
    public int Count => Questions.Count;
    public int ConfirmedCount => Questions.Count(q => q.IsConfirmed && q.ValidationError is null);
    public int IncompleteCount => Questions.Count(q => !q.IsConfirmed && q.ValidationError is not null);
    public bool IsReady => ConfirmedCount > 0;
    public string Status => IsReady ? "可练习" : "待审核";
    public string Action => IsReady ? "查看与审核题目  ›" : "审核题目  ›";
    public string ReviewSummary => $"已入库 {ConfirmedCount} · 待审核 {Count - ConfirmedCount} · 待补全 {IncompleteCount}";

    public SampleLibrary(string name, string source, int single = 0, int multiple = 0, int judgment = 0, int blank = 0, bool isReady = false, string color = "#0866FF")
    {
        Name = name; Source = source; Color = color;
        var counts = new[] { single, multiple, judgment, blank };
        for (var type = 0; type < counts.Length; type++)
        for (var i = 0; i < counts[type]; i++)
        {
            var question = QuestionDraft.Example((QuestionType)type, name, i + 1);
            question.IsConfirmed = isReady;
            Questions.Add(question);
        }
    }

    public void UpdateSource(string name, string source, string text)
    { Name = name; Source = source; SourceText = text; NotifyChanged(); }

    public void NotifyChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
    public event PropertyChangedEventHandler? PropertyChanged;

    public static SampleLibrary[] CreateExamples() =>
    [
        new("计算机网络基础", "计算机网络复习.pdf", 60, 28, 20, 20, true),
        new("大学英语词汇", "英语词汇.docx", 40, 16, 10, 20, true, "#7147EB"),
        new("教育心理学", "教育心理学.pdf", 30, 14, 10, 10, true, "#099F68"),
        new("计算机组成原理", "粘贴文字", 16, 8, 4, 4, false, "#EF970C")
    ];
}
