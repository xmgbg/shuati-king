using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ShuatiKing.Views;

public partial class ExamView
{
    private readonly CheckBox[] typeSelections = new CheckBox[4];
    private readonly TextBlock[] availableInputs = new TextBlock[4], subtotalLabels = new TextBlock[4];
    private readonly DispatcherTimer holdTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private Grid? heldRow;
    private Button? heldHandle;
    private Point holdOrigin;
    private TypeDragPreview? dragPreview;
    private const string TypeRowFormat = "ShuatiKing.ExamTypeRow";

    private void InitializeTypeSettings()
    {
        holdTimer.Tick += (_, _) => BeginTypeDrag();
        Unloaded += (_, _) => CancelHold();
        TypeRows.AllowDrop = true; TypeRows.Background = Brushes.Transparent;
        Setup.PreviewDragOver += (_, e) =>
        {
            if (dragPreview is null) return;
            var position = e.GetPosition(TypeRows);
            var inside = position.X >= 0 && position.X <= TypeRows.ActualWidth && position.Y >= 0 && position.Y <= TypeRows.ActualHeight;
            dragPreview.Update(position, inside ? InsertionY(DropSlot(position)) : null);
        };
        TypeRows.PreviewDragOver += (_, e) =>
        {
            if (e.Data.GetData(TypeRowFormat) is not Grid source || !TypeRows.Children.Contains(source)) return;
            e.Effects = DragDropEffects.Move; e.Handled = true;
        };
        TypeRows.PreviewDrop += (_, e) =>
        {
            if (e.Data.GetData(TypeRowFormat) is not Grid source || !TypeRows.Children.Contains(source)) return;
            int destination = DropSlot(e.GetPosition(TypeRows));
            if (TypeRows.Children.IndexOf(source) < destination) destination--;
            MoveTypeRow((int)source.Tag, destination); e.Handled = true;
        };
    }

