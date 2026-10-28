using System.Windows;
using System.Windows.Controls;
using ShuatiKing.Models;
using ShuatiKing.Services;

namespace ShuatiKing.Views;

public partial class ExamResultView : UserControl
{
    public ExamSession Record { get; }
    private readonly int questionFontSize;
    private int index;
    public ExamResultView(ExamSession record, int questionFontSize)
    {
        Record = record; this.questionFontSize = questionFontSize;
        InitializeComponent();
        BankName.Text = record.BankName;
        ExamDate.Text = (record.FinishedAt is { } finished ? finished.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "时间未记录") + (record.TimedOut ? " · 到时交卷" : " · 主动交卷");
        Score.Text = $"{record.Score:0.##} / {record.Total:0.##} 分";
        Summary.Text = $"共 {record.Items.Count} 题 · 答对 {record.Items.Count(i => i.Correct)} · 答错 {record.Items.Count(i => i.Answered && !i.Correct)} · 未答 {record.Items.Count(i => !i.Answered)}\n用时 {Duration(record.DurationSeconds - record.RemainingSeconds)}";
        foreach (var text in new[] { YourAnswer, CorrectAnswer, Explanation }) { text.FontSize = questionFontSize; text.LineHeight = questionFontSize * 1.6; }
        for (var i = 0; i < record.Items.Count; i++)
        {
            int position = i; var item = record.Items[i];
            var button = new Button { Content = $"{i + 1} {Status(item)}", Width = 52, MinHeight = 36, Padding = new Thickness(3, 6, 3, 6), FontSize = 12, Margin = new Thickness(0, 0, 6, 6) };
            button.SetResourceReference(ForegroundProperty, item.Correct ? "SuccessBrush" : item.Answered ? "ErrorBrush" : "MutedBrush");
            System.Windows.Automation.AutomationProperties.SetName(button, $"第 {i + 1} 题，{Status(item)}");
            button.Click += (_, _) => ShowQuestion(position); QuestionMap.Children.Add(button);
        }
        if (record.Items.Count > 0) ShowQuestion(0);
    }
    private void ShowQuestion(int position)
    {
        index = position; var item = Record.Items[index];
        QuestionPosition.Text = $"第 {index + 1} / {Record.Items.Count} 题 · 本题 {item.Points:0.##} 分";
        Outcome.Text = $"{Status(item)} · 得 { (item.Correct ? item.Points : 0):0.##} 分";
        Outcome.SetResourceReference(TextBlock.ForegroundProperty, item.Correct ? "SuccessBrush" : item.Answered ? "ErrorBrush" : "MutedBrush");
        QuestionHost.Content = new AnswerEditor(LearningRules.SnapshotAnswer(item), questionFontSize, true);
        YourAnswer.Text = "你的答案：" + AnswerText(item);
        CorrectAnswer.Text = "正确答案：" + LearningRules.CorrectText(item.Question);
        Explanation.Text = string.IsNullOrWhiteSpace(item.Question.Explanation) ? "暂无解析" : "解析：" + item.Question.Explanation;
        PreviousButton.IsEnabled = index > 0; NextButton.IsEnabled = index < Record.Items.Count - 1;
        for (var i = 0; i < QuestionMap.Children.Count; i++)
        {
            var button = (Button)QuestionMap.Children[i];
            button.SetResourceReference(BorderBrushProperty, i == index ? "AccentBrush" : "LineBrush");
            button.SetResourceReference(BackgroundProperty, i == index ? "TintBrush" : "SurfaceBrush");
        }
        ReviewScroll.ScrollToTop();
    }
    private static string Status(AnswerItem item) => item.Correct ? "答对" : item.Answered ? "答错" : "未答";
    private static string AnswerText(AnswerItem item)
    {
        if (!item.Answered) return "未作答";
        return item.Question.Type switch
        {
            QuestionType.Single or QuestionType.Multiple => string.Join("、", item.Selected.Order().Select(i => ((char)('A' + i)).ToString())),
            QuestionType.Judgment => item.Judgment == true ? "正确" : "错误",
            _ => string.Join("\n", item.Question.BlankAnswers.Select((_, i) => $"第 {i + 1} 空：" + (i < item.Blanks.Count && !string.IsNullOrWhiteSpace(item.Blanks[i]) ? item.Blanks[i] : "未填写")))
        };
    }
    private static string Duration(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, seconds)));
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }
    private void Previous(object sender, RoutedEventArgs e) => ShowQuestion(index - 1);
    private void Next(object sender, RoutedEventArgs e) => ShowQuestion(index + 1);
}