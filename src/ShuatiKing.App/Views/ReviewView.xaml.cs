using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ShuatiKing.Models;

namespace ShuatiKing.Views;

public partial class ReviewView : UserControl
{
    private readonly SampleLibrary library;
    private readonly Action back;
    private readonly Action home;
    private readonly List<(TextBox Text, ToggleButton Correct)> optionRows = [];
    private bool loading = true;
    private string filter = "all";
    public QuestionDraft? CurrentQuestion { get; private set; }
    public event Action<SampleLibrary>? ExportRequested;

    public ReviewView(SampleLibrary library, Action back, Action home, int questionFontSize = 18, bool fromImport = false)
    {
        this.library = library; this.back = back; this.home = home;
        InitializeComponent();
        ReturnButton.Visibility = fromImport ? Visibility.Visible : Visibility.Collapsed;
        LibraryCaption.Text = library.Name + " · " + library.Source;
        OriginalText.Text = string.IsNullOrEmpty(library.SourceText) ? "当前题库为示例数据，没有原始文档。" : library.SourceText;
        QuestionPreview.FontSize = questionFontSize;
        loading = false; RefreshList();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        SaveEditor(); filter = ((RadioButton)sender).Tag.ToString()!; RefreshList();
    }

    private void RefreshList()
    {
        loading = true;
        var shown = library.Questions.Where(q => filter switch
        {
            "pending" => !q.IsConfirmed, "incomplete" => !q.IsConfirmed && q.ValidationError is not null,
            "confirmed" => q.IsConfirmed, _ => true
        }).ToList();
        QuestionList.ItemsSource = shown;
        QuestionList.SelectedItem = shown.Contains(CurrentQuestion!) ? CurrentQuestion : shown.FirstOrDefault();
        loading = false;
        LoadQuestion(QuestionList.SelectedItem as QuestionDraft);
        EmptyList.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectAllCheck.IsChecked = shown.Count > 0 && shown.All(q => q.BatchSelected);
        UpdateSummary();
    }

