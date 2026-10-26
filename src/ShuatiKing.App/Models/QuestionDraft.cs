using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShuatiKing.Models;

public enum QuestionType { Single, Multiple, Judgment, Blank }

public sealed class QuestionDraft : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public QuestionType Type { get; set; }
    public string Stem { get; set; } = "";
    public List<string> Options { get; set; } = [];
    public List<int> CorrectOptions { get; set; } = [];
    public bool? JudgmentAnswer { get; set; }
    public List<List<string>> BlankAnswers { get; set; } = [];
    public string Explanation { get; set; } = "";
    public bool IsConfirmed { get; set; }
    public bool BatchSelected { get; set; }
    public string TypeName => Type switch { QuestionType.Single => "单选题", QuestionType.Multiple => "多选题", QuestionType.Judgment => "判断题", _ => "填空题" };
    public string Status => IsConfirmed ? "已入库" : ValidationError is null ? "待确认" : "待补全";
    public string Summary => $"{TypeName} · {Status}";
    public string ContentSignature => JsonSerializer.Serialize(new { Type, Stem, Options, CorrectOptions, JudgmentAnswer, BlankAnswers, Explanation });

    public string? ValidationError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Stem)) return "请补充题干。";
            if (Type is QuestionType.Single or QuestionType.Multiple)
            {
                if (Options.Count < 2 || Options.Any(string.IsNullOrWhiteSpace)) return "至少需要两个完整选项。";
                if (Options.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Options.Count) return "选项内容不能重复。";
                if (CorrectOptions.Count == 0) return "尚未选择正确答案。";
                if (CorrectOptions.Distinct().Count() != CorrectOptions.Count || CorrectOptions.Any(i => i < 0 || i >= Options.Count)) return "正确答案与现有选项不一致。";
                if (Type == QuestionType.Single && CorrectOptions.Count != 1) return "单选题只能设置一个正确答案。";
                if (Type == QuestionType.Multiple && CorrectOptions.Count < 2) return "多选题至少需要两个正确答案。";
            }
            if (Type == QuestionType.Judgment && JudgmentAnswer is null) return "尚未设置正确或错误。";
            if (Type == QuestionType.Blank)
            {
                var count = Regex.Matches(Stem, "_{2,}").Count;
                if (count == 0) return "请在题干中用 ____ 标记填空位置。";
                if (BlankAnswers.Count != count) return $"题干有 {count} 个空，需要逐空填写答案，每行对应一个空。";
                for (var i = 0; i < count; i++)
                    if (BlankAnswers[i].Count == 0 || BlankAnswers[i].Any(string.IsNullOrWhiteSpace)) return $"第 {i + 1} 个空尚未补全可接受答案。";
            }
            return null;
        }
    }

    public void NotifyChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
    public event PropertyChangedEventHandler? PropertyChanged;

    public static QuestionDraft Example(QuestionType type, string topic, int number = 1)
    {
        var question = new QuestionDraft { Type = type, Explanation = "这是用于检查界面的示例解析，可在审核时修改。" };
        switch (type)
        {
            case QuestionType.Single:
                question.Stem = $"【{topic} · 示例 {number}】OSI 参考模型中，负责路由选择的是哪一层？";
                question.Options = ["物理层", "网络层", "会话层", "表示层"]; question.CorrectOptions = [1]; break;
            case QuestionType.Multiple:
                question.Stem = $"【{topic} · 示例 {number}】以下哪些属于传输层协议？";
                question.Options = ["TCP", "IP", "UDP", "ARP"]; question.CorrectOptions = [0, 2]; break;
            case QuestionType.Judgment:
                question.Stem = $"【{topic} · 示例 {number}】TCP 提供面向连接的传输服务。";
                question.JudgmentAnswer = true; break;
            case QuestionType.Blank:
                question.Stem = $"【{topic} · 示例 {number}】TCP 的中文名称是 ____，UDP 的中文名称是 ____。";
                question.BlankAnswers = [["传输控制协议", "Transmission Control Protocol"], ["用户数据报协议", "User Datagram Protocol"]]; break;
        }
        return question;
    }
}
