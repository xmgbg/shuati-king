namespace ShuatiKing.Models;

public sealed record SampleLibrary(string Name, string Source, int Single, int Multiple, int Judgment, int Blank, bool IsReady, string Color)
{
    public int Count => Single + Multiple + Judgment + Blank;
    public string Status => IsReady ? "可练习" : "待审核";
    public string Action => IsReady ? "查看题目  ›" : "审核题目  ›";
    public static SampleLibrary[] CreateExamples() =>
    [
        new("计算机网络基础", "计算机网络复习.pdf", 60, 28, 20, 20, true, "#0866FF"),
        new("大学英语词汇", "英语词汇.docx", 40, 16, 10, 20, true, "#7147EB"),
        new("教育心理学", "教育心理学.pdf", 30, 14, 10, 10, true, "#099F68"),
        new("计算机组成原理", "粘贴文字", 16, 8, 4, 4, false, "#EF970C")
    ];
}
