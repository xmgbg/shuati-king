using System.IO;
using System.Windows;
using Microsoft.Win32;
using ShuatiKing.Models;

namespace ShuatiKing.Views;

public partial class PackageDialog : Window
{
    private readonly Action<SampleLibrary>? import;
    private SampleLibrary? preview;

    public PackageDialog(Action<SampleLibrary> import) : this()
    {
        this.import = import;
        Title = DialogHeading.Text = "导入题库";
        DialogDescription.Text = "导入后保存为独立题库，与本机已有题库分开。此处使用示例题库演示。";
        PackageFeedback.Text = "可选择文件查看入口，或载入示例题库体验后续操作。";
        PackageContent.Text = "尚未载入题库预览。";
    }

    public PackageDialog(SampleLibrary library) : this()
    {
        Title = DialogHeading.Text = "导出题库 · 内容预览";
        DialogDescription.Text = "仅包含已入库题目、答案、已有解析与资料名称。";
        ImportActions.Visibility = Visibility.Collapsed;
        PackageFeedback.Text = $"可导出 {library.ConfirmedCount} 道题。不包含原始文档、待审核草稿、密钥、答题记录或进度。";
        PackageContent.Text = Describe(library);
        PackageConfirmButton.Content = "关闭预览"; PackageConfirmButton.IsEnabled = true;
    }

    private PackageDialog() { InitializeComponent(); }

    private void ChoosePackage(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "选择题库包", Filter = "题库包|*.json;*.skbank|所有文件|*.*" };
        if (picker.ShowDialog() != true) return;
        preview = null; PackageConfirmButton.IsEnabled = false;
        PackageFeedback.Text = $"已选择 {Path.GetFileName(picker.FileName)}，文件尚未读取。请载入示例题库体验导入流程。";
        PackageContent.Text = "本轮仅提供题库包导入入口，不将所选文件视为已解析内容。";
    }

    private void LoadExample(object sender, RoutedEventArgs e)
    {
        preview = new SampleLibrary("网络复习（导入示例）", "网络复习.pdf", 2, 2, 2, 2, true);
        PackageContent.Text = Describe(preview);
        PackageFeedback.Text = "示例题库共 8 道已入库题目；确认后作为独立题库加入本次运行。";
        PackageConfirmButton.IsEnabled = true;
    }

    private void Confirm(object sender, RoutedEventArgs e)
    {
        if (import is not null)
        {
            if (preview is null) return;
            import(preview);
        }
        Close();
    }

    private static string Describe(SampleLibrary library)
    {
        var lines = new List<string> { $"资料名称：{library.Source}", $"已入库题目：{library.ConfirmedCount} 道", "" };
        var questions = library.Questions.Where(q => q.IsConfirmed && q.ValidationError is null).ToList();
        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i]; lines.Add($"{i + 1}. [{q.TypeName}] {q.Stem}");
            if (q.Type is QuestionType.Single or QuestionType.Multiple)
            {
                for (var option = 0; option < q.Options.Count; option++) lines.Add($"   {(char)('A' + option)}. {q.Options[option]}");
                lines.Add("答案：" + string.Join("、", q.CorrectOptions.Select(n => ((char)('A' + n)).ToString())));
            }
            else if (q.Type == QuestionType.Judgment) lines.Add("答案：" + (q.JudgmentAnswer == true ? "正确" : "错误"));
            else for (var blank = 0; blank < q.BlankAnswers.Count; blank++) lines.Add($"第 {blank + 1} 空：" + string.Join(" | ", q.BlankAnswers[blank]));
            if (!string.IsNullOrWhiteSpace(q.Explanation)) lines.Add("解析：" + q.Explanation);
            lines.Add("");
        }
        return string.Join("\n", lines);
    }
}
