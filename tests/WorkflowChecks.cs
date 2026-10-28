using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ShuatiKing.Views;

internal static class WorkflowChecks
{
    internal static void Run(Window window)
    {
        window.Width = 1440; window.Height = 900;
        var tab = (RadioButton)window.FindName("LibraryTab"); tab.IsChecked = false; tab.IsChecked = true;
        var host = (ContentControl)window.FindName("PageHost");
        window.UpdateLayout();
        var home = (LibraryView)host.Content;
        Program.Descendants<Button>(home).Single(b => b.Tag?.ToString() == "导入资料").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var import = (ImportView)host.Content;
        window.UpdateLayout();
        ((TextBox)import.FindName("LibraryNameInput")).Text = "闭环验收资料";
        ((TextBox)import.FindName("SourceInput")).Text = "一份独立资料。";
        Click(import, "NextButton");
        Program.Capture(window, "import-preview-light");
        Click(import, "NextButton");
        var task = import.GenerateAsync();
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => window.Dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
        Click(import, "NextButton");
        Program.Check(host.Content is ReviewView, "首页导入按钮走通到审核页面");
        var review = (ReviewView)host.Content;
        Click(review, "ReturnButton");
        Program.Check(ReferenceEquals(host.Content, import), "审核返回同一资料向导，题量和输入不丢失");
        Click(import, "BackButton");
        ((TextBox)import.FindName("PreviewInput")).Text = "修改后的保留原文";
        Click(import, "NextButton"); Click(import, "NextButton");
        review = (ReviewView)host.Content;
        Program.Check(import.CurrentLibrary!.SourceText == "修改后的保留原文", "返回修正文案不重新生成也能同步原文");
        var all = (CheckBox)review.FindName("SelectAllCheck");
        all.IsChecked = true; all.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent)); Click(review, "BatchConfirmButton");
        Click(review, "HomeButton");
        Program.Check(ReferenceEquals(host.Content, home) && home.SelectedLibrary == import.CurrentLibrary && home.SelectedLibrary.ConfirmedCount == 10 && home.SelectedLibrary.IsReady, "确认后首页选中同一题库并更新可练习状态");
        window.UpdateLayout(); Program.Capture(window, "library-after-review");
        Program.Descendants<Button>(home).Single(b => b.Tag?.ToString() == "题目管理").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Program.Check(host.Content is ReviewView reopened && reopened.CurrentQuestion?.IsConfirmed == true, "首页查看题目入口打开已确认内容");
        review = (ReviewView)host.Content;
        ((TextBox)review.FindName("StemInput")).Text = "离开页面前的草稿修改";
        ((Button)window.FindName("ProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
        Program.Check(import.CurrentLibrary!.Questions[0].Stem == "离开页面前的草稿修改" && import.CurrentLibrary.ConfirmedCount == 9, "通过顶部导航离开审核也保存草稿并更新入库状态");
    }
    private static void Click(FrameworkElement parent, string name) => ((Button)parent.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
