using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ShuatiKing.Models;

namespace ShuatiKing.Views;

public sealed class AnswerEditor : UserControl
{
    public AnswerItem Item { get; }
    public AnswerEditor(AnswerItem item, int fontSize, bool readOnly = false)
    {
        Item = item;
        SetResourceReference(ForegroundProperty, "TextBrush");
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = item.Question.TypeName, FontSize = 14, Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = item.Question.Stem, FontSize = fontSize, TextWrapping = TextWrapping.Wrap, LineHeight = fontSize * 1.6, Margin = new Thickness(0, 0, 0, 24) });
        var inputs = new StackPanel { IsEnabled = !readOnly };
        var q = item.Question;
        var group = "Answer_" + Guid.NewGuid().ToString("N");
        if (q.Type is QuestionType.Single or QuestionType.Multiple)
        {
            for (var i = 0; i < q.Options.Count; i++)
            {
                int index = i;
                ToggleButton choice = q.Type == QuestionType.Single ? new RadioButton { GroupName = group } : new CheckBox();
                choice.SetResourceReference(ForegroundProperty, "TextBrush");
                choice.Content = new TextBlock { Text = $"{(char)('A' + i)}.  {q.Options[i]}", TextWrapping = TextWrapping.Wrap, FontSize = 16, Margin = new Thickness(10, 0, 0, 0) };
                choice.IsChecked = item.Selected.Contains(i);
                choice.Checked += (_, _) => { if (q.Type == QuestionType.Single) item.Selected.Clear(); if (!item.Selected.Contains(index)) item.Selected.Add(index); };
                choice.Unchecked += (_, _) => item.Selected.Remove(index);
                AutomationPropertiesName(choice, "选项 " + (char)('A' + i));
                AddChoice(inputs, choice);
            }
        }
        else if (q.Type == QuestionType.Judgment)
        {
            foreach (bool value in new[] { true, false })
            {
                var choice = new RadioButton { GroupName = group, Content = value ? "正确" : "错误", IsChecked = item.Judgment == value, FontSize = 16 };
                choice.SetResourceReference(ForegroundProperty, "TextBrush"); choice.Checked += (_, _) => item.Judgment = value;
                AddChoice(inputs, choice);
            }
        }
        else
        {
            while (item.Blanks.Count < q.BlankAnswers.Count) item.Blanks.Add("");
            for (var i = 0; i < q.BlankAnswers.Count; i++)
            {
                int index = i;
                var input = new TextBox { Text = item.Blanks[i], FontSize = 16, Margin = new Thickness(0, 0, 0, 16) };
                input.TextChanged += (_, _) => item.Blanks[index] = input.Text;
                AutomationPropertiesName(input, $"第 {i + 1} 空");
                var label = new Label { Content = $"第 {i + 1} 空", Target = input, Padding = new Thickness(0, 0, 0, 8) };
                label.SetResourceReference(ForegroundProperty, "TextBrush"); inputs.Children.Add(label); inputs.Children.Add(input);
            }
        }
        panel.Children.Add(inputs); Content = panel;
    }
    private static void AutomationPropertiesName(DependencyObject target, string name) => System.Windows.Automation.AutomationProperties.SetName(target, name);
    private static void AddChoice(Panel panel, Control choice)
    {
        var border = new Border { Child = choice, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 10) };
        border.SetResourceReference(Border.BorderBrushProperty, "LineBrush"); border.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        if (choice is ToggleButton toggle)
        {
            void Paint() => border.SetResourceReference(BackgroundProperty, toggle.IsChecked == true ? "TintBrush" : "SurfaceBrush");
            toggle.Checked += (_, _) => Paint(); toggle.Unchecked += (_, _) => Paint(); Paint();
        }
        panel.Children.Add(border);
    }
}
