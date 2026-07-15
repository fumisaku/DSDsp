using System;
using System.IO;
using System.Text.Json;

namespace DSDsp
{
    /// <summary>
    /// WebSocket設定
    /// </summary>
    public class WebSocketSettings
    {
        public string ServerIpAddress { get; set; } = "127.0.0.1";
        public int ServerPort { get; set; } = 8080;
        public string ClientId { get; set; } = "DSDsp_001";
        public string OrgCd { get; set; } = "JS";
        public int ReconnectInterval { get; set; } = 5000;
        public int ConnectionTimeout { get; set; } = 30000;
    }

    /// <summary>
    /// ログ設定
    /// </summary>
    public class LogSettings
    {
        public int LogLevel { get; set; } = 3;
        public string LogPath { get; set; } = "./Logs";
    }

    /// <summary>
    /// 表示設定
    /// </summary>
    public class DisplaySettings
    {
        public string ScenarioPath { get; set; } = "./Scenarios";
        public int DefaultScreen { get; set; } = 0;
        /// <summary>
        /// 背景イメージファイルを格納するフォルダパス（予約。現在は未使用）。
        /// Background.Type="Image" のファイルは、プロジェクトの「イメージ」フォルダに
        /// &lt;Resource&gt; として追加すること。pack://application 経由で参照される。
        /// </summary>
        public string ImagePath { get; set; } = "./イメージ";
    }

    /// <summary>
    /// アプリケーション設定
    /// </summary>
    public class AppSettings
    {
        public WebSocketSettings WebSocketSettings { get; set; } = new WebSocketSettings();
        public LogSettings LogSettings { get; set; } = new LogSettings();
        public DisplaySettings DisplaySettings { get; set; } = new DisplaySettings();

        private static AppSettings? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// シングルトンインスタンス
        /// </summary>
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AppSettings();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 設定ファイルから読み込み
        /// </summary>
        /// <param name="filePath">設定ファイルのパス</param>
        /// <returns>読み込みに成功した場合true</returns>
        public static bool Load(string filePath = "DSDsp.json")
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine($"設定ファイルが見つかりません: {filePath}");
                    // デフォルト設定で新規作成
                    Save(filePath);
                    return false;
                }

                string jsonString = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (settings != null)
                {
                    lock (_lock)
                    {
                        _instance = settings;
                    }
                    System.Diagnostics.Debug.WriteLine($"設定ファイル読み込み成功: {filePath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定ファイル読み込みエラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 設定ファイルに保存
        /// </summary>
        /// <param name="filePath">設定ファイルのパス</param>
        /// <returns>保存に成功した場合true</returns>
        public static bool Save(string filePath = "DSDsp.json")
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string jsonString = JsonSerializer.Serialize(Instance, options);
                File.WriteAllText(filePath, jsonString);
                
                System.Diagnostics.Debug.WriteLine($"設定ファイル保存成功: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定ファイル保存エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// WebSocket接続URLを取得
        /// </summary>
        /// <returns>WebSocket接続URL</returns>
        public string GetWebSocketUrl()
        {
            return $"ws://{WebSocketSettings.ServerIpAddress}:{WebSocketSettings.ServerPort}";
        }
    }
}

// Made with Bob
