# DSDspClient 使用例

## 概要

DSDspClientは、WebSocket通信、電文処理、データ管理を統合した高レベルAPIです。

## 基本的な使い方

### 1. 初期化と接続

```csharp
using DSDsp;
using System;
using System.Windows;

public partial class MainWindow : Window
{
    private DSDspClient? _client;

    public MainWindow()
    {
        InitializeComponent();
        InitializeClient();
    }

    private async void InitializeClient()
    {
        // 設定ファイルの読み込み
        AppSettings.Load("DSDsp.json");

        // クライアントの作成
        _client = new DSDspClient();

        // イベントの購読
        _client.ConnectionStateChanged += OnConnectionStateChanged;
        _client.DA_MasterReceived += OnDA_MasterReceived;
        _client.DS_StatusReceived += OnDS_StatusReceived;
        _client.DV_ResultReceived += OnDV_ResultReceived;
        _client.CompetitionListReceived += OnCompetitionListReceived;
        _client.ErrorReceived += OnErrorReceived;

        // データ更新イベントの購読
        _client.DataManager.DA_MasterUpdated += OnDataUpdated;
        _client.DataManager.DS_StatusUpdated += OnDataUpdated;
        _client.DataManager.DV_ResultUpdated += OnDataUpdated;

        // 接続
        bool connected = await _client.ConnectAsync();
        if (connected)
        {
            // 初期化シーケンス実行（DA_Master → DS_Status）
            bool initialized = await _client.InitializeAsync();
            if (initialized)
            {
                MessageBox.Show("初期化完了", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    // 接続状態変更
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (e.IsConnected)
        {
            _client?.Log.LogAdd("接続しました", _client.Log.INFO);
            // UI更新: 接続状態表示など
        }
        else
        {
            _client?.Log.LogAdd($"切断しました: {e.Message}", _client.Log.WARNING);
            // UI更新: 切断状態表示など
        }
    }

    // DA_Master受信
    private void OnDA_MasterReceived(object? sender, EventArgs e)
    {
        if (_client?.DataManager.DA_Master == null) return;

        // DA_Masterからデータを取得
        var daMaster = _client.DataManager.DA_Master;
        var cmpName = daMaster["DA_CompName"]?.ToString() ?? "";

        _client.Log.LogAdd($"競技会名: {cmpName}", _client.Log.INFO);

        // UI更新: 競技会情報表示など
    }

    // DS_Status受信
    private void OnDS_StatusReceived(object? sender, EventArgs e)
    {
        if (_client?.DataManager.DS_Status == null) return;

        // DS_Statusからデータを取得
        var dsStatus = _client.DataManager.DS_Status;
        var version = _client.DataManager.DS_StatusVersion;

        _client.Log.LogAdd($"DS_Status受信: Version={version}", _client.Log.INFO);

        // UI更新: 進行状況表示など
        UpdateProgressDisplay();
    }

    // DV_Result受信
    private void OnDV_ResultReceived(object? sender, EventArgs e)
    {
        if (_client?.DataManager.DV_Result == null) return;

        // DV_Resultからデータを取得
        var dvResult = _client.DataManager.DV_Result;

        _client.Log.LogAdd("DV_Result受信", _client.Log.INFO);

        // UI更新: 採点結果表示など
        UpdateResultDisplay();
    }

    // 競技会リスト受信（複数競技会がある場合）
    private void OnCompetitionListReceived(object? sender, CompetitionListReceivedEventArgs e)
    {
        _client?.Log.LogAdd($"競技会リスト受信: {e.Competitions.Count}件", _client.Log.INFO);

        // 競技会選択ダイアログを表示
        var dialog = new CompetitionSelectionDialog(e.Competitions);
        if (dialog.ShowDialog() == true)
        {
            var selectedCmpNo = dialog.SelectedCmpNo;
            _ = _client?.SelectCompetitionAsync(selectedCmpNo);
        }
    }

    // エラー受信
    private void OnErrorReceived(object? sender, ErrorReceivedEventArgs e)
    {
        _client?.Log.LogAdd($"エラー [{e.Command}]: {e.ErrorMessage}", _client.Log.ERR);
        MessageBox.Show($"エラー: {e.ErrorMessage}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    // データ更新（差分更新含む）
    private void OnDataUpdated(object? sender, EventArgs e)
    {
        // DS_Statusの差分更新時もこのイベントが発火
        UpdateProgressDisplay();
    }

    // 進行状況表示を更新
    private void UpdateProgressDisplay()
    {
        if (_client?.DataManager.DS_Status == null) return;

        var dsStatus = _client.DataManager.DS_Status;

        // フロアAの現在進行を取得
        var floors = dsStatus["DS_Floors"]?.AsArray();
        if (floors != null && floors.Count > 0)
        {
            var floorA = floors[0];
            var curPrgNo = floorA?["DS_CurPrgNo"]?.ToString() ?? "0";

            // UI更新
            Dispatcher.Invoke(() =>
            {
                // txtCurrentProgress.Text = $"進行番号: {curPrgNo}";
            });
        }
    }

    // 採点結果表示を更新
    private void UpdateResultDisplay()
    {
        if (_client?.DataManager.DV_Result == null) return;

        // DV_Resultから必要なデータを取得してUI更新
        // ...
    }

    // DV_Result要求ボタン
    private async void RequestDVResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (_client == null) return;

        string kbnNo = "01";  // 区分番号
        string rndNo = "010"; // ラウンド番号

        bool success = await _client.RequestDV_ResultAsync(kbnNo, rndNo);
        if (!success)
        {
            MessageBox.Show("DV_Result要求失敗", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ウィンドウクローズ時
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _client?.Dispose();
        base.OnClosing(e);
    }
}
```

