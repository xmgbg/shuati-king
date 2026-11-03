using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using ShuatiKing.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ShuatiKing.Services;

public static class DocumentReader
{
    public static SourceDocument FromText(string text) => new("粘贴文字", null,
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select((paragraph, index) => new SourceSection($"第 {index + 1} 段", paragraph)).ToList());

    public static SourceDocument Read(string path)
    {
        var sections = new List<SourceSection>();
        var notices = new List<string>();
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".pdf":
                using (var pdf = PdfDocument.Open(path))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        var text = ContentOrderTextExtractor.GetText(page, true);
                        sections.Add(new($"第 {page.Number} 页", text));
                        if (string.IsNullOrWhiteSpace(text)) notices.Add($"第 {page.Number} 页未提取到文字");
                    }
                }
                if (notices.Count > 0) notices.Add("图片及扫描内容未进行 OCR，请核对这些页面。");
                break;
            case ".docx":
                using (var archive = ZipFile.OpenRead(path))
                {
                    var entry = archive.GetEntry("word/document.xml")
                        ?? throw new InvalidDataException("文件不包含 Word 正文，请选择有效的 .docx 文件。");
                    using var stream = entry.Open();
                    var xml = XDocument.Load(stream);
                    XNamespace w = xml.Root?.Name.Namespace ?? XNamespace.None;
                    var body = xml.Root?.Element(w + "body")
                        ?? throw new InvalidDataException("无法找到 Word 正文。");
                    var index = 0;
                    foreach (var paragraph in body.Descendants(w + "p"))
                    {
                        var text = string.Concat(paragraph.Descendants()
                            .Where(element => element.Ancestors(w + "p").FirstOrDefault() == paragraph &&
                                !element.Ancestors(w + "del").Any())
                            .Select(element => element.Name.LocalName switch
                            {
                                "t" when element.Name.Namespace == w => element.Value,
                                "tab" when element.Name.Namespace == w => "\t",
                                "br" or "cr" when element.Name.Namespace == w => "\n",
                                _ => ""
                            }));
                        sections.Add(new($"正文第 {++index} 段" + (paragraph.Ancestors(w + "tc").Any() ? " · 表格" : ""), text));
                    }
                    notices.Add("按正文段落定位，包含表格文字；Word 页码随排版变化，不作为固定位置。");
                    if (body.Descendants().Any(element => element.Name.LocalName is "drawing" or "pict" or "oMath" or "numPr"))
                        notices.Add("文档含图片、公式或自动编号，请对照原文件核对；这些内容未还原为文字。");
                    if (archive.Entries.Any(item => item.FullName.StartsWith("word/header") || item.FullName.StartsWith("word/footer") || item.FullName == "word/footnotes.xml" || item.FullName == "word/endnotes.xml"))
                        notices.Add("页眉、页脚和脚注、尾注未纳入正文提取。");
                }
                break;
            default:
                throw new NotSupportedException("仅支持文字型 PDF 和 Word（.docx），旧版 .doc 请另存为 .docx。");
        }
        if (!sections.Any(section => !string.IsNullOrWhiteSpace(section.Text)))
            throw new InvalidDataException("未提取到可用文字。扫描件或纯图片资料暂不支持，请换用文字型文件，或粘贴文字。");
        return new(Path.GetFileName(path), Path.GetFullPath(path), sections, string.Join("；", notices));
    }
}
