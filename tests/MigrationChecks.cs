using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ShuatiKing;
using ShuatiKing.Models;
using ShuatiKing.Services;

internal static class MigrationChecks
{
    private static readonly JsonSerializerOptions Options = new() { IgnoreReadOnlyProperties = true };
    private static LearningState State()
    {
        var bank = new SampleLibrary("旧记录题库", "旧文档.pdf", 1, 1, 1, 1, true);
        var history = LearningRules.CreateExam(bank, [1, 1, 1, 1], [2, 3, 4, 5], 1);
        history.Items[0].Selected = [1]; history.Items[0].Submitted = history.Items[0].Correct = true;
        history.Finished = true; history.RemainingSeconds = 17;
        return new LearningState
        {
            Practice = new() { BankId = bank.Id, BankName = bank.Name, Index = 1, Items = bank.Questions.Select(q => new AnswerItem { Question = LearningRules.Snapshot(q), Selected = [0] }).ToList() },
            Exam = LearningRules.CreateExam(bank, [1, 0, 0, 0], [2, 1, 1, 1], 3),
            Wrongs = [new() { BankId = bank.Id, BankName = bank.Name, Question = LearningRules.Snapshot(bank.Questions[1]) }],
            ExamHistory = [history]
        };
    }
    internal static void Models()
    {
        var path = Program.TestPath("legacy-settings.learning.json"); var state = State();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Options); File.WriteAllBytes(path, bytes);
        var settings = new SettingsStore(Program.TestPath("legacy-settings.dat"));
        var config = new AiConfiguration { Endpoint = "https://example.test", Model = "test", ApiKey = "test-only-secret" };
        settings.Save(new() { Nickname = "迁移测试", Configurations = [config], SelectedConfigurationId = config.Id });
        var encrypted = File.ReadAllBytes(Program.TestPath("legacy-settings.dat"));
        var store = new LearningStore(path); var migrated = store.Load();
        Program.Check(migrated.Practice!.Index == 1 && migrated.Practice.Items[0].Selected.SequenceEqual(new[] { 0 }) && migrated.Wrongs.Count == 1, "旧练习进度、作答与错题完整迁入 SQLite");
        Program.Check(migrated.Exam!.Paused && migrated.Exam.RemainingSeconds == 180 && migrated.ExamHistory[0].Score == 2 && migrated.ExamHistory[0].Total == 14, "迁移保留考试剩余时间和历史原分数，未交卷保持暂停");
        Program.Check(File.ReadAllBytes(path).SequenceEqual(bytes) && File.ReadAllBytes(path + ".migration-backup").SequenceEqual(bytes), "迁移成功保留旧 JSON 并创建逐字节一致备份");
        Program.Check(File.ReadAllBytes(Program.TestPath("legacy-settings.dat")).SequenceEqual(encrypted) && settings.Load().Configurations[0].ApiKey == config.ApiKey, "迁移不改写个人与 AI 加密配置");
        var database = new DatabaseStore(settings.DatabasePath); var banks = database.LoadLibraries();
        Program.Check(banks.Single().Id == state.Practice!.BankId && banks[0].Questions.Count == 4 && banks[0].Source == "旧学习记录恢复", "从记录快照恢复关联题库，明确标识不冒充完整旧资料");
        migrated.Practice.Index = 2; migrated.Exam.RemainingSeconds = 77; migrated.Wrongs.Clear(); store.Save(migrated);
        File.WriteAllText(path, "旧文件后来损坏");
        var again = new LearningStore(path).Load();
        Program.Check(again.Practice!.Index == 2 && again.Exam!.RemainingSeconds == 77 && again.Wrongs.Count == 0 && again.ExamHistory.Count == 1, "迁移标记避免旧文件覆盖新记录或重复导入");
        var invalidSave = new LearningStore(path).Load(); invalidSave.ExamHistory.Add(invalidSave.ExamHistory[0]);
        try { new LearningStore(path).Save(invalidSave); Program.Check(false, "重复历史标识应使保存失败"); }
        catch (SqliteException) { Program.Check(new LearningStore(path).Load().ExamHistory.Count == 1, "学习记录保存失败回滚，不清空已保存记录"); }
        var bank = banks.Single(); bank.Questions[0].Stem = "题库后来修改"; database.SaveLibrary(bank);
        Program.Check(new LearningStore(path).Load().ExamHistory[0].Items[0].Question.Stem == state.ExamHistory[0].Items[0].Question.Stem, "迁移后修改题库仍不改写历史考试快照");
        foreach (var invalid in new[] { "{broken", "{}", "[]", "{\"Wrongs\":null,\"Exam\":null}" })
        {
            var bad = Program.TestPath(Guid.NewGuid() + ".json"); File.WriteAllText(bad, invalid);
            try { new LearningStore(bad).Load(); Program.Check(false, "损坏旧记录应阻断迁移"); }
            catch (Exception error) when (error is JsonException or InvalidDataException)
            { Program.Check(File.ReadAllText(bad) == invalid, "损坏或无效旧记录不清空重建，保留原文件"); }
            File.WriteAllBytes(bad, bytes);
            Program.Check(new LearningStore(bad).Load().Wrongs.Count == 1, "修复旧文件后可以重试迁移");
        }
        var failedPath = Program.TestPath("atomic.learning.json"); File.WriteAllBytes(failedPath, bytes);
        var failed = new DatabaseStore(SettingsStore.DatabaseForLearningPath(failedPath)); failed.Initialize(); failed.InitializeLearning();
        Sql(failed.PathName, "CREATE TRIGGER fail_marker BEFORE INSERT ON metadata WHEN NEW.key='learning_migrated' BEGIN SELECT RAISE(ABORT,'test failure'); END");
        try { failed.MigrateLearning(failedPath); Program.Check(false, "事务失败应抛出"); }
        catch (SqliteException)
        {
            Program.Check(failed.LoadLibraries().Count == 0 && failed.LoadLearning().Wrongs.Count == 0 && File.Exists(failedPath + ".migration-backup"), "迁移后段失败回滚全部记录和恢复题库，备份保留");
        }
        Sql(failed.PathName, "DROP TRIGGER fail_marker"); failed.MigrateLearning(failedPath);
        Program.Check(failed.LoadLearning().Wrongs.Count == 1 && failed.LoadLibraries().Count == 1, "数据库故障修复后可重新迁移且不重复");
        var backupPath = Program.TestPath("backup-failure.json"); File.WriteAllBytes(backupPath, bytes); Directory.CreateDirectory(backupPath + ".migration-backup");
        try { new LearningStore(backupPath).Load(); Program.Check(false, "备份失败不能继续迁移"); }
        catch (UnauthorizedAccessException) { Program.Check(File.ReadAllBytes(backupPath).SequenceEqual(bytes), "备份写入失败时原文件保留且不进入数据流程"); }
        Directory.Delete(backupPath + ".migration-backup");
        Program.Check(new LearningStore(backupPath).Load().Wrongs.Count == 1, "备份故障解除后迁移可恢复");
    }
    internal static void StartupFailure()
    {
        var settings = new SettingsStore(Program.TestPath("blocked-start.dat")); File.WriteAllText(settings.LearningPath, "{broken");
        try { _ = new MainWindow(settings); Program.Check(false, "损坏迁移不得进入主窗口"); }
        catch (JsonException) { Program.Check(File.ReadAllText(settings.LearningPath) == "{broken", "迁移失败在主窗口进入前被阻断，不降级空数据使用"); }
    }
    private static void Sql(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
}
