using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;

using System.IO;

using System.Data;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using NLog;
using System.Windows.Controls;


namespace VRCSupportWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _is_analyzing = false;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public MainWindow()
        {
            InitializeComponent();
        }

        /**
        * ボタンクリックに対応したアクション定義
        */
        public async void Button_RunExecAnalyzeLog(object sender, RoutedEventArgs e)
        {
            //表示データを初期化
            Logs.Text = "";

            statusBar.Content = "ログデータ解析中です。";
            //loadingText.Visibility = Visibility.Visible;
            MenuStart.Visibility = Visibility.Collapsed;
            MenuStop.Visibility = Visibility.Visible;

            await RunExecAnalyzeLog();

            //終了後メニューを戻す
            //loadingText.Visibility = Visibility.Collapsed;
            MenuStart.Visibility = Visibility.Visible;
            MenuStop.Visibility = Visibility.Collapsed;
            statusBar.Content = "";
        }
        public void Button_StopExecAnalyzeLog(object sender, RoutedEventArgs e)
        {
            _is_analyzing = false;
            statusBar.Content = "終了処理中です";

        }
        private Task RunExecAnalyzeLog()
        {
            _is_analyzing = true;
            var LogAnalyzer = new LogAnalyzer();
            return Task.Run(() => { LogAnalyzer.ExecAnalyzeLog(ref _is_analyzing); });
        }
        public void Button_FirstHelp(object sender, RoutedEventArgs e)
        {
            PopupHelpWindow();
        }
        public void Button_Readme(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://github.com/sechiro/VRCSupportWindow");
        }
        public void Button_Credit(object sender, RoutedEventArgs e)
        {
            CreditWindow creditWin = new CreditWindow();
            creditWin.Owner = this;
            creditWin.ShowDialog();
        }


        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            TextBox parentItem = ((e.Source as MenuItem).Parent as ContextMenu).PlacementTarget as TextBox;
            string nameText = parentItem.Text;
            Clipboard.SetText(nameText);
            return;
        }
        private void MenuItemOpenLink_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            TextBox parentItem = ((e.Source as MenuItem).Parent as ContextMenu).PlacementTarget as TextBox;
            string nameText = parentItem.Text;
            OpenBrowser(nameText);
            return;
        }
        private void MenuItemCopyLog_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            TextBlock parentItem = ((e.Source as MenuItem).Parent as ContextMenu).PlacementTarget as TextBlock;
            string nameText = parentItem.Text;
            Clipboard.SetText(nameText);
            return;
        }
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            //Windowが表示された直後の処理
            //現在は特に指定なし
        }

        private void PopupHelpWindow()
        {
            HelpWindow helpWin = new HelpWindow();
            helpWin.Owner = this;
            helpWin.ShowDialog();
        }
        public static void OpenBrowser(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch
            {

                url = url.Replace("&", "^&");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });

            }
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            Application.Current.Shutdown();
        }
    }
}
