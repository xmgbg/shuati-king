using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ShuatiKing.Models;
using ShuatiKing.Services;

namespace ShuatiKing.Views;

public partial class ImportView : UserControl
{
    private readonly Action<SampleLibrary> review;
    private readonly Action<SampleLibrary> draftsChanged;
    private readonly Action exit;
    private int step;
    private bool initializing = true;
    private bool generating;
    private readonly Func<AiConfiguration?> configuration;
    private readonly QuestionImportService questionImport;
    private QuestionImportSession? importSession;
    private CancellationTokenSource? importCancellation;
    private readonly HashSet<Guid> handledCandidates = [];
    private string? fileName;
    private bool reading;
    private bool selectingLocation;
    private SourceDocument? fileDocument;
    private SourceDocument? pasteDocument;
    public SourceDocument? Document { get; private set; }
    public SampleLibrary? CurrentLibrary { get; private set; }
    public int CurrentStep => step;

    public ImportView(Action<SampleLibrary> review, Action<SampleLibrary> draftsChanged, Action exit, Func<AiConfiguration?>? configuration = null, QuestionImportService? questionImport = null)
    {
        this.configuration = configuration ?? (() => null);
        this.questionImport = questionImport ?? new();
        this.review = review; this.draftsChanged = draftsChanged; this.exit = exit;
        InitializeComponent();
        foreach (var type in Enum.GetValues<QuestionType>())
        {
            var choice = new CheckBox
            {
                Name = type + "Type", Content = QuestionDraft.NameOf(type), Tag = type,
                IsChecked = type is QuestionType.Single or QuestionType.Multiple or QuestionType.Judgment or QuestionType.Blank,
                Style = (Style)FindResource("GenerationTypeChoice")
            };
            RegisterName(choice.Name, choice);
            choice.Checked += GenerationSelectionChanged;
            choice.Unchecked += GenerationSelectionChanged;
            GenerationTypes.Children.Add(choice);
        }
        initializing = false; UpdatePage();
        Unloaded += (_, _) => importCancellation?.Cancel();
    }

    private void ContentKindChanged(object sender, RoutedEventArgs e) { if (!initializing) UpdatePage(); }
    private void SourceKindChanged(object sender, RoutedEventArgs e)
    {
        if (initializing) return;
        if (!SourceInput.IsReadOnly) CapturePaste();
        var isFile = FileMode.IsChecked == true;
        Document = isFile ? fileDocument : pasteDocument;
        SourceInput.IsReadOnly = isFile;
        SourceInput.Text = Document?.Text ?? "";
        FileControls.Visibility = isFile ? Visibility.Visible : Visibility.Collapsed;
        InputLabel.Text = isFile ? "提取文字" : "粘贴资料文字";
        ReadStatus.Text = "";
        FormFeedback.Text = "";
    }

    private void CapturePaste()
    {
        if (pasteDocument?.Text != SourceInput.Text) pasteDocument = DocumentReader.FromText(SourceInput.Text);
    }

