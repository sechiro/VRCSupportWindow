using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Threading;
using System.Windows.Controls;


namespace VRCSupportWindow
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string GetAppPath()
        {
            string? appPath = System.IO.Path.GetDirectoryName(
                System.AppContext.BaseDirectory);
            if (appPath is null)
            {
                throw new DirectoryNotFoundException("実行ファイルのパス取得失敗");
            }

            return appPath;
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // メイン ウィンドウ表示
            MainWindow window = new MainWindow(e);
            window.Show();
        }

    }
}