    private void QuestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        SaveEditor(); LoadQuestion(QuestionList.SelectedItem as QuestionDraft); UpdateSummary();
    }

    private void LoadQuestion(QuestionDraft? question)
    {
        loading = true; CurrentQuestion = question; EditorRoot.IsEnabled = question is not null;
        StemInput.Text = question?.Stem ?? ""; ExplanationInput.Text = question?.Explanation ?? "";
        QuestionTypeInput.SelectedIndex = (int)(question?.Type ?? QuestionType.Single);
        TrueAnswer.IsChecked = question?.JudgmentAnswer == true;
        FalseAnswer.IsChecked = question?.JudgmentAnswer == false;
        BlankAnswersInput.Text = question is null ? "" : string.Join("\n", question.BlankAnswers.Select(a => string.Join(" | ", a)));
        RenderOptions(question?.Options ?? [], question?.CorrectOptions ?? []);
        UpdateAnswerVisibility(); UpdateValidation();
        loading = false;
    }

    private void TypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        var values = optionRows.Select(r => r.Text.Text).ToList();
        var selected = optionRows.Select((r, i) => (r, i)).Where(x => x.r.Correct.IsChecked == true).Select(x => x.i).ToList();
        if (QuestionTypeInput.SelectedIndex == 0 && selected.Count > 1) selected = selected.Take(1).ToList();
        RenderOptions(values, selected); UpdateAnswerVisibility();
    }

    private void UpdateAnswerVisibility()
    {
        ChoiceAnswers.Visibility = QuestionTypeInput.SelectedIndex < 2 ? Visibility.Visible : Visibility.Collapsed;
        JudgmentAnswers.Visibility = QuestionTypeInput.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        BlankAnswers.Visibility = QuestionTypeInput.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        ChoiceHint.Text = QuestionTypeInput.SelectedIndex == 0 ? "选择一个正确答案" : "勾选所有正确答案";
    }

    private void RenderOptions(IReadOnlyList<string> options, IReadOnlyList<int> correct)
    {
        optionRows.Clear(); OptionsPanel.Children.Clear();
        var values = options.Count == 0 ? new[] { "", "", "", "" } : options.ToArray();
        for (var i = 0; i < values.Length; i++)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(42) });
            row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = new GridLength(40) });
            ToggleButton selector = QuestionTypeInput.SelectedIndex == 0 ? new RadioButton { GroupName = "SingleCorrectAnswer" } : new CheckBox();
            selector.Content = ((char)('A' + i)).ToString(); selector.VerticalAlignment = VerticalAlignment.Center;
            selector.IsChecked = correct.Contains(i); selector.SetResourceReference(ForegroundProperty, "TextBrush");
            System.Windows.Automation.AutomationProperties.SetName(selector, "正确答案 " + selector.Content);
            var text = new TextBox { Text = values[i], MinHeight = 40, Padding = new Thickness(10, 8, 10, 8) };
            System.Windows.Automation.AutomationProperties.SetName(text, "选项 " + selector.Content);
            var remove = new Button { Content = "×", Padding = new Thickness(8, 4, 8, 4), MinHeight = 36, Margin = new Thickness(6, 0, 0, 0), IsEnabled = values.Length > 2 };
            System.Windows.Automation.AutomationProperties.SetName(remove, "删除选项 " + selector.Content);
            int index = i; remove.Click += (_, _) => RemoveOption(index);
            Grid.SetColumn(text, 1); Grid.SetColumn(remove, 2);
            row.Children.Add(selector); row.Children.Add(text); row.Children.Add(remove); OptionsPanel.Children.Add(row);
            optionRows.Add((text, selector));
        }
        AddOptionButton.IsEnabled = values.Length < 26;
    }

    private void AddOption(object sender, RoutedEventArgs e)
    {
        if (optionRows.Count >= 26) return;
        var values = optionRows.Select(r => r.Text.Text).Append("").ToArray();
        var selected = optionRows.Select((r, i) => (r, i)).Where(x => x.r.Correct.IsChecked == true).Select(x => x.i).ToArray();
        RenderOptions(values, selected);
    }

    private void RemoveOption(int index)
    {
        var values = optionRows.Select(r => r.Text.Text).ToList();
        var selected = optionRows.Select((r, i) => (r, i)).Where(x => x.r.Correct.IsChecked == true && x.i != index).Select(x => x.i > index ? x.i - 1 : x.i).ToArray();
        values.RemoveAt(index); RenderOptions(values, selected);
    }

    private void StemEdited(object sender, TextChangedEventArgs e)
    { if (QuestionPreview is not null) QuestionPreview.Text = StemInput.Text; }

    private void SaveEditor()
    {
        if (loading || CurrentQuestion is not { } q) return;
        var before = q.ContentSignature;
        q.Type = (QuestionType)QuestionTypeInput.SelectedIndex; q.Stem = StemInput.Text.Trim(); q.Explanation = ExplanationInput.Text.Trim();
        var choice = q.Type is QuestionType.Single or QuestionType.Multiple;
        q.Options = choice ? optionRows.Select(r => r.Text.Text.Trim()).ToList() : [];
        q.CorrectOptions = choice ? optionRows.Select((r, i) => (r, i)).Where(x => x.r.Correct.IsChecked == true).Select(x => x.i).ToList() : [];
        q.JudgmentAnswer = q.Type == QuestionType.Judgment ? TrueAnswer.IsChecked == true ? true : FalseAnswer.IsChecked == true ? false : null : null;
        q.BlankAnswers = q.Type != QuestionType.Blank || string.IsNullOrWhiteSpace(BlankAnswersInput.Text) ? [] : BlankAnswersInput.Text.Replace("\r", "").Split('\n').Select(line => line.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList()).ToList();
        if (before != q.ContentSignature) q.IsConfirmed = false;
        q.NotifyChanged(); library.NotifyChanged(); UpdateValidation(); UpdateSummary();
    }

    private void UpdateValidation()
    {
        QuestionStatus.Text = CurrentQuestion?.Status ?? "暂无题目";
        ValidationHint.Text = CurrentQuestion?.ValidationError ?? "";
        ValidationPanel.Visibility = CurrentQuestion?.ValidationError is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SaveQuestion(object sender, RoutedEventArgs e)
    {
        SaveEditor();
        ReviewFeedback.Text = CurrentQuestion?.ValidationError is { } error ? "已保存草稿 · " + error + " 补全后才能确认入库。" : "题目修改已保存。内容发生变化的题目需要重新确认入库。";
        RefreshList();
    }

    private void ConfirmQuestion(object sender, RoutedEventArgs e)
    {
        SaveEditor();
        if (CurrentQuestion is not { } q) return;
        if (q.ValidationError is { } error) { ReviewFeedback.Text = "无法入库 · " + error; return; }
        q.IsConfirmed = true; q.BatchSelected = false; q.NotifyChanged(); library.NotifyChanged();
        ReviewFeedback.Text = "已确认入库，返回题库后可查看最新可用状态。"; RefreshList();
    }

    private void SelectAll(object sender, RoutedEventArgs e)
    {
        foreach (var q in QuestionList.Items.Cast<QuestionDraft>()) { q.BatchSelected = SelectAllCheck.IsChecked == true; q.NotifyChanged(); }
        UpdateSummary();
    }

    private void BatchSelectionChanged(object sender, RoutedEventArgs e) => UpdateSummary();

    private void BatchConfirm(object sender, RoutedEventArgs e)
    {
        SaveEditor();
        var selected = library.Questions.Where(q => q.BatchSelected).ToList();
        if (selected.Count == 0) { ReviewFeedback.Text = "请先勾选需要确认的题目。"; return; }
        int accepted = 0, rejected = 0;
        foreach (var q in selected)
        {
            if (q.ValidationError is not null) { rejected++; continue; }
            q.IsConfirmed = true; q.BatchSelected = false; q.NotifyChanged(); accepted++;
        }
        library.NotifyChanged();
        ReviewFeedback.Text = $"已确认 {accepted} 道题。" + (rejected > 0 ? $" {rejected} 道题尚未补全，已保留且未入库，可筛选“待补全”继续修正。" : "");
        RefreshList();
    }

    private void UpdateSummary()
    {
        ReviewSummary.Text = $"共 {library.Count} 道 · {library.ReviewSummary}";
        SelectionCount.Text = $"已勾选 {library.Questions.Count(q => q.BatchSelected)}";
        ExportButton.IsEnabled = library.ConfirmedCount > 0;
    }

    public void SaveDraft() => SaveEditor();

    private void Return(object sender, RoutedEventArgs e) { SaveEditor(); back(); }
    private void Home(object sender, RoutedEventArgs e) { SaveEditor(); home(); }
    private void Export(object sender, RoutedEventArgs e) { SaveEditor(); ExportRequested?.Invoke(library); }
}
