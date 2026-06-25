using System;
using System.IO;
using System.Windows;

namespace DSDsp
{
    /// <summary>
    /// ログ出力クラス
    /// </summary>
    public class LOG_C
    {
        private string ON_OFF_Flag;
        private string LOG_Path;
        private string LOG_Filename;
        private int LOG_Level;

        // ログレベル定数
        public int ERR = 1;         // エラー
        public int WARNING = 2;     // 警告
        public int INFO = 3;        // 情報
        public int DEBUG = 4;       // デバッグ
        public int DEB_Detail = 5;  // 詳細デバッグ

        // ログ出力イベント（UIに表示する場合に使用）
        public event Action<string>? LogOutput;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public LOG_C()
        {
            LOG_Level = 3; // デフォルトはINFOレベルまで
            ON_OFF_Flag = "OFF";
            LOG_Path = string.Empty;
            LOG_Filename = string.Empty;
        }

        /// <summary>
        /// ログレベルを設定
        /// </summary>
        /// <param name="Level">ログレベル（1:ERR, 2:WARNING, 3:INFO, 4:DEBUG, 5:DEB_Detail）</param>
        public void SetLogLevel(int Level)
        {
            LOG_Level = Level;
        }

        /// <summary>
        /// ログファイルを作成
        /// </summary>
        /// <param name="logPath">ログファイルの保存パス（省略時はカレントディレクトリ）</param>
        /// <returns>作成されたログファイル名</returns>
        public string CreateFile(string? logPath = null)
        {
            ON_OFF_Flag = "ON";
            
            // ログパスの設定
            if (string.IsNullOrEmpty(logPath))
            {
                LOG_Path = Directory.GetCurrentDirectory();
            }
            else
            {
                LOG_Path = logPath;
                // ディレクトリが存在しない場合は作成
                if (!Directory.Exists(LOG_Path))
                {
                    Directory.CreateDirectory(LOG_Path);
                }
            }

            // ログファイル名の生成
            LOG_Filename = Path.Combine(LOG_Path, $"LOG{DateTime.Now:yyyyMMddHHmmss}.log");
            
            // 初期メッセージを書き込み
            LogAdd("=== ログファイル作成 ===", INFO);
            
            return LOG_Filename;
        }

        /// <summary>
        /// ログを追加
        /// </summary>
        /// <param name="cmt">ログメッセージ</param>
        /// <param name="Level">ログレベル</param>
        public void LogAdd(string cmt, int Level)
        {
            if (ON_OFF_Flag != "ON")
            {
                return;
            }

            if (Level > LOG_Level)
            {
                return;
            }

            try
            {
                string levelStr = GetLevelString(Level);
                string logMessage = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} [{levelStr}] {cmt}";
                
                // ファイルに書き込み
                using (StreamWriter writer = new StreamWriter(LOG_Filename, true, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine(logMessage);
                }

                // イベント発火（UI表示用）
                string shortMessage = $"{DateTime.Now:HH:mm:ss} {cmt}";
                LogOutput?.Invoke(shortMessage);

                // デバッグ出力
                System.Diagnostics.Debug.WriteLine(logMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ログ書き込みエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ログレベルを文字列に変換
        /// </summary>
        private string GetLevelString(int level)
        {
            return level switch
            {
                1 => "ERR",
                2 => "WARN",
                3 => "INFO",
                4 => "DEBUG",
                5 => "DETAIL",
                _ => "UNKNOWN"
            };
        }

        /// <summary>
        /// ログ出力を開始
        /// </summary>
        public void Set_ON()
        {
            ON_OFF_Flag = "ON";
        }

        /// <summary>
        /// ログ出力を停止
        /// </summary>
        public void Set_OFF()
        {
            ON_OFF_Flag = "OFF";
        }

        /// <summary>
        /// ログファイルのパスを取得
        /// </summary>
        public string GetLogFilePath()
        {
            return LOG_Filename;
        }
    }
}

// Made with Bob
