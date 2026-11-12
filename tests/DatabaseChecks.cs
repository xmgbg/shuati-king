using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using ShuatiKing;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;

internal static class DatabaseChecks
{
    internal static void Models()
    {
        var store = new DatabaseStore(Program.TestPath("database/data/shuati.db")); store.Initialize();
        Program.Check(store.LoadLibraries().Count == 0, "正式数据库首次为空，不注入示例题库");
        var original = Program.TestPath("original.pdf"); File.WriteAllText(original, "原始资料副本内容");
        var bank = new SampleLibrary("持久题库", "original.pdf", 1, 1, 1, 1);
        bank.Questions[0].IsConfirmed = true; bank.Questions[1].CorrectOptions.Clear();
        bank.Questions[0].SourceEvidence = "第1页\n原文依据"; bank.Questions[0].AiDoubt = "疑点";
        bank.SourceText = "已修正文字"; bank.SourceDocument = new("original.pdf", original, [new("第1页", "原始文字") { Text = "已修正文字" }]);
        bank.ImportIssues.Add(new("第2页", "不支持的题型", "原始内容"));
        store.SaveLibrary(bank); var restored = new DatabaseStore(store.PathName).LoadLibraries().Single();
        Program.Check(restored.Id == bank.Id && restored.Questions.Select(q => q.Id).SequenceEqual(bank.Questions.Select(q => q.Id)), "题库和题目标识、顺序重启后不变");
        Program.Check(restored.Questions[0].IsConfirmed && restored.Questions[1].ValidationError is not null && restored.ImportIssues.Count == 1, "审核状态、待补全答案与报告保存完整");
        Program.Check(restored.SourceDocument!.Sections[0].OriginalText == "原始文字" && restored.SourceDocument.Sections[0].Text == "已修正文字" && restored.Questions[0].SourceEvidence.Contains("依据"), "资料原文、修正文字与逐题依据保存完整");
        var copy = restored.SourceDocument.FilePath!;
        Program.Check(copy.StartsWith(store.MaterialsDirectory, StringComparison.OrdinalIgnoreCase) && File.ReadAllText(copy) == File.ReadAllText(original), "原始文档复制到独立内部目录");
        File.Delete(original); store.SaveLibrary(bank);
        Program.Check(File.Exists(copy), "用户原文件移动后继续使用内部副本保存");
        var second = new SampleLibrary("同名独立资料", "original.pdf"); second.Questions.Add(QuestionDraft.Example(QuestionType.Single, "新题")); store.SaveLibrary(second);
        Program.Check(store.LoadLibraries().Count == 2, "同名资料对应独立题库，不覆盖旧库");
        var before = store.LoadLibraries().Single(value => value.Id == bank.Id).Questions.Count;
        bank.Questions.Add(bank.Questions[0]);
        try { store.SaveLibrary(bank); Program.Check(false, "重复标识应触发事务失败"); }
        catch (SqliteException) { Program.Check(store.LoadLibraries().Single(value => value.Id == bank.Id).Questions.Count == before, "题库保存事务失败不留下半批数据"); }
    }
    internal static void Views()
    {
        var settings = new SettingsStore(Program.TestPath("persistent-ui.dat"));
        var first = new MainWindow(settings); first.Show(); first.UpdateLayout();
        var home = (LibraryView)((ContentControl)first.FindName("PageHost")).Content;
        Program.Check(home.Banks.Count == 0 && ((TextBlock)home.FindName("EmptyLibrary")).IsVisible, "正式首页展示空题库导入引导");
        Program.Capture(first, "database-empty-home"); first.Close();
        var database = new DatabaseStore(settings.DatabasePath); var bank = new SampleLibrary("重启审核", "粘贴文字", 1, 0, 0, 0);
        database.SaveLibrary(bank);
        var window = new MainWindow(settings); window.Show(); window.UpdateLayout();
        home = (LibraryView)((ContentControl)window.FindName("PageHost")).Content;
        var restored = home.Banks.Single();
        var review = new ReviewView(restored, () => { }, () => { }, persist: database.SaveLibrary);
        ((ContentControl)window.FindName("PageHost")).Content = review;
        ((TextBox)review.FindName("StemInput")).Text = "重启后保留的修改";
        ((Button)review.FindName("ConfirmQuestionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Program.Check(database.LoadLibraries().Single().Questions[0].IsConfirmed, "确认入库立即写入数据库");
        ((TextBox)review.FindName("StemInput")).Text = "关闭前的草稿修改";
        window.Close();
        var reopened = new MainWindow(settings); reopened.Show(); reopened.UpdateLayout();
        var after = ((LibraryView)((ContentControl)reopened.FindName("PageHost")).Content).Banks.Single();
        Program.Check(after.Questions[0].Stem == "关闭前的草稿修改" && !after.Questions[0].IsConfirmed, "正常关闭保存当前编辑，重启后保留草稿并要求重审");
        reopened.Close();
        using var fixture = new GenerationFixture();
        var reply = fixture.Server.ReplyFactory!; var calls = 0; var savedBeforeNextRequest = false;
        fixture.Server.ReplyFactory = request =>
        {
            if (++calls == 2)
            {
                savedBeforeNextRequest = database.LoadLibraries().Any(value => value.Name == "按批落盘" && value.Count == 4);
                fixture.Server.Status = 429;
            }
            return reply(request);
        };
        var import = new ImportView(_ => { }, database.SaveLibrary, () => { }, () => fixture.Configuration);
        ((TextBox)import.FindName("LibraryNameInput")).Text = "按批落盘";
        ((TextBox)import.FindName("SourceInput")).Text = "第一章\n用于验证批次落盘的资料。\n第二章\n用于验证请求失败的资料。";
        void Click(string name) => ((Button)import.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Click("NextButton"); Click("NextButton");
        var task = import.GenerateAsync(); var frame = new System.Windows.Threading.DispatcherFrame();
        task.ContinueWith(_ => import.Dispatcher.BeginInvoke(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
        Program.Check(savedBeforeNextRequest && database.LoadLibraries().Any(value => value.Name == "按批落盘" && value.Count == 4), "成功批次在下一次请求前落盘，后续请求失败不丢已有草稿");
        var failing = new ReviewView(after, () => { }, () => { }, persist: _ => throw new IOException("磁盘测试"));
        ((Button)failing.FindName("ConfirmQuestionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Program.Check(!after.Questions[0].IsConfirmed && ((TextBlock)failing.FindName("ReviewFeedback")).Text.Contains("保存失败"), "保存失败不显示入库成功");
    }
}