    private void BuildTypeRow(int type, string name)
    {
        var row = new Grid { Tag = type, Height = 64, Background = Brushes.Transparent };
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1.5, GridUnitType.Star), MinWidth = 152 });
        row.ColumnDefinitions.Add(new() { MinWidth = 80 });
        row.ColumnDefinitions.Add(new() { MinWidth = 100 }); row.ColumnDefinitions.Add(new() { MinWidth = 100 });
        row.ColumnDefinitions.Add(new() { Width = new GridLength(0.75, GridUnitType.Star), MinWidth = 70 });
        var label = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var circle = new CheckBox { IsChecked = true, Style = (Style)FindResource("TypeCircle") };
        System.Windows.Automation.AutomationProperties.SetName(circle, "启用" + name);
        typeSelections[type] = circle;
        var title = new TextBlock { Text = name, FontSize = 16, Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand };
        title.MouseLeftButtonDown += (_, _) => circle.IsChecked = circle.IsChecked != true;
        var drag = new Button { Style = (Style)FindResource("TypeDragHandle"), Margin = new Thickness(0, 0, 8, 0), ToolTip = "调整顺序" };
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
        label.Children.Add(drag); label.Children.Add(circle); label.Children.Add(title);
        drag.PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (Keyboard.Modifiers != ModifierKeys.Alt || key is not (Key.Up or Key.Down)) return;
            MoveTypeRow(type, TypeRows.Children.IndexOf(row) + (key == Key.Up ? -1 : 1)); e.Handled = true;
        };
        row.Children.Add(label);
        var count = new TextBox { Text = type == 0 ? "10" : "5", MaxLength = 4, Width = 80, Height = 38, MinHeight = 38, Padding = new Thickness(10, 6, 10, 6), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var score = new TextBox { Text = type < 2 ? "2" : "1", MaxLength = 8, Width = 80, Height = 38, MinHeight = 38, Padding = new Thickness(10, 6, 10, 6), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(count, name + "考试题量");
        System.Windows.Automation.AutomationProperties.SetName(score, name + "每题分值");
        countInputs[type] = count; scoreInputs[type] = score;
        count.TextChanged += SettingsChanged; score.TextChanged += SettingsChanged;
        circle.Checked += (_, e) => { count.IsEnabled = score.IsEnabled = true; title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush"); SettingsChanged(circle, e); };
        circle.Unchecked += (_, e) => { count.IsEnabled = score.IsEnabled = false; title.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); SettingsChanged(circle, e); };
        var available = new TextBlock { FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center };
        var subtotal = new TextBlock { FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(available, name + "可用题量");
        System.Windows.Automation.AutomationProperties.SetName(subtotal, name + "分值小计");
        availableInputs[type] = available; subtotalLabels[type] = subtotal;
        Grid.SetColumn(available, 1); Grid.SetColumn(count, 2); Grid.SetColumn(score, 3); Grid.SetColumn(subtotal, 4);
        row.Children.Add(available); row.Children.Add(count); row.Children.Add(score); row.Children.Add(subtotal);
        var divider = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
        divider.SetResourceReference(Border.BackgroundProperty, "LineBrush"); Grid.SetColumnSpan(divider, 5); row.Children.Add(divider);
        TypeRows.Children.Add(row);
    }

    public void MoveTypeRow(int type, int targetIndex)
    {
        var row = TypeRows.Children.Cast<Grid>().Single(r => (int)r.Tag == type);
        targetIndex = Math.Clamp(targetIndex, 0, TypeRows.Children.Count - 1);
        if (TypeRows.Children.IndexOf(row) == targetIndex) return;
        TypeRows.Children.Remove(row); TypeRows.Children.Insert(targetIndex, row);
        UpdateSettings();
    }
    private int DropSlot(Point position)
    {
        int slot = 0;
        foreach (Grid row in TypeRows.Children)
        {
            if (position.Y < row.TranslatePoint(new Point(), TypeRows).Y + row.ActualHeight / 2) break;
            slot++;
        }
        return slot;
    }
    private double InsertionY(int slot)
    {
        var row = (Grid)TypeRows.Children[Math.Min(slot, TypeRows.Children.Count - 1)];
        return row.TranslatePoint(new Point(), TypeRows).Y + (slot == TypeRows.Children.Count ? row.ActualHeight + 4 : -4);
    }
    private void BeginTypeDrag()
    {
        var row = heldRow; var handle = heldHandle;
        var pickup = row is null ? new Point() : Mouse.GetPosition(row);
        CancelHold();
        if (row is null || handle is null || Mouse.LeftButton != MouseButtonState.Pressed) return;
        var layer = AdornerLayer.GetAdornerLayer(TypeRows)!;
        dragPreview = new TypeDragPreview(row, TypeRows, pickup);
        layer.Add(dragPreview);
        dragPreview.Update(Mouse.GetPosition(TypeRows), InsertionY(TypeRows.Children.IndexOf(row)));
        row.Opacity = 0.2;
        GiveFeedbackEventHandler cursor = (_, e) => { e.UseDefaultCursors = false; Mouse.SetCursor(Cursors.SizeAll); e.Handled = true; };
        handle.GiveFeedback += cursor;
        try { DragDrop.DoDragDrop(handle, new DataObject(TypeRowFormat, row), DragDropEffects.Move); }
        finally
        {
            row.Opacity = 1; layer.Remove(dragPreview); dragPreview = null;
            handle.GiveFeedback -= cursor; Mouse.SetCursor(null);
        }
    }

    private sealed class TypeDragPreview : Adorner
    {
        private readonly ImageSource snapshot;
        private readonly Size size;
        private readonly Point pickup;
        private readonly Brush surface;
        private readonly Pen outline, insertion;
        private Point position;
        private double? insertionY;

        public TypeDragPreview(Grid row, FrameworkElement host, Point pickup) : base(host)
        {
            IsHitTestVisible = false; this.pickup = pickup;
            size = row.RenderSize;
            surface = (Brush)row.FindResource("SurfaceBrush");
            outline = new Pen((Brush)row.FindResource("LineBrush"), 1);
            insertion = new Pen((Brush)row.FindResource("AccentBrush"), 2);
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 5, Opacity = 0.24 };
            var dpi = VisualTreeHelper.GetDpi(row);
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * dpi.DpiScaleX), (int)Math.Ceiling(size.Height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(new VisualBrush(row), null, new Rect(size));
            bitmap.Render(visual); bitmap.Freeze(); snapshot = bitmap;
        }
        public void Update(Point position, double? insertionY)
        {
            this.position = position; this.insertionY = insertionY; InvalidateVisual();
        }
        protected override void OnRender(DrawingContext drawing)
        {
            if (insertionY is { } y) drawing.DrawLine(insertion, new Point(0, y), new Point(size.Width, y));
            var origin = new Point(position.X - pickup.X, position.Y - pickup.Y - 6);
            drawing.DrawRoundedRectangle(surface, outline, new Rect(origin.X - 8, origin.Y - 6, size.Width + 16, size.Height + 12), 6, 6);
            drawing.DrawImage(snapshot, new Rect(origin, size));
        }
    }
    private void CancelHold()
    {
        holdTimer.Stop(); var handle = heldHandle; heldRow = null; heldHandle = null;
        if (handle?.IsMouseCaptured == true) handle.ReleaseMouseCapture();
    }
}
