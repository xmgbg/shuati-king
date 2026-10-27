using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ShuatiKing.Views;

public partial class ExamView
{
    private readonly CheckBox[] typeSelections = new CheckBox[4];
    private readonly DispatcherTimer holdTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private Grid? heldRow;
    private Button? heldHandle;
    private Point holdOrigin;
    private const string TypeRowFormat = "ShuatiKing.ExamTypeRow";

    private void InitializeTypeSettings()
    {
        holdTimer.Tick += (_, _) => BeginTypeDrag();
        Unloaded += (_, _) => CancelHold();
    }

    private void BuildTypeRow(int type, string name)
    {
        var row = new Grid { Tag = type, Margin = new Thickness(0, 0, 0, 12), AllowDrop = true, Background = Brushes.Transparent };
        row.ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new());
        var label = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var circle = new CheckBox { IsChecked = true, Style = (Style)FindResource("TypeCircle") };
        System.Windows.Automation.AutomationProperties.SetName(circle, "启用" + name);
        typeSelections[type] = circle;
        var title = new TextBlock { Text = name, FontSize = 14, Margin = new Thickness(8, 0, 10, 0), Cursor = Cursors.Hand };
        title.MouseLeftButtonDown += (_, _) => circle.IsChecked = circle.IsChecked != true;
        var drag = new Button { Content = "拖动", FontSize = 12, MinHeight = 32, Padding = new Thickness(6, 4, 6, 4), Cursor = Cursors.SizeAll, Margin = new Thickness(0, 0, 6, 0), ToolTip = "长按 350 毫秒后拖动调整题型顺序" };
        System.Windows.Automation.AutomationProperties.SetName(drag, "长按拖动" + name);
        drag.PreviewMouseLeftButtonDown += (_, e) => { CancelHold(); heldRow = row; heldHandle = drag; holdOrigin = e.GetPosition(drag); drag.CaptureMouse(); holdTimer.Start(); e.Handled = true; };
        drag.PreviewMouseMove += (_, e) =>
        {
            var position = e.GetPosition(drag);
            if (heldHandle == drag && e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(position.X - holdOrigin.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(position.Y - holdOrigin.Y) >= SystemParameters.MinimumVerticalDragDistance))
                BeginTypeDrag();
        };
        drag.PreviewMouseLeftButtonUp += (_, _) => CancelHold();
        drag.LostMouseCapture += (_, _) => CancelHold();
        label.Children.Add(circle); label.Children.Add(title); label.Children.Add(drag);
        foreach (int direction in new[] { -1, 1 })
        {
            var move = new Button { Content = direction < 0 ? "↑" : "↓", FontSize = 14, MinHeight = 32, Width = 28, Padding = new Thickness(3), Margin = new Thickness(0, 0, 4, 0), ToolTip = direction < 0 ? "上移题型" : "下移题型" };
            System.Windows.Automation.AutomationProperties.SetName(move, (direction < 0 ? "上移" : "下移") + name);
            move.Click += (_, _) => MoveTypeRow(type, TypeRows.Children.IndexOf(row) + direction);
            label.Children.Add(move);
        }
        row.Children.Add(label);
        var count = new TextBox { Text = type == 0 ? "10" : "5", MaxLength = 4, Margin = new Thickness(0, 0, 14, 0) };
        var score = new TextBox { Text = type < 2 ? "2" : "1", MaxLength = 8 };
        System.Windows.Automation.AutomationProperties.SetName(count, name + "考试题量");
        System.Windows.Automation.AutomationProperties.SetName(score, name + "每题分值");
        countInputs[type] = count; scoreInputs[type] = score;
        count.TextChanged += SettingsChanged; score.TextChanged += SettingsChanged;
        circle.Checked += (_, e) => { count.IsEnabled = score.IsEnabled = true; title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush"); SettingsChanged(circle, e); };
        circle.Unchecked += (_, e) => { count.IsEnabled = score.IsEnabled = false; title.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); SettingsChanged(circle, e); };
        Grid.SetColumn(count, 1); Grid.SetColumn(score, 2); row.Children.Add(count); row.Children.Add(score);
        row.DragOver += (_, e) =>
        {
            if (!e.Data.GetDataPresent(TypeRowFormat) || e.Data.GetData(TypeRowFormat) is not Grid source || !TypeRows.Children.Contains(source)) { e.Effects = DragDropEffects.None; return; }
            ClearDropHighlight(); row.SetResourceReference(BackgroundProperty, "TintBrush"); e.Effects = DragDropEffects.Move; e.Handled = true;
        };
        row.DragLeave += (_, _) => row.Background = Brushes.Transparent;
        row.Drop += (_, e) =>
        {
            if (e.Data.GetData(TypeRowFormat) is not Grid source || !TypeRows.Children.Contains(source)) return;
            int destination = TypeRows.Children.IndexOf(row) + (e.GetPosition(row).Y > row.ActualHeight / 2 ? 1 : 0);
            if (TypeRows.Children.IndexOf(source) < destination) destination--;
            MoveTypeRow((int)source.Tag, destination); ClearDropHighlight(); e.Handled = true;
        };
        TypeRows.Children.Add(row);
    }

    public void MoveTypeRow(int type, int targetIndex)
    {
        var row = TypeRows.Children.Cast<Grid>().Single(r => (int)r.Tag == type);
        targetIndex = Math.Clamp(targetIndex, 0, TypeRows.Children.Count - 1);
        if (TypeRows.Children.IndexOf(row) == targetIndex) return;
        TypeRows.Children.Remove(row); TypeRows.Children.Insert(targetIndex, row);
        Feedback.Text = "题型顺序已调整，将按当前顺序出卷。"; UpdateSettings();
    }
    private void BeginTypeDrag()
    {
        var row = heldRow; var handle = heldHandle;
        CancelHold();
        if (row is null || handle is null || Mouse.LeftButton != MouseButtonState.Pressed) return;
        row.Opacity = 0.6;
        try { DragDrop.DoDragDrop(handle, new DataObject(TypeRowFormat, row), DragDropEffects.Move); }
        finally { row.Opacity = 1; ClearDropHighlight(); }
    }
    private void ClearDropHighlight() { foreach (Grid row in TypeRows.Children) row.Background = Brushes.Transparent; }
    private void CancelHold()
    {
        holdTimer.Stop(); var handle = heldHandle; heldRow = null; heldHandle = null;
        if (handle?.IsMouseCaptured == true) handle.ReleaseMouseCapture();
    }
}
