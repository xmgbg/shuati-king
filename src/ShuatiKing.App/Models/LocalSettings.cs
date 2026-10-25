namespace ShuatiKing.Models;

public sealed class LocalSettings
{
    public string Nickname { get; set; } = "学习者";
    public string? Avatar { get; set; }
    public string Theme { get; set; } = "浅色";
    public int QuestionFontSize { get; set; } = 18;
    public List<AiConfiguration> Configurations { get; set; } = [new()];
    public Guid SelectedConfigurationId { get; set; }
}

public sealed class AiConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "配置 1";
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public AiConfiguration Copy() => (AiConfiguration)MemberwiseClone();
}
