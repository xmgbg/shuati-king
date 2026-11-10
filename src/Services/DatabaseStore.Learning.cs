using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ShuatiKing.Models;

namespace ShuatiKing.Services;

public sealed partial class DatabaseStore
{
    public void InitializeLearning()
    {
        using var connection = Open();
        Execute(connection, null, "CREATE TABLE IF NOT EXISTS learning_records(kind TEXT NOT NULL,id TEXT NOT NULL,bank_id TEXT NOT NULL,ordinal INTEGER NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(kind,id))");
    }
    public void MigrateLearning(string legacyPath)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using (var marker = connection.CreateCommand())
        {
            marker.Transaction = transaction; marker.CommandText = "SELECT value FROM metadata WHERE key='learning_migrated'";
            if (marker.ExecuteScalar() is not null) return;
        }
        if (File.Exists(legacyPath))
        {
            var bytes = File.ReadAllBytes(legacyPath);
            using var json = JsonDocument.Parse(bytes);
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("Wrongs", out var wrongs) || wrongs.ValueKind != JsonValueKind.Array ||
                !json.RootElement.TryGetProperty("Practice", out _) && !json.RootElement.TryGetProperty("Exam", out _))
                throw new InvalidDataException("旧学习记录结构不完整，请修复文件后重试，原文件未修改。");
            var state = JsonSerializer.Deserialize<LearningState>(bytes, JsonOptions) ?? throw new InvalidDataException("旧学习记录为空。");
            ValidateLearning(state); NormalizeLearning(state);
            var backup = legacyPath + ".migration-backup";
            if (File.Exists(backup) && !File.ReadAllBytes(backup).SequenceEqual(bytes)) backup += "-" + Guid.NewGuid().ToString("N");
            if (!File.Exists(backup)) File.WriteAllBytes(backup, bytes);
            WriteLearning(connection, transaction, state);
            RestoreLegacyLibraries(connection, transaction, state);
            Execute(connection, transaction, "INSERT INTO metadata VALUES('learning_backup',$path)", ("$path", backup));
        }
        Execute(connection, transaction, "INSERT INTO metadata VALUES('learning_migrated','1')");
        transaction.Commit();
    }
    public LearningState LoadLearning()
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT kind,payload FROM learning_records ORDER BY ordinal";
        using var rows = command.ExecuteReader(); var state = new LearningState();
        while (rows.Read())
        {
            var payload = rows.GetString(1);
            switch (rows.GetString(0))
            {
                case "practice": state.Practice = JsonSerializer.Deserialize<PracticeSession>(payload, JsonOptions) ?? throw new InvalidDataException("练习记录为空。"); break;
                case "exam": state.Exam = JsonSerializer.Deserialize<ExamSession>(payload, JsonOptions) ?? throw new InvalidDataException("考试记录为空。"); break;
                case "wrong": state.Wrongs.Add(JsonSerializer.Deserialize<WrongEntry>(payload, JsonOptions) ?? throw new InvalidDataException("错题为空。")); break;
                case "history": state.ExamHistory.Add(JsonSerializer.Deserialize<ExamSession>(payload, JsonOptions) ?? throw new InvalidDataException("考试历史为空。")); break;
                default: throw new InvalidDataException("无法识别的学习记录类型。");
            }
        }
        ValidateLearning(state); NormalizeLearning(state); return state;
    }
    public void SaveLearning(LearningState state)
    {
        ValidateLearning(state);
        using var connection = Open(); using var transaction = connection.BeginTransaction();
        WriteLearning(connection, transaction, state); transaction.Commit();
    }
    private static void WriteLearning(SqliteConnection connection, SqliteTransaction transaction, LearningState state)
    {
        Execute(connection, transaction, "DELETE FROM learning_records");
        void Write(string kind, string id, Guid bank, int ordinal, object value) => Execute(connection, transaction,
            "INSERT INTO learning_records VALUES($kind,$id,$bank,$ordinal,$payload)", ("$kind", kind), ("$id", id), ("$bank", bank.ToString()), ("$ordinal", ordinal), ("$payload", JsonSerializer.Serialize(value, value.GetType(), JsonOptions)));
        if (state.Practice is { } practice) Write("practice", "current", practice.BankId, 0, practice);
        if (state.Exam is { } exam) Write("exam", "current", exam.BankId, 0, exam);
        for (var i = 0; i < state.Wrongs.Count; i++) Write("wrong", state.Wrongs[i].BankId + "/" + state.Wrongs[i].Question.Id, state.Wrongs[i].BankId, i, state.Wrongs[i]);
        for (var i = 0; i < state.ExamHistory.Count; i++) Write("history", state.ExamHistory[i].Id.ToString(), state.ExamHistory[i].BankId, i, state.ExamHistory[i]);
    }
    private static void NormalizeLearning(LearningState state)
    {
        if (state.Exam is { Finished: false } exam) exam.Paused = true;
        if (state.Exam is { Finished: true } completed) LearningRules.ArchiveExam(state, completed);
    }
    private static void ValidateLearning(LearningState state)
    {
        static bool BadQuestion(QuestionDraft? q) => q is null || q.Id == Guid.Empty || string.IsNullOrWhiteSpace(q.Stem) || !Enum.IsDefined(q.Type) ||
            q.Options is null || q.CorrectOptions is null || q.BlankAnswers is null || q.BlankAnswers.Any(values => values is null);
        static bool BadItems(List<AnswerItem>? items, int index) => items is null || index < 0 || index > items.Count || items.Any(item => item is null || BadQuestion(item.Question) || item.Selected is null || item.Blanks is null);
        if (state.Wrongs is null || state.ExamHistory is null || state.Wrongs.Any(wrong => wrong is null || wrong.BankId == Guid.Empty || BadQuestion(wrong.Question)) ||
            state.ExamHistory.Any(exam => exam is null || exam.BankId == Guid.Empty || !exam.Finished || BadItems(exam.Items, exam.Index)) ||
            state.Practice is { } practice && (practice.BankId == Guid.Empty || BadItems(practice.Items, practice.Index)) ||
            state.Exam is { } current && (current.BankId == Guid.Empty || BadItems(current.Items, current.Index)))
            throw new InvalidDataException("学习记录结构损坏，请修复后重试；原数据不会清空重建。");
    }
    private static void RestoreLegacyLibraries(SqliteConnection connection, SqliteTransaction transaction, LearningState state)
    {
        var snapshots = new List<(Guid Bank, string Name, QuestionDraft Question)>();
        if (state.Practice is { } practice) snapshots.AddRange(practice.Items.Select(item => (practice.BankId, practice.BankName, item.Question)));
        if (state.Exam is { } exam) snapshots.AddRange(exam.Items.Select(item => (exam.BankId, exam.BankName, item.Question)));
        snapshots.AddRange(state.Wrongs.Select(wrong => (wrong.BankId, wrong.BankName, wrong.Question)));
        foreach (var history in state.ExamHistory) snapshots.AddRange(history.Items.Select(item => (history.BankId, history.BankName, item.Question)));
        foreach (var group in snapshots.GroupBy(snapshot => snapshot.Bank))
        {
            using var exists = connection.CreateCommand(); exists.Transaction = transaction;
            exists.CommandText = "SELECT COUNT(*) FROM libraries WHERE id=$id"; exists.Parameters.AddWithValue("$id", group.Key.ToString());
            if (Convert.ToInt32(exists.ExecuteScalar()) > 0) continue;
            var bank = new SampleLibrary(group.First().Name, "旧学习记录恢复", id: group.Key)
            { SourceText = "从旧学习记录恢复当时的题目快照；旧版本未保存完整题库或原始资料，无法还原未记录的题目。" };
            foreach (var entry in group.DistinctBy(snapshot => snapshot.Question.Id))
            {
                var question = JsonSerializer.Deserialize<QuestionDraft>(JsonSerializer.Serialize(entry.Question, JsonOptions), JsonOptions)!;
                question.IsConfirmed = question.ValidationError is null; question.BatchSelected = false; bank.Questions.Add(question);
            }
            WriteLibrary(connection, transaction, bank, null);
        }
    }
}
