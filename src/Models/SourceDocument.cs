namespace ShuatiKing.Models;

public sealed class SourceDocument(string name, string? filePath, List<SourceSection> sections, string notice = "")
{
    public string Name { get; } = name;
    public string? FilePath { get; } = filePath;
    public List<SourceSection> Sections { get; } = sections;
    public string Notice { get; } = notice;
    public string Text => string.Join("\n", Sections.Select(section => section.Text));
    public bool HasText => Sections.Any(section => !string.IsNullOrWhiteSpace(section.Text));
}

// Location 和 OriginalText 始终对应提取时的原始位置；用户修正只改变 Text。
public sealed class SourceSection(string location, string originalText)
{
    public string Location { get; } = location;
    public string OriginalText { get; } = originalText;
    public string Text { get; set; } = originalText;
    public override string ToString() => Location;
}
