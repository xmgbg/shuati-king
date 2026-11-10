using System.Windows;

namespace ShuatiKing;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        while (true)
        {
            try { MainWindow = new MainWindow(); MainWindow.Show(); return; }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException or Microsoft.Data.Sqlite.SqliteException)
            {
                if (MessageBox.Show("本地数据初始化或迁移失败，已暂停进入软件。原数据不会清空。\n请检查磁盘、权限和旧数据文件，修复后点击确定重试，或取消退出。\n\n" + error.Message,
                    "无法打开本地数据", MessageBoxButton.OKCancel, MessageBoxImage.Error) != MessageBoxResult.OK)
                { Shutdown(); return; }
            }
        }
    }
}
