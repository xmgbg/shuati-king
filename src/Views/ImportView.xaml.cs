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
    public MaterialPlan? Plan { get; private set; }
    public SourceDocument? Document { get; private set; }
    public SampleLibrary? CurrentLibrary { get; private set; }
    public int CurrentStep => step;

    public ImportView(Action<SampleLibrary> review, Action<SampleLibrary> draftsChanged, Action exit, Func<AiConfiguration?>? configuration = null, QuestionImportService? questionImport = null)
    {
        this.configuration = configuration ?? (() => null);
        this.questionImport = questionImport ?? new();
        this.review = review; this.draftsChanged = draftsChanged; this.exit = exit;
        InitializeComponent(); initializing = false; UpdatePage();
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
        if (SourceLocations.SelectedItem is SourceSection section) section.Text = PreviewInput.Text;
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
            Plan = MaterialPlanner.Create(Document!, ExistingMode.IsChecked == true, [0, 0, 0, 0]);
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
        PathHint.Text = existing ? "保留原文，先预览检查，再进入现成题目整理与审核。" : "先检查资料文字，再选择题型和数量生成题目。";
        RouteSummary.Text = existing ? "现成题目 → 预览文字 → AI 识别 → 修正与审核" : "知识资料 → 预览文字 → 生成草稿 → 审核入库";

        KnowledgeSettings.Visibility = existing ? Visibility.Collapsed : Visibility.Visible;
        GenerateTitle.Text = existing ? "整理现成题目" : "选择题型与数量";
        GenerateDescription.Text = existing ? "使用当前 AI 配置识别题目，按批次发送资料。原答案保留，缺答案题目待补全。" : "本轮不读取密钥、不调用 AI。正式出题需在“我的”中配置服务，生成后先审核再入库。";
        GenerateButton.Content = existing ? importSession is { Complete: true } ? "识别已完成" : importSession is { CompletedBatches: > 0 } ? "继续识别" : "识别题目" : CurrentLibrary?.Count > 0 ? "补充生成缺少题目（演示）" : "生成草稿（演示）";
        TaskPlanSummary.Text = Plan is null ? "" : $"资料已分为 {Plan.Batches.Length} 批";
        FlowNotice.Visibility = existing ? Visibility.Collapsed : Visibility.Visible;
        FlowNotice.Text = existing ? "识别完成后先审核，确认后可用于练习。" : "知识资料出题暂为示例结果。";
        BackButton.Content = step == 0 ? "返回题库" : "‹  上一步";
        NextButton.Content = step == 0 ? "预览内容  ›" : step == 1 ? existing ? "确认内容，整理题目  ›" : "确认内容，设置出题  ›" : "查看并审核草稿  ›";
        NextButton.IsEnabled = !generating && !reading && (step != 2 || CurrentLibrary?.Count > 0);
        PreservedDraftHint.Text = CurrentLibrary?.Count > 0 ? $"已保留 {CurrentLibrary.Count} 道草稿；返回修改不会清空题目。" : "";
    }

    private async void GenerateClick(object sender, RoutedEventArgs e) => await GenerateAsync();

    public async Task GenerateAsync()
    {
        if (generating || step != 2) return;
        var existing = ExistingMode.IsChecked == true;
        if (existing) { await RecognizeExistingAsync(); return; }
        int[] targets = new int[4];
        var inputs = new[] { SingleCount, MultipleCount, JudgmentCount, BlankCount };
        {
            for (var i = 0; i < 4; i++)
                if (!int.TryParse(inputs[i].Text, out targets[i]) || targets[i] < 0 || targets[i] > 100)
                { FormFeedback.Text = "请将每种题型的演示数量填写为 0–100 的整数。"; inputs[i].Focus(); return; }
            if (targets.Sum() == 0) { FormFeedback.Text = "请至少设置一道题。"; return; }
        }
        Plan = MaterialPlanner.Create(Document!, existing, targets);
        CurrentLibrary ??= new SampleLibrary(LibraryNameInput.Text.Trim(), FileMode.IsChecked == true ? fileName! : "粘贴文字");
        CurrentLibrary.UpdateSource(LibraryNameInput.Text.Trim(), FileMode.IsChecked == true ? fileName! : "粘贴文字", Document!.Text);
        CurrentLibrary.SourceDocument = Document;
        var scenario = ((ComboBoxItem)GenerationScenario.SelectedItem).Tag.ToString();
        generating = true; FormFeedback.Text = "";
        SourcePage.IsEnabled = KnowledgeSettings.IsEnabled = GenerateButton.IsEnabled = BackButton.IsEnabled = NextButton.IsEnabled = false;
        GenerationProgress.Visibility = ResultPanel.Visibility = Visibility.Visible;
        GenerationFeedback.Text = "正在生成示例草稿，请稍候…";
        ActualCounts.Text = "";
        await Task.Delay(900);
        if (scenario is "failure" or "unconfigured")
            GenerationFeedback.Text = scenario == "failure" ? "演示 · HTTP 429：服务请求受限。已有草稿已保留，可调整演示状态后重试。" : "演示 · AI 尚未配置。正式生成前请进入“我的”补充服务地址、密钥与模型；也可切换成功状态继续体验。";
        else
        {
            var added = WorkflowPreview.MissingQuestions(CurrentLibrary, targets);
            if (scenario == "partial" && added.Count > 1) added = added.Take(Math.Max(1, added.Count / 2)).ToList();
            foreach (var question in added) CurrentLibrary.Questions.Add(question);
            CurrentLibrary.NotifyChanged(); draftsChanged(CurrentLibrary);
            var missing = WorkflowPreview.MissingQuestions(CurrentLibrary, targets).Count;
            GenerationFeedback.Text = missing > 0 ? $"本次返回 {added.Count} 道示例草稿，仍缺 {missing} 道。已有结果已保留，可审核草稿或切换成功状态补充。"
                : added.Count == 0 ? "已满足目标题量，无需重复生成，可以继续审核。"
                : $"本次新增 {added.Count} 道示例草稿，目标题量已满足；确认前均不可练习。";
        }
        ActualCounts.Text = $"实际共 {CurrentLibrary.Count} 道：单选 {CurrentLibrary.Single} / 多选 {CurrentLibrary.Multiple} / 判断 {CurrentLibrary.Judgment} / 填空 {CurrentLibrary.Blank}";
        generating = false;
        SourcePage.IsEnabled = KnowledgeSettings.IsEnabled = GenerateButton.IsEnabled = BackButton.IsEnabled = true;
        GenerationProgress.Visibility = Visibility.Collapsed; UpdatePage();
    }
    private void CancelImport(object sender, RoutedEventArgs e) => importCancellation?.Cancel();

    private async Task RecognizeExistingAsync()
    {
        AiConfiguration? config;
        try { config = configuration()?.Copy(); }
        catch (InvalidOperationException error) { FormFeedback.Text = error.Message; return; }
        if (config is null) { FormFeedback.Text = "请先在我的中保存并选用 AI 配置。"; return; }
        var plan = MaterialPlanner.Create(Document!, true, [0, 0, 0, 0], config);
        if (importSession is null || importSession.Plan.SourceVersion != plan.SourceVersion || importSession.Plan.ConfigurationId != config.Id || importSession.Plan.Model != config.Model.Trim())
        {
            importSession = new(plan); handledCandidates.Clear(); CurrentLibrary = null;
        }
        Plan = importSession.Plan;
        if (importSession.Complete) { GenerationFeedback.Text = "识别已完成，可查看并审核草稿。"; return; }
        generating = true; FormFeedback.Text = "";
        SourcePage.IsEnabled = GenerateButton.IsEnabled = BackButton.IsEnabled = NextButton.IsEnabled = false;
        ResultPanel.Visibility = GenerationProgress.Visibility = CancelImportButton.Visibility = Visibility.Visible;
        using var cancellation = new CancellationTokenSource(); importCancellation = cancellation;
        void Progress() => GenerationFeedback.Text = $"已处理 {importSession.CompletedBatches} / {Plan.Batches.Length} 批…";
        Progress();
        try
        {
            await questionImport.RunAsync(importSession, config, Progress, cancellation.Token);
            GenerationFeedback.Text = $"识别完成，共得到 {importSession.Questions.Count} 道候选题。";
        }
        catch (OperationCanceledException) { GenerationFeedback.Text = "识别已暂停，已完成批次保留；可手动继续。当前请求可能产生费用。"; }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        { GenerationFeedback.Text = error.Message; }
        finally
        {
            importCancellation = null; generating = false;
            GenerationProgress.Visibility = CancelImportButton.Visibility = Visibility.Collapsed;
            SourcePage.IsEnabled = GenerateButton.IsEnabled = BackButton.IsEnabled = true;
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
                if (CurrentLibrary.Questions.Any(question => SameQuestion(question, candidate)))
                    session.Issues.Add(new(candidate.SourceEvidence.Split('\n')[0], "跳过完全重复题", candidate.Stem));
                else CurrentLibrary.Questions.Add(candidate);
            }
            CurrentLibrary.ImportIssues.Clear(); CurrentLibrary.ImportIssues.AddRange(session.Issues);
            CurrentLibrary.NotifyChanged(); draftsChanged(CurrentLibrary);
        }
        ActualCounts.Text = $"草稿 {CurrentLibrary?.Count ?? 0} 道 · 待补全 {CurrentLibrary?.IncompleteCount ?? 0} 道\n输入 {session.InputTokens} / 输出 {session.OutputTokens} Token" +
            (session.UsageUnknown ? "（另有未确认用量）" : "");
        ImportReport.Text = string.Join("\n\n", session.Issues.Select(issue => issue.Location + " · " + issue.Reason + "\n" + issue.OriginalText));
        ImportReportPanel.Visibility = session.Issues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool SameQuestion(QuestionDraft left, QuestionDraft right) =>
        left.Type == right.Type && left.Stem == right.Stem && left.Options.SequenceEqual(right.Options) &&
        left.CorrectOptions.Order().SequenceEqual(right.CorrectOptions.Order()) && left.JudgmentAnswer == right.JudgmentAnswer &&
        left.BlankAnswers.Select(values => string.Join("\0", values)).SequenceEqual(right.BlankAnswers.Select(values => string.Join("\0", values)));

}
