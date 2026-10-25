using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShuatiKing;
using ShuatiKing.Views;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        window.Show();
        window.UpdateLayout();
        var host = (ContentControl)window.FindName("PageHost");
        var library = (LibraryView)host.Content;
        var list = (ListBox)library.FindName("LibraryList");
        foreach (var index in new[] { 0, 1, 2, 3 })
        {
            list.SelectedIndex = index;
            window.UpdateLayout();
            Check(library.SelectedLibrary?.Count == new[] { 128, 86, 64, 32 }[index], "选中题库数量正确");
        }
        Check(!library.SelectedLibrary!.IsReady, "待审核题库不可练习");
        list.SelectedIndex = 0;
        Capture(window, "home-light");
        window.Width = 1120; window.Height = 720;
        Capture(window, "home-compact");
        window.Close();
        Console.WriteLine("通过：首页四题库切换、待审核状态与两种窗口渲染。");
    }

    internal static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Capture(Window window, string name)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var surface = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        Directory.CreateDirectory(".artifacts");
        using var file = File.Create($".artifacts/{name}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
    }
}
