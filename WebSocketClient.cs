using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DSDsp
{
    /// <summary>
    /// WebSocketメッセージ受信イベント引数
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        public string Message { get; set; }
        public DateTime ReceivedTime { get; set; }

        public MessageReceivedEventArgs(string message)
        {
            Message = message;
            ReceivedTime = DateTime.Now;
        }
    }

    /// <summary>
    /// WebSocket接続状態変更イベント引数
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string? Message { get; set; }

        public ConnectionStateChangedEventArgs(bool isConnected, string? message = null)
        {
            IsConnected = isConnected;
            Message = message;
        }
    }

    /// <summary>
    /// WebSocketクライアント
    /// </summary>
    public class WebSocketClient : IDisposable
    {
        private ClientWebSocket? _client;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _receiveTask;
        private readonly LOG_C _log;
        private bool _isDisposed;
        private bool _isConnected;

        // イベント
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        /// <summary>
        /// 接続状態
        /// </summary>
        public bool IsConnected => _isConnected && _client?.State == WebSocketState.Open;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="log">ログオブジェクト</param>
        public WebSocketClient(LOG_C log)
        {
            _log = log;
            _isDisposed = false;
            _isConnected = false;
        }

        /// <summary>
        /// サーバーに接続
        /// </summary>
        /// <param name="uri">接続先URI</param>
        /// <returns>接続成功時true</returns>
        public async Task<bool> ConnectAsync(Uri uri)
        {
            try
            {
                if (_isConnected)
                {
                    _log.LogAdd("既に接続されています", _log.WARNING);
                    return true;
                }

                _client = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                _log.LogAdd($"接続開始: {uri}", _log.INFO);
                
                await _client.ConnectAsync(uri, _cancellationTokenSource.Token);
                
                _isConnected = true;
                _log.LogAdd($"接続成功: {uri}", _log.INFO);
                
                // 接続状態変更イベント発火
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(true, "接続成功"));

                // 受信タスク開始
                _receiveTask = Task.Run(ReceiveLoop);

                return true;
            }
            catch (Exception ex)
            {
                _log.LogAdd($"接続エラー: {ex.Message}", _log.ERR);
                _isConnected = false;
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, $"接続エラー: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// メッセージを送信
        /// </summary>
        /// <param name="message">送信メッセージ</param>
        /// <returns>送信成功時true</returns>
        public async Task<bool> SendMessageAsync(string message)
        {
            try
            {
                if (_client == null || !IsConnected)
                {
                    _log.LogAdd("未接続のため送信できません", _log.WARNING);
                    return false;
                }

                byte[] buffer = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(buffer);
                
                await _client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                
                _log.LogAdd($"電文送信: {message}", _log.INFO);
                return true;
            }
            catch (Exception ex)
            {
                _log.LogAdd($"送信エラー: {ex.Message}", _log.ERR);
                return false;
            }
        }

        /// <summary>
        /// メッセージ受信ループ
        /// </summary>
        private async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            var messageBuilder = new System.Text.StringBuilder();

            try
            {
                while (_client != null && IsConnected && !_cancellationTokenSource!.Token.IsCancellationRequested)
                {
                    messageBuilder.Clear();
                    WebSocketReceiveResult result;
                    
                    // メッセージが完全に受信されるまでループ
                    do
                    {
                        var segment = new ArraySegment<byte>(buffer);
                        result = await _client.ReceiveAsync(segment, _cancellationTokenSource.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _log.LogAdd("サーバーから切断されました", _log.INFO);
                            await DisconnectAsync();
                            return;
                        }

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            messageBuilder.Append(chunk);
                        }
                    }
                    while (!result.EndOfMessage);

                    // 完全なメッセージを取得
                    if (messageBuilder.Length > 0)
                    {
                        string message = messageBuilder.ToString();
                        _log.LogAdd($"電文受信: {message.Substring(0, Math.Min(200, message.Length))}...", _log.INFO);

                        // メッセージ受信イベント発火（UIスレッドで実行）
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _log.LogAdd("受信ループがキャンセルされました", _log.INFO);
            }
            catch (Exception ex)
            {
                _log.LogAdd($"受信エラー: {ex.Message}", _log.ERR);
                _isConnected = false;
                
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, $"受信エラー: {ex.Message}"));
                });
            }
        }

        /// <summary>
        /// 切断
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (_client != null && IsConnected)
                {
                    _log.LogAdd("切断開始", _log.INFO);
                    
                    _cancellationTokenSource?.Cancel();
                    
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "正常終了", CancellationToken.None);
                    
                    _isConnected = false;
                    _log.LogAdd("切断完了", _log.INFO);
                    
                    ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, "切断完了"));
                }
            }
            catch (Exception ex)
            {
                _log.LogAdd($"切断エラー: {ex.Message}", _log.ERR);
                _isConnected = false;
            }
            finally
            {
                _client?.Dispose();
                _client = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            DisconnectAsync().Wait();
            _isDisposed = true;
        }
    }
}

// Made with Bob
