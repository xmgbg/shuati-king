using System.IO;
using System.Windows;
using System.Windows.Controls;
using ShuatiKing;
using ShuatiKing.Models;
using ShuatiKing.Services;

internal static class InteractivePreview
{
    internal static void Run()
    {
        var app = new App(); app.InitializeComponent();
        var settingsPath = Program.TestPath("history-preview.dat");
        var settings = new SettingsStore(settingsPath); var state = new LearningState();
        foreach (var bank in SampleLibrary.CreateExamples().Take(2))
        {
            var exam = state.Exam = LearningRules.CreateExam(bank, [1, 1, 1, 1], [25, 25, 25, 25], 30);
            exam.Items[0].Selected = [.. exam.Items[0].Question.CorrectOptions];
            exam.Items[1].Selected = [0]; exam.Items[2].Judgment = exam.Items[2].Question.JudgmentAnswer;
            exam.Advance(323); LearningRules.FinishExam(state, exam, false);
        }
        new LearningStore(settings.LearningPath).Save(state);
        var window = new MainWindow(settings) { Title = "刷题KING · 界面验收" };
        window.Loaded += (_, _) => ((StackPanel)window.FindName("NavigationTabs")).Children.OfType<RadioButton>().Single(tab => tab.Content?.ToString() == "考试记录").IsChecked = true;
        app.Run(window);
        if (File.Exists(settingsPath)) File.Delete(settingsPath);
        File.Delete(settings.LearningPath);
    }
}