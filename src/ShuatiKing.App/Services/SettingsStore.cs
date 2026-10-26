using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ShuatiKing.Models;

namespace ShuatiKing.Services;

public sealed class SettingsStore
{
    private readonly string path;
    public SettingsStore(string? path = null) => this.path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShuatiKing", "settings.dat");

    public string LearningPath => Path.ChangeExtension(path, "learning.json");

    public LocalSettings Load()
    {
        if (!File.Exists(path)) return new();
        var json = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<LocalSettings>(json) ?? throw new InvalidDataException("设置文件内容为空。");
    }

    public void Save(LocalSettings settings)
    {
        var encrypted = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(settings), null, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path + ".tmp", encrypted);
        File.Move(path + ".tmp", path, true);
    }
}
