using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Reflection;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class ExamTypeChecks
{
    internal static void Run(Window window)
    {
        window.Width = 1440; window.Height = 900;
        var bank = new SampleLibrary("题型设置验收", "text", 2, 2, 2, 2, true);
        var state = new LearningState();
        var view = new ExamView(state, new[] { bank }, () => { }, () => { }, () => 18);
        ((ContentControl)window.FindName("PageHost")).Content = view; window.UpdateLayout();
        var panel = (StackPanel)view.FindName("TypeRows");
        var rows = panel.Children.Cast<Grid>().ToArray();
        foreach (var row in rows) Toggle(row).IsChecked = false;
        Click(view, "StartExamButton");
        Program.Check(state.Exam is null, "未选题型不能开考");
        Toggle(rows[0]).IsChecked = Toggle(rows[3]).IsChecked = true;
        Inputs(rows[0])[0].Text = "1"; Inputs(rows[0])[1].Text = "125";
        Inputs(rows[3])[0].Text = "1"; Inputs(rows[3])[1].Text = "50";
        Inputs(rows[1])[0].Text = "999"; Inputs(rows[1])[1].Text = "无效";
        Program.Check(!Inputs(rows[1])[0].IsEnabled && !Inputs(rows[1])[1].IsEnabled, "取消题型后禁用数量与分值");
        Program.Check(((TextBlock)view.FindName("PaperTotal")).Text.Contains("175"), "未选题型不影响满分统计或触发参数错误");
        var label = (StackPanel)rows[3].Children[0];
        Program.Check(label.Children[0] is Button && label.Children.OfType<Button>().Count() == 1, "拖动手柄位于最左侧且没有上下箭头");
        Program.Check(rows.All(r => Inputs(r).All(input => input.ActualWidth <= 112)), "题量及分值输入框保持紧凑宽度");
        view.MoveTypeRow(3, 0);
        Program.Check((int)((Grid)panel.Children[0]).Tag == 3 && Inputs(rows[3])[1].Text == "50", "重排题型保留原分值");
        Program.Check(string.IsNullOrEmpty(((TextBlock)view.FindName("Feedback")).Text), "排序后不显示改动说明");
        Program.Capture(window, "exam-type-settings");
        var previewType = typeof(ExamView).GetNestedType("TypeDragPreview", BindingFlags.NonPublic)!;
        var layer = AdornerLayer.GetAdornerLayer(panel)!;
        foreach (var theme in new[] { "浅色", "深色" })
        {
            ThemeManager.Apply(theme); window.UpdateLayout();
            var preview = (Adorner)Activator.CreateInstance(previewType, rows[0], panel, new Point(16, 18))!;
            layer.Add(preview); rows[0].Opacity = 0.2;
            try
            {
                previewType.GetMethod("Update")!.Invoke(preview, new object[] { new Point(30, 100), 128.0 });
                Program.Check(!preview.IsHitTestVisible && (int)((Grid)panel.Children[0]).Tag == 3, "悬浮预览不拦截鼠标且松手前不修改顺序");
                Program.Capture(window, theme == "浅色" ? "exam-type-drag-light" : "exam-type-drag-dark");
            }
            finally { layer.Remove(preview); rows[0].Opacity = 1; }
            Program.Check(layer.GetAdorners(panel) is null && rows[0].Opacity == 1, "拖动预览清理后恢复原行且无悬浮残留");
        }
        ThemeManager.Apply("深色"); window.Width = 1120; window.Height = 720; Program.Capture(window, "exam-type-settings-dark");
        ThemeManager.Apply("浅色");
        Click(view, "StartExamButton"); window.UpdateLayout();
        Program.Check(state.Exam is not null && state.Exam.Total == 175 && state.Exam.Items.Select(i => i.Question.Type).SequenceEqual(new[] { QuestionType.Blank, QuestionType.Single }), "实际开考只抽取已选题型且按照显示顺序出卷");
        view.PauseForNavigation();
    }
    private static CheckBox Toggle(Grid row) => ((StackPanel)row.Children[0]).Children.OfType<CheckBox>().Single();
    private static TextBox[] Inputs(Grid row) => row.Children.OfType<TextBox>().ToArray();
    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
