using System.IO; //System.IO.FileInfo, System.IO.StreamReader, System.IO.StreamWriter
using System; //Exception
using System.Text; //Encoding
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Config;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Windows.Documents;

namespace VRCSupportWindow
{
    public class LogAnalyzer : DispatcherObject
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public FileInfo GetLatestLogfileInfo(string? logDir = null)
        {
            if (logDir == null)
            {
                logDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low\\VRChat\\VRChat";
            }
            logger.Info("VRChat Log Dir: {}", logDir);
            DirectoryInfo dir = new DirectoryInfo(logDir);

            //タイムスタンプ逆順にソートして、最後に更新されたログファイルを取得
            FileInfo[] info = dir.GetFiles("output_log_*.txt").OrderByDescending(p => p.LastWriteTime).ToArray();

            //JSONデータを加工時の参照用にキャッシュ
            Dictionary<string, JObject> userCache = new Dictionary<string, JObject>();
            Dictionary<string, JObject> worldCache = new Dictionary<string, JObject>();

            //進捗表示用
            FileInfo latestLogfileInfo = info[0];

            return latestLogfileInfo;
        }

        public void ExecAnalyzeLog(ref bool _is_analyzing)
        {
            //アプリ実行中は処理をループ
            //最新のファイルを読み取る
            //一行ずつ監視して読み取る
            //1分に1回程度、新しいファイルができていないか監視して、新しいファイルができていたら、そちらから読み込むように切り替える

            FileInfo latestLogfileInfo = GetLatestLogfileInfo();

            FileStream fs = latestLogfileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            StreamReader reader = new StreamReader(fs, Encoding.UTF8);

            int counter = 0;


            while (_is_analyzing)
            {
                counter += 1;
                AnalyzeLogFile(ref reader, ref _is_analyzing);

                FileInfo nextLatestLogfileInfo = GetLatestLogfileInfo();
                logger.Info($"latestLogfileInfo: {latestLogfileInfo.Name}");
                logger.Info($"nextLatestLogfileInfo: {nextLatestLogfileInfo.Name}");

                if (nextLatestLogfileInfo.Name != latestLogfileInfo.Name)
                {
                    latestLogfileInfo = nextLatestLogfileInfo;
                    fs.Close();
                    fs = latestLogfileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    reader = new StreamReader(fs, Encoding.UTF8);
                }

                System.Threading.Thread.Sleep(5000);
            }
        }

