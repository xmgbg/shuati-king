using System.Text.Json.Serialization;

namespace ShuatiKing.Models;

public sealed class AnswerItem
{
    public QuestionDraft Question { get; set; } = new();
    public List<int> Selected { get; set; } = [];
    public bool? Judgment { get; set; }
    public List<string> Blanks { get; set; } = [];
    public bool Submitted { get; set; }
    public bool Correct { get; set; }
    public decimal Points { get; set; } = 1;
    [JsonIgnore] public bool Answered => Selected.Count > 0 || Judgment is not null || Blanks.Any(s => !string.IsNullOrWhiteSpace(s));
}

public sealed class PracticeSession
{
    public Guid BankId { get; set; }
    public string BankName { get; set; } = "";
    public string Mode { get; set; } = "顺序练习";
    public List<AnswerItem> Items { get; set; } = [];
    public int Index { get; set; }
    [JsonIgnore] public bool Complete => Items.Count > 0 && Items.All(i => i.Submitted);
}

public sealed class ExamSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public Guid BankId { get; set; }
    public string BankName { get; set; } = "";
    public List<AnswerItem> Items { get; set; } = [];
    public int Index { get; set; }
    public double DurationSeconds { get; set; }
    public double RemainingSeconds { get; set; }
    public bool Paused { get; set; }
    public bool Finished { get; set; }
    public bool TimedOut { get; set; }
    [JsonIgnore] public decimal Total => Items.Sum(i => i.Points);
    [JsonIgnore] public decimal Score => Items.Where(i => i.Correct).Sum(i => i.Points);
    public void Advance(double seconds)
    {
        if (!Paused && !Finished) RemainingSeconds = Math.Max(0, RemainingSeconds - Math.Max(0, seconds));
    }
}

public sealed class WrongEntry
{
    public Guid BankId { get; set; }
    public string BankName { get; set; } = "";
    public QuestionDraft Question { get; set; } = new();
    [JsonIgnore] public string Label => BankName + " · " + Question.TypeName;
}

public sealed class LearningState
{
    public PracticeSession? Practice { get; set; }
    public ExamSession? Exam { get; set; }
    public List<WrongEntry> Wrongs { get; set; } = [];
    public List<ExamSession> ExamHistory { get; set; } = [];
}
