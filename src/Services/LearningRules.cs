using ShuatiKing.Models;

namespace ShuatiKing.Services;

public static class LearningRules
{
    public static QuestionDraft Snapshot(QuestionDraft q) => new()
    {
        Id = q.Id, Type = q.Type, Stem = q.Stem, Options = [.. q.Options], CorrectOptions = [.. q.CorrectOptions],
        JudgmentAnswer = q.JudgmentAnswer, BlankAnswers = q.BlankAnswers.Select(a => a.ToList()).ToList(), Explanation = q.Explanation, IsConfirmed = true
    };

    public static bool IsCorrect(AnswerItem answer)
    {
        var q = answer.Question;
        return q.Type switch
        {
            QuestionType.Single or QuestionType.Multiple => answer.Selected.Count == q.CorrectOptions.Count && answer.Selected.ToHashSet().SetEquals(q.CorrectOptions),
            QuestionType.Judgment => answer.Judgment is not null && answer.Judgment == q.JudgmentAnswer,
            _ => answer.Blanks.Count == q.BlankAnswers.Count && q.BlankAnswers.Select((aliases, i) => aliases.Any(a => string.Equals(a.Trim(), answer.Blanks[i].Trim(), StringComparison.OrdinalIgnoreCase))).All(x => x)
        };
    }

    public static string CorrectText(QuestionDraft q) => q.Type switch
    {
        QuestionType.Single or QuestionType.Multiple => string.Join("、", q.CorrectOptions.Select(i => ((char)('A' + i)).ToString())),
        QuestionType.Judgment => q.JudgmentAnswer == true ? "正确" : "错误",
        _ => string.Join("\n", q.BlankAnswers.Select((a, i) => $"第 {i + 1} 空：" + string.Join(" / ", a)))
    };

    public static bool Record(LearningState state, Guid bankId, string bankName, AnswerItem answer)
    {
        answer.Correct = IsCorrect(answer); answer.Submitted = true;
        var prior = state.Wrongs.Find(w => w.BankId == bankId && w.Question.Id == answer.Question.Id);
        if (answer.Correct) { if (prior is not null) state.Wrongs.Remove(prior); return prior is not null; }
        if (prior is null) state.Wrongs.Add(new() { BankId = bankId, BankName = bankName, Question = Snapshot(answer.Question) });
        else prior.Question = Snapshot(answer.Question);
        return false;
    }

    public static PracticeSession StartPractice(SampleLibrary bank, string mode, LearningState state)
    {
        var questions = mode == "错题重练" ? state.Wrongs.Where(w => w.BankId == bank.Id).Select(w => w.Question).ToArray()
            : bank.Questions.Where(q => q.IsConfirmed && q.ValidationError is null).ToArray();
        if (mode == "随机练习") Random.Shared.Shuffle(questions);
        return new() { BankId = bank.Id, BankName = bank.Name, Mode = mode, Items = questions.Select(q => new AnswerItem { Question = Snapshot(q) }).ToList() };
    }

    public static string? Shortage(SampleLibrary bank, int[] counts)
    {
        var names = new[] { "单选题", "多选题", "判断题", "填空题" };
        var missing = new List<string>();
        for (var t = 0; t < 4; t++)
        {
            int available = bank.Questions.Count(q => q.Type == (QuestionType)t && q.IsConfirmed && q.ValidationError is null);
            if (counts[t] > available) missing.Add($"{names[t]}缺 {counts[t] - available} 道（可用 {available} 道）");
        }
        return missing.Count > 0 ? string.Join("；", missing) + "。请调整题量后再开始，不会重复抽题。" : null;
    }

    public static ExamSession CreateExam(SampleLibrary bank, int[] counts, decimal[] scores, int minutes, QuestionType[]? typeOrder = null)
    {
        if (counts.Length != 4 || scores.Length != 4 || counts.Any(c => c < 0) || counts.Sum() == 0 || scores.Any(s => s <= 0) || minutes <= 0) throw new ArgumentException("组卷设置不完整。");
        if (Shortage(bank, counts) is { } missing) throw new ArgumentException(missing);
        var order = typeOrder ?? Enum.GetValues<QuestionType>();
        if (order.Any(t => !Enum.IsDefined(t)) || order.Distinct().Count() != order.Length || Enumerable.Range(0, 4).Any(t => counts[t] > 0 && !order.Contains((QuestionType)t)))
            throw new ArgumentException("题型顺序须包含全部已选题型，且不能重复。");
        var items = new List<AnswerItem>();
        foreach (var type in order)
        {
            int t = (int)type;
            var pool = bank.Questions.Where(q => q.Type == (QuestionType)t && q.IsConfirmed && q.ValidationError is null).ToArray();
            Random.Shared.Shuffle(pool);
            items.AddRange(pool.Take(counts[t]).Select(q => new AnswerItem { Question = Snapshot(q), Points = scores[t] }));
        }
        return new() { BankId = bank.Id, BankName = bank.Name, Items = items, StartedAt = DateTimeOffset.Now, DurationSeconds = minutes * 60.0, RemainingSeconds = minutes * 60.0 };
    }

    public static void FinishExam(LearningState state, ExamSession exam, bool timeout)
    {
        if (exam.Finished) return;
        foreach (var item in exam.Items) Record(state, exam.BankId, exam.BankName, item);
        exam.Finished = true; exam.Paused = false; exam.TimedOut = timeout; exam.FinishedAt = DateTimeOffset.Now;
        ArchiveExam(state, exam);
    }
    public static AnswerItem SnapshotAnswer(AnswerItem item) => new()
    {
        Question = Snapshot(item.Question), Selected = [.. item.Selected], Judgment = item.Judgment, Blanks = [.. item.Blanks],
        Submitted = item.Submitted, Correct = item.Correct, Points = item.Points
    };

    public static void ArchiveExam(LearningState state, ExamSession exam)
    {
        if (!exam.Finished || state.ExamHistory.Any(saved => saved.Id == exam.Id)) return;
        state.ExamHistory.Add(new()
        {
            Id = exam.Id, BankId = exam.BankId, BankName = exam.BankName, StartedAt = exam.StartedAt, FinishedAt = exam.FinishedAt,
            Items = exam.Items.Select(SnapshotAnswer).ToList(), Index = 0,
            DurationSeconds = exam.DurationSeconds, RemainingSeconds = exam.RemainingSeconds,
            Paused = false, Finished = true, TimedOut = exam.TimedOut
        });
    }
}
