using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ShuatiKing.Models;
using ShuatiKing.Views;

internal static class ImportChecks
{
    internal static void Run(Window window)
    {
        var host = (ContentControl)window.FindName("PageHost");
        SampleLibrary? saved = null;
        var import = new ImportView(_ => { }, library => saved = library, () => { });
        host.Content = import; window.UpdateLayout();
        Click(import, "NextButton");
        Program.Check(import.CurrentStep == 0, "缺题库名称时不能继续");
        ((TextBox)import.FindName("LibraryNameInput")).Text = "网络资料测试";
        ((TextBox)import.FindName("SourceInput")).Text = "测试原文";
        Click(import, "NextButton");
        ((TextBox)import.FindName("PreviewInput")).Text = "已修正的原文";
        Click(import, "BackButton");
        Program.Check(((TextBox)import.FindName("SourceInput")).Text == "已修正的原文", "预览修改返回后保留");
        Click(import, "NextButton"); Click(import, "NextButton");
        ((ComboBox)import.FindName("GenerationScenario")).SelectedIndex = 1;
        var operation = import.GenerateAsync();
        Program.Check(!((Button)import.FindName("GenerateButton")).IsEnabled, "生成中避免重复操作");
        Complete(operation, window.Dispatcher);
        Program.Check(saved?.Count == 5 && saved.ConfirmedCount == 0, "部分返回保留五道未入库草稿");
        ((ComboBox)import.FindName("GenerationScenario")).SelectedIndex = 2;
        Complete(import.GenerateAsync(), window.Dispatcher);
        Program.Check(saved!.Count == 5 && ((TextBlock)import.FindName("GenerationFeedback")).Text.Contains("429"), "失败提示状态码且保留草稿");
        ((ComboBox)import.FindName("GenerationScenario")).SelectedIndex = 0;
        Complete(import.GenerateAsync(), window.Dispatcher);
        Program.Check(saved.Count == 10 && saved.Single == 4 && saved.Multiple == 2 && saved.Judgment == 2 && saved.Blank == 2, "补充只补足目标题型数量");
        Program.Capture(window, "import-generated-light");
        Click(import, "BackButton");
        Program.Check(import.CurrentLibrary!.Count == 10, "返回修正仍保留全部草稿");
        var existing = new ImportView(_ => { }, _ => { }, () => { });
        host.Content = existing; window.UpdateLayout();
        ((RadioButton)existing.FindName("ExistingMode")).IsChecked = true;
        ((TextBox)existing.FindName("LibraryNameInput")).Text = "现成题目";
        ((TextBox)existing.FindName("SourceInput")).Text = "无法识别的原文也要保留";
        Program.Capture(window, "import-source-light");
        Click(existing, "NextButton"); Click(existing, "NextButton");
        Complete(existing.GenerateAsync(), window.Dispatcher);
        Program.Check(existing.CurrentLibrary!.Count == 4 && existing.CurrentLibrary.IncompleteCount == 1, "现成题目路径包含四题型及缺答案草稿");
        Program.Check(existing.CurrentLibrary.SourceText.Contains("无法识别"), "无法识别的原文不丢失");
        Complete(existing.GenerateAsync(), window.Dispatcher);
        Program.Check(existing.CurrentLibrary.Count == 4, "重复识别不重复添加示例题目");
    }

    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Complete(Task task, Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
    }
}
