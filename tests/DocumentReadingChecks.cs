using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ShuatiKing.Models;
using ShuatiKing.Services;
using ShuatiKing.Views;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Core;

internal static class DocumentReadingChecks
{
    internal static void Run(Window window)
    {
        var wordPath = Program.TestPath("中文资料.docx");
        using (var zip = ZipFile.Open(wordPath, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            writer.Write("""
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>
                  <w:p><w:r><w:t xml:space="preserve">第一章 网络 </w:t></w:r><w:r><w:t>基础</w:t><w:tab/><w:t>TCP</w:t><w:br/><w:t>第二行</w:t></w:r></w:p>
                  <w:p><w:r><w:t>第二段：中文标点，英文 UDP。</w:t></w:r><w:del><w:r><w:delText>删除的旧内容</w:delText></w:r></w:del></w:p>
                  <w:tbl><w:tr><w:tc><w:p><w:r><w:t>表格单元格 A</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>表格单元格 B</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
                  <w:p/>
                </w:body></w:document>
                """);
        }
        var wordBytes = File.ReadAllBytes(wordPath);
        var doc = DocumentReader.Read(wordPath);
        Program.Check(doc.Sections.Count == 5 && doc.Sections[0].Text == "第一章 网络 基础\tTCP\n第二行", "Word 合并文字运行并保留空格、制表符与软换行");
        Program.Check(doc.Sections[1].Text == "第二段：中文标点，英文 UDP。" && doc.Sections[2].Location == "正文第 3 段 · 表格", "Word 中文、表格顺序与正文段落位置正确");
        Program.Check(doc.Sections[3].Text == "表格单元格 B" && doc.Sections[4].Text == "", "Word 表格和空段落不漏失");
        Program.Check(doc.FilePath == wordPath, "资料记录原文件路径");

        var pdfPath = Program.TestPath("pages.PDF");
        var pdf = new PdfDocumentBuilder();
        var font = pdf.AddStandard14Font(Standard14Font.Helvetica);
        pdf.AddPage(500, 700).AddText("First page: TCP", 14, new PdfPoint(30, 650), font);
        pdf.AddPage(500, 700).AddText("Second page: UDP", 14, new PdfPoint(30, 650), font);
        pdf.AddPage(500, 700);
        File.WriteAllBytes(pdfPath, pdf.Build());
        var pages = DocumentReader.Read(pdfPath);
        Program.Check(pages.Sections.Count == 3 && pages.Sections[1].Location == "第 2 页", "PDF 保留全部物理页号，包括空白页");
        Program.Check(pages.Sections[0].Text.Contains("First page: TCP") && pages.Sections[1].Text.Contains("Second page: UDP"), "PDF 多页文字真实提取且顺序正确");
        Program.Check(pages.Notice.Contains("第 3 页未提取到文字"), "PDF 无文字页面明确提示，不静默丢页");
        var blank = new PdfDocumentBuilder(); blank.AddPage(500, 700);
        var blankPath = Program.TestPath("blank.pdf"); File.WriteAllBytes(blankPath, blank.Build());
        Throws<InvalidDataException>(() => DocumentReader.Read(blankPath), "无文字 PDF 不进入可用预览");
        var broken = Program.TestPath("broken.docx"); File.WriteAllText(broken, "not a zip");
        Throws<InvalidDataException>(() => DocumentReader.Read(broken), "损坏 Word 报错");
        Throws<NotSupportedException>(() => DocumentReader.Read("old.doc"), "旧版 doc 明确不支持");
        var paste = DocumentReader.FromText("甲\r\n\r\n乙\r丙");
        Program.Check(paste.Text == "甲\n\n乙\n丙" && paste.Sections[2].Location == "第 3 段", "粘贴文字保留段落顺序和空行");

        var host = (ContentControl)window.FindName("PageHost");
        var view = new ImportView(_ => { }, _ => { }, () => { });
        host.Content = view; window.UpdateLayout();
        ((TextBox)view.FindName("SourceInput")).Text = "原有粘贴资料";
        var operation = view.LoadFileAsync(pdfPath);
        Program.Check(!((Button)view.FindName("NextButton")).IsEnabled, "读取中禁用下一步，避免使用半成品");
        Complete(operation, window.Dispatcher);
        Program.Check(((TextBox)view.FindName("SourceInput")).IsReadOnly && view.Document?.Sections.Count == 3, "文件读取后展示真实文字并进入文件来源模式");
        Click(view, "NextButton");
        var positions = (ListBox)view.FindName("SourceLocations");
        var editor = (TextBox)view.FindName("PreviewInput");
        editor.Text = "修正第一页";
        positions.SelectedIndex = 1; editor.Text = "修正第二页";
        positions.SelectedIndex = 0;
        Program.Check(editor.Text == "修正第一页" && ((TextBox)view.FindName("OriginalInput")).Text.Contains("First page"), "切换页码保留修改，原文对照仍为提取内容");
        Click(view, "BackButton"); Click(view, "NextButton");
        Program.Check(editor.Text == "修正第一页" && view.Document!.Sections[1].Text == "修正第二页", "返回来源再进预览不覆盖逐页修改");
        Click(view, "NextButton"); Click(view, "BackButton");
        Program.Check(editor.Text == "修正第一页", "进入后续设置再返回也保留修改");
        Click(view, "BackButton");
        ((RadioButton)view.FindName("PasteMode")).IsChecked = true;
        Program.Check(((TextBox)view.FindName("SourceInput")).Text == "原有粘贴资料", "文件与粘贴两种来源切换不会互相覆盖");
        ((RadioButton)view.FindName("FileMode")).IsChecked = true;
        var previous = view.Document;
        Complete(view.LoadFileAsync(broken), window.Dispatcher);
        Program.Check(ReferenceEquals(previous, view.Document) && ((TextBlock)view.FindName("FormFeedback")).Text.Length > 0 && ((Button)view.FindName("NextButton")).IsEnabled, "读取失败保留上一份资料并允许重试");
        Complete(view.LoadFileAsync(wordPath), window.Dispatcher);
        ((RadioButton)view.FindName("ExistingMode")).IsChecked = true;
        Click(view, "NextButton"); positions.SelectedIndex = 2;
        Program.Check(((TextBlock)view.FindName("LocationTitle")).Text.Contains("表格") && editor.Text == "表格单元格 A", "现成题目路径同样支持 Word 来源定位");
        editor.Text = "修正后的表格资料";
        window.Width = 1440; window.Height = 900;
        ((Expander)view.FindName("OriginalExpander")).IsExpanded = true;
        Program.Capture(window, "document-preview-word");
        var width = window.Width; var height = window.Height;
        window.Width = 1120; window.Height = 720;
        Program.Capture(window, "document-preview-compact");
        Program.Check(editor.ActualWidth > 150 && ((Button)view.FindName("NextButton")).IsVisible, "最小窗口仍可编辑和继续");
        ThemeManager.Apply("深色"); Program.Capture(window, "document-preview-dark"); ThemeManager.Apply("浅色");
        window.Width = width; window.Height = height;
        Program.Check(File.ReadAllBytes(wordPath).SequenceEqual(wordBytes), "读取和编辑不改写用户原始文件");
        foreach (var section in view.Document!.Sections) section.Text = "";
        Click(view, "NextButton");
        Program.Check(view.CurrentStep == 1 && ((TextBlock)view.FindName("FormFeedback")).Text.Contains("不能为空"), "清空全部提取文字后不能确认继续");
    }

    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); Program.Check(false, message); }
        catch (T) { Program.Check(true, message); }
    }
    private static void Click(FrameworkElement view, string name) => ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Complete(Task task, Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
    }
}