        public void AnalyzeLogFile(ref StreamReader reader, ref bool _is_analyzing)
        {

            string? line; //ファイルを読み終わったらnullになる
            string worldVisitTimestamp = "";
            string worldName = "";
            string worldInstanceInfo = "";

            int refreshCounter = 0;
            int fileCheckPeriod = 5000; //ログファイルをチェックする秒数(ミリ秒

            //JSONデータを加工時の参照用にキャッシュ
            Dictionary<string, JObject> userCache = new Dictionary<string, JObject>();
            Dictionary<string, JObject> worldCache = new Dictionary<string, JObject>();


            while (_is_analyzing == true && refreshCounter < 11) //1分間更新がなかったら新しいファイルがないか確認
            {

                line = reader.ReadLine();

                //ファイルを読み込んでnullだったら、既定の秒数待って最初に戻って再読み込み
                if (line == null)
                {
                    refreshCounter += 1;
                    System.Threading.Thread.Sleep(fileCheckPeriod);
                    continue;
                }
                //ログを1行ずつ処理。ログにはワールドの情報→ユーザーの情報の順番で情報が登場する想定。
                //JSON詳細データは、それぞれの情報の前に登場する
                //logger.Debug($"line: {line}");

                Regex reg;
                MatchCollection mc;

                //JSON
                //詳細をシリアライズ済みJSONとして出力している行の処理
                reg = new Regex("(?<timestamp>[0-9.]+ [0-9:]+).+"
                                + Regex.Escape("[API]")
                                + " {(?<rawjson>{.*})}$"
                                );
                mc = reg.Matches(line);
                if (mc.Count > 0)
                {
                    //Console.WriteLine(line);
                    logger.Debug(line);
                    foreach (Match match in mc)
                    {
                        GroupCollection groups = match.Groups;
                        JObject json;
                        try
                        {
                            //steamDetailsが空の場合、JSONのフォーマット違反になるため、もし文字列があれば変換前に削除する
                            json = JObject.Parse(groups["rawjson"].Value.Replace("\"steamDetails\":{{}},", ""));
                        }
                        catch (Newtonsoft.Json.JsonReaderException)
                        {
                            //不正なJSONは無視する
                            //自分自身の初期化API呼び出しの際のsteamDetailsのフォーマットがおかしい。
                            //今回は利用しないデータなので、無視して次の行へ
                            logger.Info("不正なJSONデータです。");
                            logger.Info(line);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            //全く解析ができなかった場合は、それを通常のログにその行を出力
                            logger.Info("その他のエラーです。");
                            logger.Info(line);
                            logger.Info($"message: {ex.Message}");
                            logger.Info($"{ex.StackTrace}");
                            continue;
                        }

                        if (json["authorName"] != null)
                        {
                            //Console.WriteLine("World!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                            string name_raw = json["name"].ToString();
                            //Console.WriteLine(name_raw);
                            json["raw_json"] = groups["rawjson"].Value;
                            worldCache[name_raw] = json;
                        }
                        else if (json["displayName"] != null)
                        {
                            //Console.WriteLine("User!!!!!!!!!!!!!!!!!!!");
                            string name_raw = json["displayName"].ToString();
                            //Console.WriteLine(name_raw);
                            json["raw_json"] = groups["rawjson"].Value;
                            userCache[name_raw] = json;
                        }
                    }
                    continue;
                }

                //ワールドインスタンス情報

                reg = new Regex("(?<timestamp>[0-9.]+ [0-9:]+).+Joining (?<worldinstanceinfo>wrld_.+:.+~region.+)");
                mc = reg.Matches(line);
                if (mc.Count > 0)
                {
                    //Console.WriteLine(line);
                    GroupCollection groups;
                    foreach (Match match in mc)
                    {
                        groups = match.Groups;
                        //Console.WriteLine(groups["timestamp"].Value);
                        worldInstanceInfo = groups["worldinstanceinfo"].Value.Replace(":", "&instanceId=");

                        //logger.Info(worldInstanceInfo);
                    }
                }

                //ワールド
                reg = new Regex("(?<timestamp>[0-9.]+ [0-9:]+).+Joining or Creating Room: (?<worldname>.+)");
                mc = reg.Matches(line);
                if (mc.Count > 0)
                {
                    //Console.WriteLine(line);
                    GroupCollection groups;
                    foreach (Match match in mc)
                    {
                        groups = match.Groups;
                        //Console.WriteLine(groups["timestamp"].Value);
                        worldVisitTimestamp = groups["timestamp"].Value;
                        //Console.WriteLine(groups["worldname"].Value);
                        worldName = groups["worldname"].Value;

                        //logger.Info(worldName);
                    }


                    //worlcCacheに情報があれば取得
                    string worldId = "";
                    string authorName = "";
                    string authorId = "";
                    string description = "";
                    string imageUrl = "";
                    string url = "";
                    string rawJson = "";
                    try
                    {
                        //先にインスタンス情報が取れている前提
                        url = "https://vrchat.com/home/launch?worldId=" + worldInstanceInfo;

                        //現在ここまでの詳細情報は取得していないが、ロジックとしては残している
                        if (worldCache.ContainsKey(worldName))
                        {
                            JObject current_cache = worldCache[worldName];
                            //logger.Info(current_cache);
                            worldId = current_cache["id"].ToString();
                            authorName = current_cache["authorName"].ToString();
                            authorId = current_cache["authorId"].ToString();
                            description = current_cache["description"].ToString();
                            imageUrl = current_cache["imageUrl"].ToString();
                            rawJson = current_cache["raw_json"].ToString();
                        }
                    }
                    catch (System.Collections.Generic.KeyNotFoundException)
                    {
                        //情報が取得できなかった場合は空文字のまま
                        logger.Info($"{worldName}の詳細情報はありませんでした。");
                    }
                    catch (Exception ex)
                    {
                        //情報が取得したが、想定外のエラーの場合はその旨だけログに出す
                        logger.Info($"{worldName}の詳細情報取得時に不明なエラーが発生しました。");
                        logger.Info($"message: {ex.Message}");
                        logger.Info($"{ex.StackTrace}");
                    }

                    //worldVisitHistory
                    //logger.Info("WorldVisitHistory: {0},{1},{2},{3},{4},{5},{6},{7},{8}",
                    //worldVisitTimestamp, worldName, worldId, authorName, authorId, description, imageUrl, url, rawJson);

                    try
                    {
                        //メインウィンドウにログ追記
                        this.Dispatcher.Invoke((Action)(() =>
                            {
                                var mainWindow = (MainWindow)App.Current.MainWindow;

                                //最後に訪問したワールドに戻るためのURLを表示
                                mainWindow.lastWorldUrlLinkText3.Text = mainWindow.lastWorldUrlLinkText2.Text;
                                mainWindow.lastWorldUrlLinkText2.Text = mainWindow.lastWorldUrlLinkText1.Text;
                                mainWindow.lastWorldUrlLinkText1.Text = url;

                                string log_line = $"{worldName} (Visited at {worldVisitTimestamp})";
                                mainWindow.Logs.Text += log_line + "\n";

                                logger.Info($"Add World: {log_line}");
                            }));
                    }
                    catch (Exception ex)
                    {
                        //更新失敗全般をログに記録
                        logger.Info($"表示の更新に失敗しました。");
                        logger.Info($"message: {ex.Message}");
                        logger.Info($"{ex.StackTrace}");
                    }
                    continue;
                }

                //ユーザー
                reg = new Regex("(?<timestamp>[0-9.]+ [0-9:]+).+"
                                        + Regex.Escape("[Behaviour]")
                                        + " Initialized PlayerAPI \"(?<playername>.+)\" is (remote|local)"
                                    );
                mc = reg.Matches(line);
                if (mc.Count > 0)
                {
                    //Console.WriteLine(line);
                    string timestamp = "";
                    string displayName = "";
                    foreach (Match match in mc)
                    {
                        GroupCollection groups = match.Groups;
                        //Console.WriteLine(groups["timestamp"].Value);
                        timestamp = groups["timestamp"].Value;
                        //Console.WriteLine(groups["playername"].Value);
                        displayName = groups["playername"].Value;
                    }

                    //userCacheにbioの情報があれば取得
                    string bio = "";
                    try
                    {
                        //現在ここまでの詳細情報は取得していないが、ロジックとしては残している
                        if (userCache.ContainsKey(displayName))
                        {
                            JObject current_cache = userCache[displayName];
                            if (current_cache.ContainsKey("bio"))
                            {
                                bio = current_cache["bio"].ToString();
                            }
                        }
                    }
                    catch (System.Collections.Generic.KeyNotFoundException)
                    {
                        //情報が取得できなかった場合は空文字のまま
                        logger.Info($"{displayName}の詳細情報はありませんでした。");
                    }
                    catch (Exception ex)
                    {
                        //情報が取得したが、想定外のエラーの場合はその旨だけログに出す
                        logger.Info($"{displayName}の詳細情報取得時に不明なエラーが発生しました。");
                        logger.Info($"message: {ex.Message}");
                        logger.Info($"{ex.StackTrace}");
                    }

                    //logger.Info("UserEncounterHistory: {0},{1},{2},{3},{4}",
                    //timestamp, displayName, bio, worldVisitTimestamp, worldName);


                    try
                    {
                        //メインウィンドウにログ追記
                        this.Dispatcher.Invoke((Action)(() =>
                            {
                                var mainWindow = (MainWindow)App.Current.MainWindow;

                                string log_line = $"{timestamp}     {displayName}";
                                //前に2スペース分インデントを付けて表示
                                mainWindow.Logs.Text += "  " + log_line + "\n";

                                logger.Info($"Add User: {log_line}");
                            }));
                        //logger.Info(newRecord);
                    }
                    catch (Exception ex)
                    {
                        //更新失敗全般をログに記録
                        logger.Info($"表示の更新に失敗しました。");
                        logger.Info($"message: {ex.Message}");
                        logger.Info($"{ex.StackTrace}");
                    }
                }
            }
        }
    }
}