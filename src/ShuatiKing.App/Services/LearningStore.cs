using System.IO;
using System.Text.Json;
using ShuatiKing.Models;

namespace ShuatiKing.Services;

public sealed class LearningStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { IgnoreReadOnlyProperties = true };
    public LearningState Load()
    {
        if (!File.Exists(path)) return new();
        var state = JsonSerializer.Deserialize<LearningState>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("学习记录为空。");
        if (state.Exam is { Finished: false } exam) exam.Paused = true;
        if (state.Exam is { Finished: true } completed) LearningRules.ArchiveExam(state, completed);
        return state;
    }
    public void Save(LearningState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(state, Options));
        File.Move(path + ".tmp", path, true);
    }
}
