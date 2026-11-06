using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ShuatiKing.Models;
using ShuatiKing.Views;
using ShuatiKing.Services;

internal static class ImportChecks
{
    internal static void Run(Window window)
    {
        ((RadioButton)window.FindName("LibraryTab")).IsChecked = true;
        var host = (ContentControl)window.FindName("PageHost");
        using var fixture = new GenerationFixture();
        SampleLibrary? saved = null;
        var import = new ImportView(_ => { }, library => saved = library, () => { }, () => fixture.Configuration);
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
        fixture.Partial = true;
        var operation = import.GenerateAsync();
        Program.Check(!((Button)import.FindName("GenerateButton")).IsEnabled, "生成中避免重复操作");
        Complete(operation, window.Dispatcher);
        Program.Check(saved?.Count == 5 && saved.ConfirmedCount == 0, "部分返回保留五道未入库草稿");
        fixture.Server.Status = 429;
        Complete(import.GenerateAsync(), window.Dispatcher);
        Program.Check(saved!.Count == 5 && ((TextBlock)import.FindName("GenerationFeedback")).Text.Contains("429"), "失败提示状态码且保留草稿");
        fixture.Server.Status = 200; fixture.Partial = false;
        Complete(import.GenerateAsync(), window.Dispatcher);
        Program.Check(saved.Count == 10 && saved.Single == 4 && saved.Multiple == 2 && saved.Judgment == 2 && saved.Blank == 2, "补充只补足目标题型数量");
        Program.Check(((TextBlock)import.FindName("ActualCounts")).Text.Contains("输入 1000 / 输出 600") && ((TextBlock)import.FindName("ActualCounts")).Text.Contains("仍缺 0"), "手动补充累计实际 Token，当前缺口单独显示");
        Program.Check(((Button)import.FindName("GenerateButton")).Content.ToString() == "生成已完成", "题量满足后不误示补充生成");
        var completedRequests = fixture.Server.RequestCount;
        Complete(import.GenerateAsync(), window.Dispatcher);
        Program.Check(fixture.Server.RequestCount == completedRequests, "已满足题量时再次点击不产生额外调用");
        Program.Capture(window, "import-generated-light");
        Click(import, "BackButton");
        Program.Check(import.CurrentLibrary!.Count == 10, "返回修正仍保留全部草稿");

    }

    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Complete(Task task, Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
    }
}