    private async void ChooseFile(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "选择资料", Filter = "文字型 PDF 或 Word|*.pdf;*.docx" };
        if (picker.ShowDialog() == true) await LoadFileAsync(picker.FileName);
    }

    public async Task LoadFileAsync(string path)
    {
        if (reading || generating) return;
        FileMode.IsChecked = true;
        reading = true;
        SourcePage.IsEnabled = BackButton.IsEnabled = NextButton.IsEnabled = false;
        ReadProgress.Visibility = Visibility.Visible;
        ReadStatus.Text = "正在读取资料…"; FormFeedback.Text = "";
        try
        {
            var document = await Task.Run(() => DocumentReader.Read(path));
            fileDocument = Document = document;
            fileName = document.Name; SelectedFile.Text = fileName;
            LibraryNameInput.Text = Path.GetFileNameWithoutExtension(fileName);
            SourceInput.Text = document.Text;
            CurrentLibrary = null; importSession = null; handledCandidates.Clear();
            ResultPanel.Visibility = Visibility.Collapsed;
            ReadStatus.Text = $"已读取 {document.Sections.Count} 个来源位置 · {document.Text.Length} 字符";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException
            or System.Xml.XmlException or UglyToad.PdfPig.Core.PdfDocumentFormatException
            or UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException)
        {
            ReadStatus.Text = "读取失败";
            FormFeedback.Text = error is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException
                ? error.Message : "无法读取此文件，请检查文件是否损坏、加密，或另存后重试。";
        }
        finally
        {
            reading = false; SourcePage.IsEnabled = BackButton.IsEnabled = true;
            ReadProgress.Visibility = Visibility.Collapsed; UpdatePage();
        }
    }

    private void ShowDocument()
    {
        selectingLocation = true;
        SourceLocations.ItemsSource = Document!.Sections;
        selectingLocation = false;
        SourceLocations.SelectedIndex = 0;
        ShowLocation();
        DocumentSummary.ToolTip = Document.FilePath;
        DocumentSummary.Text = $"{Document.Name} · {Document.Sections.Count} 个来源位置";
        PreviewNote.Text = Document.Notice;
    }

    private void LocationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initializing && !selectingLocation) ShowLocation();
    }

    private void ShowLocation()
    {
        selectingLocation = true;
        var section = SourceLocations.SelectedItem as SourceSection;
        PreviewInput.Text = section?.Text ?? "";
        OriginalInput.Text = section?.OriginalText ?? "";
        LocationTitle.Text = section?.Location ?? "";
        selectingLocation = false;
    }

    private void PreviewChanged(object sender, TextChangedEventArgs e)
    {
        if (initializing || selectingLocation) return;
        if (SourceLocations.SelectedItem is SourceSection section && section.Text != PreviewInput.Text)
        {
            section.Text = PreviewInput.Text;
            importSession = null;
        }
    }

    private void Next(object sender, RoutedEventArgs e)
    {
        if (generating || reading) return;
        FormFeedback.Text = "";
        if (step == 0)
        {
            if (string.IsNullOrWhiteSpace(LibraryNameInput.Text)) { FormFeedback.Text = "请填写题库名称。"; LibraryNameInput.Focus(); return; }
            if (FileMode.IsChecked == true && fileName is null) { FormFeedback.Text = "请先选择文件，或切换为粘贴文字。"; return; }
            if (FileMode.IsChecked != true && string.IsNullOrWhiteSpace(SourceInput.Text)) { FormFeedback.Text = "请粘贴文字，或选择可读取的文件。"; SourceInput.Focus(); return; }
            if (FileMode.IsChecked != true) { CapturePaste(); Document = pasteDocument; }
            else Document = fileDocument;
            ShowDocument(); step = 1;
        }
        else if (step == 1)
        {
            if (Document?.HasText != true) { FormFeedback.Text = "预览内容不能为空，请补充文字。"; return; }
            SourceInput.Text = Document!.Text;
            CurrentLibrary?.UpdateSource(LibraryNameInput.Text.Trim(), FileMode.IsChecked == true ? fileName! : "粘贴文字", Document!.Text);
            if (CurrentLibrary is not null) CurrentLibrary.SourceDocument = Document;
            step = 2;
        }
        else if (CurrentLibrary?.Count > 0) { review(CurrentLibrary); return; }
        UpdatePage();
    }

    private void Back(object sender, RoutedEventArgs e)
    {
        if (generating || reading) return;
        if (step == 0) { exit(); return; }
        if (step == 1) SourceInput.Text = Document!.Text;
        step--; FormFeedback.Text = ""; UpdatePage();
    }

    private void UpdatePage()
    {
        if (initializing) return;
        var existing = ExistingMode.IsChecked == true;
        SourcePage.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        PreviewPage.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        GeneratePage.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (label, index) in new[] { (StepSource, 0), (StepPreview, 1), (StepGenerate, 2) })
        { label.SetResourceReference(TextBlock.ForegroundProperty, index == step ? "AccentBrush" : "MutedBrush"); label.FontWeight = index == step ? FontWeights.SemiBold : FontWeights.Normal; }
        ImportSidebar.Visibility = step == 2 ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumnSpan(ImportPanel, step == 2 ? 3 : 1);
        ImportPanel.VerticalAlignment = step == 2 ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        ImportPanel.HorizontalAlignment = step == 2 ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        PathHint.Text = existing ? "保留原文，先预览检查，再进入现成题目整理与审核。" : "先检查资料文字，再选择题型生成题目。";
        RouteSummary.Text = existing ? "现成题目 → 预览文字 → AI 识别 → 修正与审核" : "知识资料 → 预览文字 → 生成草稿 → 审核入库";

        KnowledgeSettings.Visibility = existing ? Visibility.Collapsed : Visibility.Visible;
        GenerateTitle.Text = existing ? "整理现成题目" : "选择题型";
        GenerateDescription.Text = existing ? "识别资料中的题目和原答案。" : "勾选要生成的题型，题量取决于资料内容。";
        var selectedTypes = SelectedTypes();
        var current = importSession is not null && importSession.Plan.ExistingQuestions == existing &&
            (existing || importSession.Plan.SelectedTypes.SequenceEqual(selectedTypes));
        GenerationActions.Visibility = current && importSession!.Complete ? Visibility.Collapsed : Visibility.Visible;
        GenerateButton.IsEnabled = !generating && (existing || selectedTypes.Length > 0) && !(current && importSession!.Complete);
        GenerateButton.Content = current && importSession!.Complete ? (existing ? "识别已完成" : "生成已完成")
            : current && importSession!.CompletedBatches > 0 ? (existing ? "继续识别" : "继续生成")
            : existing ? "识别题目" : "生成草稿";
        CancelImportButton.Content = existing ? "暂停识别" : "暂停生成";
        BackButton.Content = step == 0 ? "返回题库" : "‹  上一步";
        NextButton.Content = step == 0 ? "预览内容  ›" : step == 1 ? existing ? "确认内容，整理题目  ›" : "确认内容，设置出题  ›" : "查看并审核草稿  ›";
        NextButton.Visibility = step == 2 && CurrentLibrary?.Count is not > 0 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.IsEnabled = !generating && !reading && (step != 2 || CurrentLibrary?.Count > 0);
        PreservedDraftHint.Text = CurrentLibrary?.Count > 0 ? $"已保留 {CurrentLibrary.Count} 道草稿；返回修改不会清空题目。" : "";
    }

    private async void GenerateClick(object sender, RoutedEventArgs e) => await GenerateAsync();

    private QuestionType[] SelectedTypes() => GenerationTypes.Children.OfType<CheckBox>()
        .Where(choice => choice.IsChecked == true).Select(choice => (QuestionType)choice.Tag).ToArray();

    private void GenerationSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!initializing && !generating) UpdatePage();
    }

    private void CancelImport(object sender, RoutedEventArgs e) => importCancellation?.Cancel();

    public async Task GenerateAsync()
    {
        if (generating || step != 2) return;
        var existing = ExistingMode.IsChecked == true;
        var types = existing ? [] : SelectedTypes();
        if (!existing && types.Length == 0) return;
        AiConfiguration? config;
        try { config = configuration()?.Copy(); }
        catch (InvalidOperationException error) { FormFeedback.Text = error.Message; return; }
        if (config is null) { FormFeedback.Text = "请先在我的中保存并选用 AI 配置。"; return; }
        var plan = MaterialPlanner.Create(Document!, existing, types, config);
        if (importSession is null || importSession.Plan.SourceVersion != plan.SourceVersion || importSession.Plan.ConfigurationId != config.Id ||
            importSession.Plan.Model != config.Model.Trim() || importSession.Plan.ExistingQuestions != existing || !importSession.Plan.SelectedTypes.SequenceEqual(types))
        { importSession = new(plan); handledCandidates.Clear(); }
        if (importSession.Complete)
        {
            if (plan.Batches.IsEmpty)
            {
                ResultPanel.Visibility = Visibility.Visible;
                GenerationFeedback.Text = "没有可用于出题的正文。";
                PublishImportedQuestions();
                UpdatePage();
            }
            return;
        }
        generating = true; FormFeedback.Text = "";
        SourcePage.IsEnabled = KnowledgeSettings.IsEnabled = GenerateButton.IsEnabled = BackButton.IsEnabled = NextButton.IsEnabled = false;
        ResultPanel.Visibility = GenerationProgress.Visibility = CancelImportButton.Visibility = Visibility.Visible;
        using var cancellation = new CancellationTokenSource(); importCancellation = cancellation;
        void Progress()
        {
            GenerationFeedback.Text = $"已处理 {importSession.CompletedBatches} / {importSession.Plan.Batches.Length} 批…";
            if (importSession.CompletedBatches > 0) PublishImportedQuestions();
        }
        Progress();
        try
        {
            await questionImport.RunAsync(importSession, config, Progress, cancellation.Token);
            GenerationFeedback.Text = importSession.Questions.Count > 0
                ? (existing ? "识别完成，请审核草稿。" : "生成完成，请审核草稿。")
                : (existing ? "未识别到可导入的题目。" : "所选题型未生成可审核的题目。");
        }
        catch (OperationCanceledException) { GenerationFeedback.Text = "任务已暂停，已完成批次保留，可手动继续。当前请求可能产生费用。"; }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException) { GenerationFeedback.Text = error.Message; }
        finally
        {
            importCancellation = null; generating = false;
            GenerationProgress.Visibility = CancelImportButton.Visibility = Visibility.Collapsed;
            SourcePage.IsEnabled = KnowledgeSettings.IsEnabled = GenerateButton.IsEnabled = BackButton.IsEnabled = true;
            PublishImportedQuestions(); UpdatePage();
        }
    }

    private void PublishImportedQuestions()
    {
        var session = importSession!;
        if (session.Questions.Count > 0)
        {
            CurrentLibrary ??= new SampleLibrary(LibraryNameInput.Text.Trim(), FileMode.IsChecked == true ? fileName! : "粘贴文字");
            CurrentLibrary.UpdateSource(LibraryNameInput.Text.Trim(), FileMode.IsChecked == true ? fileName! : "粘贴文字", Document!.Text);
            CurrentLibrary.SourceDocument = Document;
            foreach (var candidate in session.Questions)
            {
                if (!handledCandidates.Add(candidate.Id)) continue;
                if (CurrentLibrary.Questions.Any(question => QuestionRules.Same(question, candidate)))
                    session.Issues.Add(new(candidate.SourceEvidence.Split('\n')[0], "跳过完全重复题", candidate.Stem));
                else CurrentLibrary.Questions.Add(candidate);
            }
            session.Issues.AddRange(QuestionRules.RemoveDuplicateDrafts(CurrentLibrary, session.Questions.Select(question => question.Id)));
            foreach (var issue in session.Issues)
                if (!CurrentLibrary.ImportIssues.Contains(issue)) CurrentLibrary.ImportIssues.Add(issue);
            CurrentLibrary.NotifyChanged();
            try { draftsChanged(CurrentLibrary); }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
            { GenerationFeedback.Text += "\n本机保存失败，草稿仍在内存中，请检查数据目录后重试保存或进入审核。"; }
        }
        var distribution = CurrentLibrary?.Questions.GroupBy(q => q.Type).OrderBy(group => group.Key)
            .Select(group => $"{QuestionDraft.NameOf(group.Key)} {group.Count()}").ToArray() ?? [];
        ActualCounts.Text = $"草稿 {CurrentLibrary?.Count ?? 0} 道 · 待补全 {CurrentLibrary?.IncompleteCount ?? 0} 道\n" +
            (distribution.Length == 0 ? "" : string.Join(" · ", distribution) + "\n") +
            $"输入 {session.InputTokens} / 输出 {session.OutputTokens} Token" +
            (session.UsageUnknown ? "（另有未确认用量）" : "");
        var report = CurrentLibrary?.ImportIssues ?? session.Issues;
        ImportReport.Text = string.Join("\n\n", report.Select(issue => issue.Location + " · " + issue.Reason + "\n" + issue.OriginalText));
        ImportReportPanel.Visibility = report.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

}
