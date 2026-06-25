using System;
using System.Threading.Tasks;
using DSDsp.Data;
using DSDsp.Handlers;
using DSDsp.Messages;

namespace DSDsp
{
    /// <summary>
    /// DSDspクライアント統合クラス
    /// WebSocket通信、電文処理、データ管理を統合
    /// </summary>
    public class DSDspClient : IDisposable
    {
        private readonly LOG_C _log;
        private readonly WebSocketClient _wsClient;
        private readonly DataManager _dataManager;
        private readonly DPMessageHandler _messageHandler;
        private bool _isDisposed;

        /// <summary>
        /// ログオブジェクト
        /// </summary>
        public LOG_C Log => _log;

        /// <summary>
        /// データマネージャー
        /// </summary>
        public DataManager DataManager => _dataManager;

        /// <summary>
        /// メッセージハンドラー
        /// </summary>
        public DPMessageHandler MessageHandler => _messageHandler;

        /// <summary>
        /// 接続状態
        /// </summary>
        public bool IsConnected => _wsClient.IsConnected;

        // イベント（外部に公開）
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<CompetitionListReceivedEventArgs>? CompetitionListReceived;
        public event EventHandler? DA_MasterReceived;
        public event EventHandler? DS_StatusReceived;
        public event EventHandler? DV_ResultReceived;
        public event EventHandler<ErrorReceivedEventArgs>? ErrorReceived;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public DSDspClient()
        {
            // ログの初期化
            _log = new LOG_C();
            _log.SetLogLevel(AppSettings.Instance.LogSettings.LogLevel);
            _log.CreateFile(AppSettings.Instance.LogSettings.LogPath);

            // データマネージャーの初期化
            _dataManager = new DataManager(_log);

            // WebSocketクライアントの初期化
            _wsClient = new WebSocketClient(_log);
            _wsClient.MessageReceived += OnMessageReceived;
            _wsClient.ConnectionStateChanged += OnConnectionStateChanged;

            // メッセージハンドラーの初期化
            _messageHandler = new DPMessageHandler(_log, _dataManager, _wsClient);
            _messageHandler.CompetitionListReceived += (s, e) => CompetitionListReceived?.Invoke(s, e);
            _messageHandler.DA_MasterReceived += (s, e) => DA_MasterReceived?.Invoke(s, e);
            _messageHandler.DS_StatusReceived += (s, e) => DS_StatusReceived?.Invoke(s, e);
            _messageHandler.DV_ResultReceived += (s, e) => DV_ResultReceived?.Invoke(s, e);
            _messageHandler.ErrorReceived += (s, e) => ErrorReceived?.Invoke(s, e);

            _isDisposed = false;

            _log.LogAdd("DSDspClient初期化完了", _log.INFO);
        }

        /// <summary>
        /// サーバーに接続
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            var settings = AppSettings.Instance.WebSocketSettings;
            var url = AppSettings.Instance.GetWebSocketUrl();
            var uri = new Uri(url);

            _log.LogAdd($"接続開始: {url} (ClientId={settings.ClientId})", _log.INFO);
            return await _wsClient.ConnectAsync(uri);
        }

        /// <summary>
        /// サーバーに接続（URLを指定）
        /// </summary>
        public async Task<bool> ConnectAsync(string url)
        {
            var uri = new Uri(url);
            _log.LogAdd($"接続開始: {url}", _log.INFO);
            return await _wsClient.ConnectAsync(uri);
        }

        /// <summary>
        /// 切断
        /// </summary>
        public async Task DisconnectAsync()
        {
            await _wsClient.DisconnectAsync();
        }

        /// <summary>
        /// 初期化シーケンスを実行
        /// DA_Master → DS_Status の順で取得
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            var settings = AppSettings.Instance.WebSocketSettings;
            
            _log.LogAdd("初期化シーケンス開始", _log.INFO);

            // 1. DA_Master要求
            _log.LogAdd($"DA_Master要求: OrgCd={settings.OrgCd}", _log.INFO);
            bool success = await _messageHandler.RequestDA_MasterAsync(settings.OrgCd);
            
            if (!success)
            {
                _log.LogAdd("DA_Master要求失敗", _log.ERR);
                return false;
            }

            // DA_Master受信を待つ（タイムアウト付き）
            var waitTask = WaitForDA_MasterAsync();
            var timeoutTask = Task.Delay(AppSettings.Instance.WebSocketSettings.ConnectionTimeout);
            var completedTask = await Task.WhenAny(waitTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _log.LogAdd("DA_Master受信タイムアウト", _log.ERR);
                return false;
            }

            // 競技会番号が設定されているか確認
            if (string.IsNullOrEmpty(_dataManager.CmpNo))
            {
                _log.LogAdd("競技会番号が取得できませんでした", _log.ERR);
                return false;
            }

            // 2. DS_Status要求（OrgCdは常に設定ファイルの値を使用）
            _log.LogAdd($"DS_Status要求: OrgCd={settings.OrgCd}, CmpNo={_dataManager.CmpNo}", _log.INFO);
            success = await _messageHandler.RequestDS_StatusAsync(settings.OrgCd, _dataManager.CmpNo);

            if (!success)
            {
                _log.LogAdd("DS_Status要求失敗", _log.ERR);
                return false;
            }

            _log.LogAdd("初期化シーケンス完了", _log.INFO);
            return true;
        }

        /// <summary>
        /// DA_Master受信を待つ
        /// </summary>
        private Task WaitForDA_MasterAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            
            EventHandler? handler = null;
            handler = (s, e) =>
            {
                _messageHandler.DA_MasterReceived -= handler;
                tcs.SetResult(true);
            };
            
            _messageHandler.DA_MasterReceived += handler;
            return tcs.Task;
        }

        /// <summary>
        /// 競技会を選択（複数競技会がある場合）
        /// </summary>
        public async Task<bool> SelectCompetitionAsync(string cmpNo)
        {
            var settings = AppSettings.Instance.WebSocketSettings;
            return await _messageHandler.SelectCompetitionAsync(settings.OrgCd, cmpNo);
        }

        /// <summary>
        /// DV_Resultを要求
        /// </summary>
        public async Task<bool> RequestDV_ResultAsync(string kbnNo, string rndNo)
        {
            var settings = AppSettings.Instance.WebSocketSettings;
            
            if (string.IsNullOrEmpty(_dataManager.CmpNo))
            {
                _log.LogAdd("CmpNoが未設定です", _log.ERR);
                return false;
            }

            return await _messageHandler.RequestDV_ResultAsync(
                settings.OrgCd,
                _dataManager.CmpNo,
                kbnNo,
                rndNo);
        }

        /// <summary>
        /// メッセージ受信イベントハンドラ
        /// </summary>
        private async void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            await _messageHandler.HandleMessageAsync(e.Message);
        }

        /// <summary>
        /// 接続状態変更イベントハンドラ
        /// </summary>
        private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            ConnectionStateChanged?.Invoke(sender, e);
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _log.LogAdd("DSDspClient終了処理開始", _log.INFO);

            _wsClient.MessageReceived -= OnMessageReceived;
            _wsClient.ConnectionStateChanged -= OnConnectionStateChanged;
            _wsClient.Dispose();

            _dataManager.Clear();

            _log.LogAdd("DSDspClient終了処理完了", _log.INFO);
            _isDisposed = true;
        }
    }
}

// Made with Bob
