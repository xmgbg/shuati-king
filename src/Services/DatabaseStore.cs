using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ShuatiKing.Models;

namespace ShuatiKing.Services;

public sealed partial class DatabaseStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IgnoreReadOnlyProperties = true };
    public string PathName { get; } = Path.GetFullPath(path);
    public string MaterialsDirectory => Path.Combine(Directory.GetParent(Path.GetDirectoryName(PathName)!)!.FullName, "materials");
    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = PathName, Pooling = false, ForeignKeys = true }.ToString());
        connection.Open(); return connection;
    }
    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
    public void Initialize()
    {
        using var connection = Open();
        using var version = connection.CreateCommand(); version.CommandText = "PRAGMA user_version";
        var number = Convert.ToInt32(version.ExecuteScalar());
        if (number > 1) throw new InvalidDataException("数据库版本高于当前软件支持范围，请使用新版软件；原数据已保留。");
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS libraries(id TEXT PRIMARY KEY, name TEXT NOT NULL, source TEXT NOT NULL, color TEXT NOT NULL, source_text TEXT NOT NULL, report TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS questions(id TEXT NOT NULL, library_id TEXT NOT NULL REFERENCES libraries(id) ON DELETE CASCADE, ordinal INTEGER NOT NULL, payload TEXT NOT NULL, PRIMARY KEY(library_id,id));
            CREATE TABLE IF NOT EXISTS materials(library_id TEXT PRIMARY KEY REFERENCES libraries(id) ON DELETE CASCADE, name TEXT NOT NULL, copy_path TEXT, notice TEXT NOT NULL, sections TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS metadata(key TEXT PRIMARY KEY,value TEXT NOT NULL);
            PRAGMA user_version=1;
            """);
        transaction.Commit();
    }
    public List<SampleLibrary> LoadLibraries()
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,source,color,source_text,report FROM libraries ORDER BY rowid";
        using var reader = command.ExecuteReader(); var result = new List<SampleLibrary>();
        while (reader.Read())
        {
            var bank = new SampleLibrary(reader.GetString(1), reader.GetString(2), color: reader.GetString(3), id: Guid.Parse(reader.GetString(0))) { SourceText = reader.GetString(4) };
            bank.ImportIssues.AddRange(JsonSerializer.Deserialize<List<ImportIssue>>(reader.GetString(5), JsonOptions) ?? throw new InvalidDataException("处理报告为空。"));
            using var questions = connection.CreateCommand(); questions.CommandText = "SELECT payload FROM questions WHERE library_id=$id ORDER BY ordinal"; questions.Parameters.AddWithValue("$id", bank.Id.ToString());
            using (var rows = questions.ExecuteReader()) while (rows.Read())
            {
                var q = JsonSerializer.Deserialize<QuestionDraft>(rows.GetString(0), JsonOptions) ?? throw new InvalidDataException("题目记录为空。");
                if (q.Options is null || q.CorrectOptions is null || q.BlankAnswers is null || q.BlankAnswers.Any(values => values is null)) throw new InvalidDataException("题目记录损坏。");
                q.BatchSelected = false; bank.Questions.Add(q);
            }
            using var materials = connection.CreateCommand(); materials.CommandText = "SELECT name,copy_path,notice,sections FROM materials WHERE library_id=$id"; materials.Parameters.AddWithValue("$id", bank.Id.ToString());
            using (var rows = materials.ExecuteReader()) if (rows.Read())
            {
                var sections = JsonSerializer.Deserialize<List<SectionData>>(rows.GetString(3), JsonOptions) ?? throw new InvalidDataException("资料段落为空。");
                bank.SourceDocument = new(rows.GetString(0), rows.IsDBNull(1) ? null : rows.GetString(1), sections.Select(section => new SourceSection(section.Location, section.Original) { Text = section.Edited }).ToList(), rows.GetString(2));
            }
            result.Add(bank);
        }
        return result;
    }
    public void SaveLibrary(SampleLibrary library)
    {
        var document = library.SourceDocument; string? copy = document?.FilePath; string? created = null;
        try
        {
            if (copy is not null)
            {
                var destination = Path.Combine(MaterialsDirectory, library.Id.ToString("N"), "original" + Path.GetExtension(copy).ToLowerInvariant());
                if (!File.Exists(destination) && !File.Exists(copy)) throw new FileNotFoundException("原始资料或内部资料副本不存在，请恢复文件后重试。");
                if (!string.Equals(Path.GetFullPath(copy), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    // 已有副本不覆盖；同一题库继续保存修正后的文字，原始文档保持导入时版本。
                    if (!File.Exists(destination))
                    {
                        var temporary = destination + ".tmp";
                        try { File.Copy(copy, temporary, true); File.Move(temporary, destination); created = destination; }
                        finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    }
                    copy = destination;
                }
            }
            using var connection = Open(); using var transaction = connection.BeginTransaction();
            WriteLibrary(connection, transaction, library, copy);
            transaction.Commit();
            if (document is not null) library.SourceDocument = new(document.Name, copy, document.Sections, document.Notice);
        }
        catch
        {
            if (created is not null && File.Exists(created)) File.Delete(created);
            throw;
        }
    }
    private static void WriteLibrary(SqliteConnection connection, SqliteTransaction transaction, SampleLibrary bank, string? copy)
    {
        Execute(connection, transaction, "INSERT INTO libraries VALUES($id,$name,$source,$color,$text,$report) ON CONFLICT(id) DO UPDATE SET name=excluded.name,source=excluded.source,color=excluded.color,source_text=excluded.source_text,report=excluded.report",
            ("$id", bank.Id.ToString()), ("$name", bank.Name), ("$source", bank.Source), ("$color", bank.Color), ("$text", bank.SourceText), ("$report", JsonSerializer.Serialize(bank.ImportIssues, JsonOptions)));
        Execute(connection, transaction, "DELETE FROM questions WHERE library_id=$id", ("$id", bank.Id.ToString()));
        for (var i = 0; i < bank.Questions.Count; i++) Execute(connection, transaction, "INSERT INTO questions VALUES($id,$bank,$ordinal,$payload)",
            ("$id", bank.Questions[i].Id.ToString()), ("$bank", bank.Id.ToString()), ("$ordinal", i), ("$payload", JsonSerializer.Serialize(bank.Questions[i], JsonOptions)));
        if (bank.SourceDocument is { } document)
            Execute(connection, transaction, "INSERT INTO materials VALUES($id,$name,$copy,$notice,$sections) ON CONFLICT(library_id) DO UPDATE SET name=excluded.name,copy_path=excluded.copy_path,notice=excluded.notice,sections=excluded.sections",
                ("$id", bank.Id.ToString()), ("$name", document.Name), ("$copy", copy), ("$notice", document.Notice),
                ("$sections", JsonSerializer.Serialize(document.Sections.Select(section => new SectionData(section.Location, section.OriginalText, section.Text)), JsonOptions)));
    }
    private sealed record SectionData(string Location, string Original, string Edited);
}
