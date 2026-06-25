# DSDsp WebSocket通信 使用ガイド

## 概要

DSDspアプリケーションでDSServer_MainとWebSocket通信を行うためのクラス群です。

## 構成ファイル

### 1. DSDsp.json（設定ファイル）

```json
{
  "WebSocketSettings": {
    "ServerIpAddress": "127.0.0.1", // サーバーのIPアドレス
    "ServerPort": 8080, // サーバーのポート番号
    "ClientId": "DSDsp_001", // クライアント識別ID
    "ReconnectInterval": 5000, // 再接続間隔（ミリ秒）
    "ConnectionTimeout": 30000 // 接続タイムアウト（ミリ秒）
  },
  "LogSettings": {
    "LogLevel": 3, // ログレベル（1:ERR, 2:WARN, 3:INFO, 4:DEBUG, 5:DETAIL）
    "LogPath": "./Logs" // ログファイル保存パス
  }
}
```

### 2. LOG_C.cs（ログクラス）

- ログファイルへの出力機能
- ログレベル管理
- UIへのログ出力イベント

### 3. AppSettings.cs（設定管理クラス）

- DSDsp.jsonの読み込み・保存
- シングルトンパターンで設定を管理

### 4. WebSocketClient.cs（WebSocket通信クラス）

- サーバーへの接続・切断
- メッセージ送受信
- イベント駆動型の通信処理

## 使用方法

### 基本的な使い方

```csharp
using DSDsp;

public partial class MainWindow : Window
{
    private LOG_C? _log;
    private WebSocketClient? _wsClient;

    public MainWindow()
    {
        InitializeComponent();
        InitializeWebSocket();
    }

    private void InitializeWebSocket()
    {
        // 1. 設定ファイルの読み込み
        AppSettings.Load("DSDsp.json");

        // 2. ログの初期化
        _log = new LOG_C();
        _log.SetLogLevel(AppSettings.Instance.LogSettings.LogLevel);
        _log.CreateFile(AppSettings.Instance.LogSettings.LogPath);

        // ログ出力イベントの購読（オプション）
        _log.LogOutput += (message) =>
        {
            // UIにログを表示する場合
            Dispatcher.Invoke(() =>
            {
                // txtLog.AppendText(message + Environment.NewLine);
            });
        };

        // 3. WebSocketクライアントの初期化
        _wsClient = new WebSocketClient(_log);

        // メッセージ受信イベントの購読
        _wsClient.MessageReceived += OnMessageReceived;

        // 接続状態変更イベントの購読
        _wsClient.ConnectionStateChanged += OnConnectionStateChanged;
    }

    // サーバーに接続
    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_wsClient == null) return;

        string url = AppSettings.Instance.GetWebSocketUrl();
        Uri uri = new Uri(url);

        bool success = await _wsClient.ConnectAsync(uri);

        if (success)
        {
            MessageBox.Show("接続成功", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("接続失敗", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // メッセージ送信
    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (_wsClient == null || !_wsClient.IsConnected) return;

        string message = "テストメッセージ";
        await _wsClient.SendMessageAsync(message);
    }

    // メッセージ受信イベントハンドラ
    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        // UIスレッドで実行されるため、Dispatcher.Invokeは不要
        _log?.LogAdd($"メッセージ受信処理: {e.Message}", _log.INFO);

        // 受信したメッセージの処理
        // TODO: 電文の種類に応じた処理を実装
    }

    // 接続状態変更イベントハンドラ
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (e.IsConnected)
        {
            _log?.LogAdd("接続状態: 接続中", _log.INFO);
        }
        else
        {
            _log?.LogAdd($"接続状態: 切断 ({e.Message})", _log.WARNING);
        }
    }

    // 切断
    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_wsClient != null)
        {
            await _wsClient.DisconnectAsync();
        }
    }

    // ウィンドウクローズ時の処理
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _wsClient?.Dispose();
        base.OnClosing(e);
    }
}
```

## 複数起動時の設定

同じPCで複数のDSDspアプリを起動する場合、各インスタンスで異なる設定ファイルを使用します。

### 方法1: コマンドライン引数で設定ファイルを指定

```csharp
// App.xaml.cs
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    string configFile = "DSDsp.json";

    // コマンドライン引数から設定ファイル名を取得
    if (e.Args.Length > 0)
    {
        configFile = e.Args[0];
    }

    AppSettings.Load(configFile);
}
```

起動例:

```
DSDsp.exe DSDsp_001.json
DSDsp.exe DSDsp_002.json
```

### 方法2: 環境変数で識別

```csharp
// App.xaml.cs
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    string instanceId = Environment.GetEnvironmentVariable("DSDSP_INSTANCE") ?? "001";
    string configFile = $"DSDsp_{instanceId}.json";

    AppSettings.Load(configFile);
}
```

### 設定ファイル例

**DSDsp_001.json**

```json
{
  "WebSocketSettings": {
    "ServerIpAddress": "127.0.0.1",
    "ServerPort": 8080,
    "ClientId": "DSDsp_001"
  }
}
```

**DSDsp_002.json**

```json
{
  "WebSocketSettings": {
    "ServerIpAddress": "127.0.0.1",
    "ServerPort": 8080,
    "ClientId": "DSDsp_002"
  }
}
```

## ログレベル

| レベル     | 値  | 説明             |
| ---------- | --- | ---------------- |
| ERR        | 1   | エラーのみ       |
| WARNING    | 2   | 警告以上         |
| INFO       | 3   | 情報以上（推奨） |
| DEBUG      | 4   | デバッグ情報     |
| DEB_Detail | 5   | 詳細デバッグ情報 |

## 注意事項

1. **設定ファイルの配置**: DSDsp.jsonは実行ファイルと同じディレクトリに配置してください
2. **ログディレクトリ**: LogPathで指定したディレクトリが存在しない場合は自動作成されます
3. **接続確認**: 接続前に`IsConnected`プロパティで接続状態を確認してください
4. **リソース解放**: アプリケーション終了時は必ず`Dispose()`を呼び出してください
5. **UIスレッド**: メッセージ受信イベントは自動的にUIスレッドで実行されます

## トラブルシューティング

### 接続できない場合

- サーバーのIPアドレスとポート番号を確認
- ファイアウォールの設定を確認
- サーバーが起動しているか確認

### ログが出力されない場合

- `CreateFile()`が呼ばれているか確認
- `Set_ON()`が呼ばれているか確認
- ログレベルが適切か確認

### 複数起動時に競合する場合

- 各インスタンスで異なるClientIdを設定
- 異なる設定ファイルを使用