## 2. データアクセス

### DA_Masterからデータを取得

```csharp
private void GetCompetitionInfo()
{
    if (_client?.DataManager.DA_Master == null) return;

    var daMaster = _client.DataManager.DA_Master;

    // 競技会情報
    var orgCd = daMaster["DA_OrgCD"]?.ToString();
    var cmpNo = daMaster["DA_CompNo"]?.ToString();
    var cmpName = daMaster["DA_CompName"]?.ToString();

    // 区分リスト
    var kubuns = daMaster["DA_Kubuns"]?.AsArray();
    if (kubuns != null)
    {
        foreach (var kubun in kubuns)
        {
            var kbnNo = kubun?["DA_KbnNo"]?.ToString();
            var kbnName = kubun?["DA_KbnName"]?.ToString();
            // ...
        }
    }
}
```

### DS_Statusからデータを取得

```csharp
private void GetProgressInfo()
{
    if (_client?.DataManager.DS_Status == null) return;

    var dsStatus = _client.DataManager.DS_Status;

    // バージョン番号
    var version = _client.DataManager.DS_StatusVersion;

    // フロア情報
    var floors = dsStatus["DS_Floors"]?.AsArray();
    if (floors != null && floors.Count > 0)
    {
        var floorA = floors[0];
        var curPrgNo = floorA?["DS_CurPrgNo"]?.ToString();

        // 進行リスト
        var progresses = floorA?["DS_PRGRSs"]?.AsArray();
        if (progresses != null)
        {
            foreach (var progress in progresses)
            {
                var prgNo = progress?["DS_PrgNo"]?.ToString();
                var prgSts = progress?["DS_PrgSts"]?.ToString();
                var curDanNo = progress?["DS_CurDanNo"]?.ToString();
                var curHeat = progress?["DS_CurHeat"]?.ToString();
                // ...
            }
        }
    }
}
```

### DV_Resultからデータを取得

```csharp
private void GetResultInfo()
{
    if (_client?.DataManager.DV_Result == null) return;

    var dvResult = _client.DataManager.DV_Result;

    // 採点結果データの取得
    // （DV_Resultの構造に応じて実装）
}
```

## 3. 複数起動対応

### 方法1: 異なる設定ファイルを使用

**DSDsp_001.json**

```json
{
  "WebSocketSettings": {
    "ServerIpAddress": "127.0.0.1",
    "ServerPort": 8080,
    "ClientId": "DSDsp_001",
    "OrgCd": "JS"
  }
}
```

**DSDsp_002.json**

```json
{
  "WebSocketSettings": {
    "ServerIpAddress": "127.0.0.1",
    "ServerPort": 8080,
    "ClientId": "DSDsp_002",
    "OrgCd": "JS"
  }
}
```

**起動時に設定ファイルを指定**

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

**起動例**

```
DSDsp.exe DSDsp_001.json
DSDsp.exe DSDsp_002.json
```

### 方法2: 環境変数を使用

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

## 4. エラーハンドリング

```csharp
private void OnErrorReceived(object? sender, ErrorReceivedEventArgs e)
{
    switch (e.Command)
    {
        case "DP_ANS_DA_NG":
            MessageBox.Show("DA_Master取得エラー", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            break;

        case "DP_ANS_DS_NG":
            MessageBox.Show("DS_Status取得エラー", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            break;

        case "DP_ANS_DV_RESULT_NG":
            MessageBox.Show("DV_Result取得エラー", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            break;

        default:
            MessageBox.Show($"エラー: {e.ErrorMessage}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            break;
    }
}
```

## 5. ログ出力

```csharp
// ログレベルの設定
_client.Log.SetLogLevel(_client.Log.DEBUG);

// ログ出力
_client.Log.LogAdd("メッセージ", _client.Log.INFO);

// ログ出力イベントの購読（UI表示用）
_client.Log.LogOutput += (message) =>
{
    Dispatcher.Invoke(() =>
    {
        txtLog.AppendText(message + Environment.NewLine);
        txtLog.ScrollToEnd();
    });
};
```

## 6. 再接続処理

```csharp
private async void ReconnectButton_Click(object sender, RoutedEventArgs e)
{
    if (_client == null) return;

    // 切断
    await _client.DisconnectAsync();

    // 少し待つ
    await Task.Delay(1000);

    // 再接続
    bool connected = await _client.ConnectAsync();
    if (connected)
    {
        // 初期化
        await _client.InitializeAsync();
    }
}
```

## まとめ

DSDspClientを使用することで、以下が簡単に実装できます:

- WebSocket接続管理
- 電文の送受信
- DA_Master、DS_Status、DV_Resultの管理
- DS_Statusの差分更新
- イベント駆動型のデータ更新通知
- 複数インスタンスの起動

詳細な電文仕様は「DSDsp電文仕様書.md」を参照してください。
