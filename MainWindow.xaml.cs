using DSDsp.Data;
using DSDsp.Scenario;
using DSDsp.画面;
using Microsoft.Win32;
using System.IO;

namespace DSDsp
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private DisplayWindow? _offScreenWindow;   // 実コンテンツ保持（画面外・常時非表示）
        private DisplayWindow? _displayWindow;     // モニター用ミラーウィンドウ
        private DisplayWindow? _fullScreenWindow;  // スクリーン用ミラーウィンドウ
        private DSDspClient? _client;
        private ScenarioManager? _scenarioManager;
        private LOG_C? _log;

        // 現在のシナリオ
        private ProgressScenario? _currentProgressScenario;
        private AjsScenarioDefinition? _currentAjsScenario;   // AJS: 新モデル
        private ScreenScenario? _currentAwardScenario;

        // AJS画面進行一覧（BuildProgressList で動的生成）
        private List<AjsProgressItem>? _currentAjsProgressItems;

        // AJS SUB画面進行一覧
        private List<AjsProgressItem>? _currentAjsSubProgressItems;

        // 現在の選択
        private int _currentProgressIndex = -1;
        private int _currentAjsIndex = -1;
        private int _currentAjsSubIndex = -1;     // SUB画面進行の選択インデックス
        private int _currentAwardIndex = -1;
        private int _selectedScreenIndex = -1;   // コンボボックスで選択されているスクリーン番号
        private int _activeScreenIndex = -1;     // 現在全画面表示中のスクリーン番号（-1=非表示）
        private bool _isTestDisplayActive = false;  // テスト表示が有効かどうか
        private bool _isManualDisconnect = false;   // 手動切断フラグ（true の場合は自動再接続しない）

        // AJS区分情報（キー: 表示テキスト, 値: "区分No-ラウンドNo"）
        private Dictionary<string, string> _ajsCategoryKeys = new Dictionary<string, string>();

        // プログラムによる LstAjsProgress.SelectedIndex 変更時に SelectionChanged を無視するフラグ
        private bool _suppressAjsSelectionChanged = false;

        // プログラムによる LstAjsSubProgress.SelectedIndex 変更時に SelectionChanged を無視するフラグ
        private bool _suppressAjsSubSelectionChanged = false;

        // テスト用データマネージャー（サーバー未接続時にJSONファイルから直接投入）
        private DataManager? _testDataManager;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 設定ファイルの読み込み
            AppSettings.Load("DSDsp.json");

            // ログの初期化
            _log = new LOG_C();
            _log.SetLogLevel(AppSettings.Instance.LogSettings.LogLevel);
            _log.CreateFile(AppSettings.Instance.LogSettings.LogPath);
            _log.LogAdd("DSDsp起動", _log.INFO);

            // バージョン情報をヘッダーに表示
            // Version形式: 1.yy.MMdd.HHmm  例: 1.25.720.1432 → "v1.25 build 2025-07-20 14:32"
            var ver = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            if (ver != null && TxtVersion != null)
            {
                // ver.Build = MMdd (例: 720 = 07/20), ver.Revision = HHmm (例: 1432)
                int year  = 2000 + ver.Minor;
                int month = ver.Build / 100;
                int day   = ver.Build % 100;
                int hour  = ver.Revision / 100;
                int min   = ver.Revision % 100;
                TxtVersion.Text = $"v{ver.Major}.{ver.Minor} build {year}-{month:D2}-{day:D2} {hour:D2}:{min:D2}";
            }

            // シナリオマネージャーの初期化
            var scenarioPath = AppSettings.Instance.DisplaySettings.ScenarioPath;
            _scenarioManager = new ScenarioManager(_log, scenarioPath);

            // サーバー情報を表示
            var settings = AppSettings.Instance.WebSocketSettings;
            TxtServerInfo.Text = $"{settings.ServerIpAddress}:{settings.ServerPort}";

            // コントロール画面を左上に配置
            this.Left = 0;
            this.Top = 0;

            // 表示用ウィンドウを作成（モニター用）
            CreateDisplayWindow();

            // シナリオファイルを読み込み
            LoadScenarios();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _log?.LogAdd("DSDsp終了", _log.INFO);
            _client?.Dispose();
            _fullScreenWindow?.Close();
            _displayWindow?.Close();
            _offScreenWindow?.Close();
        }

        /// <summary>
        /// オフスクリーンウィンドウを初回のみ作成する。
        /// 実コンテンツはここに表示される。画面外に置き Hidden のまま維持する。
        /// モニターとスクリーンは両方ともこの ContentGrid を VisualBrush でミラーする。
        /// </summary>
        private void EnsureOffScreenWindowCreated()
        {
            if (_offScreenWindow != null) return;

            _offScreenWindow = new DisplayWindow();
            _offScreenWindow.Title = "オフスクリーン（内部用）";
            _offScreenWindow.WindowStyle = WindowStyle.None;
            _offScreenWindow.ResizeMode  = ResizeMode.NoResize;
            _offScreenWindow.ShowInTaskbar = false;
            // 画面外の座標に配置したまま Visible で Show() し続ける。
            // Hidden にすると WPF のレンダリングが停止して VisualBrush が古い状態を映すため。
            _offScreenWindow.Left = -10000;
            _offScreenWindow.Top  = -10000;
            _offScreenWindow.Width  = 641;
            _offScreenWindow.Height = 387;

            _offScreenWindow.Show();   // Visible のまま維持

            // 既存のミラーウィンドウにソースを設定（メイン＋SUBを含む LayeredContentGrid をミラー）
            _displayWindow?.SetMirrorSource(_offScreenWindow.LayeredContentGrid);
            _fullScreenWindow?.SetMirrorSource(_offScreenWindow.LayeredContentGrid);

            // 現在のシナリオ背景を適用
            ApplyScenarioBackground(_currentAjsScenario?.Background);

            _log?.LogAdd("オフスクリーンウィンドウを作成（画面外 Visible）", _log.INFO);
        }

        /// <summary>
        /// モニター用ミラーウィンドウを作成する。
        /// </summary>
        private void CreateDisplayWindow()
        {
            if (_displayWindow != null)
            {
                if (!_displayWindow.IsVisible)
                    _displayWindow.Show();
                return;
            }

            _displayWindow = new DisplayWindow();
            _displayWindow.Left = this.Left + this.Width + 10;
            _displayWindow.Top  = this.Top;
            _displayWindow.Closed += DisplayWindow_Closed;
            _displayWindow.Show();

            // オフスクリーンウィンドウが既にあればミラーソースを設定
            if (_offScreenWindow != null)
                _displayWindow.SetMirrorSource(_offScreenWindow.LayeredContentGrid);

            _log?.LogAdd("モニター用ミラーウィンドウを作成", _log.INFO);
        }

        /// <summary>
        /// モニター用ウィンドウが閉じられた時の処理
        /// </summary>
        private void DisplayWindow_Closed(object? sender, EventArgs e)
        {
            if (_displayWindow != null)
            {
                _displayWindow.Closed -= DisplayWindow_Closed;
                _displayWindow = null;
                _log?.LogAdd("モニター用ウィンドウが閉じられました", _log.INFO);
            }
        }

        /// <summary>
        /// 全画面ウィンドウが外部から閉じられた時の処理
        /// </summary>
        private void FullScreenWindow_Closed(object? sender, EventArgs e)
        {
            if (_fullScreenWindow != null)
            {
                _fullScreenWindow.Closed -= FullScreenWindow_Closed;
                _fullScreenWindow = null;
                _activeScreenIndex = -1;
                UpdateToggleDisplayButton(false);
                _log?.LogAdd("全画面ウィンドウが閉じられました", _log.INFO);
            }
        }

        #region シナリオ読み込み

        /// <summary>
        /// シナリオファイルを読み込み
        /// </summary>
        private void LoadScenarios()
        {
            if (_scenarioManager == null) return;

            // AJSシナリオ
            var ajsFiles = _scenarioManager.GetScenarioFiles(ScenarioType.AJS);
            CmbAjsScenario.ItemsSource = ajsFiles;
            if (ajsFiles.Count > 0)
                CmbAjsScenario.SelectedIndex = 0;
        }

        #endregion

        #region 共通コントロール

        /// <summary>
        /// 接続ボタンクリック
        /// </summary>
        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_client != null && _client.IsConnected)
            {
                await DisconnectAsync();
            }
            else
            {
                await ConnectAsync();
            }
        }

        /// <summary>
        /// サーバーに接続
        /// </summary>
        private async System.Threading.Tasks.Task ConnectAsync()
        {
            try
            {
                _isManualDisconnect = false;
                UpdateConnectionStatus("接続中...", Brushes.Orange);
                BtnConnect.IsEnabled = false;

                _client = new DSDspClient();
                _client.ConnectionStateChanged += OnConnectionStateChanged;
                _client.DA_MasterReceived += OnDA_MasterReceived;
                _client.DS_StatusReceived += OnDS_StatusReceived;
                _client.DV_ResultReceived += OnDV_ResultReceived;
                _client.ErrorReceived += OnErrorReceived;
                _client.HeatEndNotifyReceived += OnHeatEndNotifyReceived;
                _client.CompetitionSelector = OnSelectCompetitionAsync;

                bool connected = await _client.ConnectAsync();
                
                if (connected)
                {
                    bool initialized = await _client.InitializeAsync();
                    
                    if (initialized)
                    {
                        UpdateConnectionStatus("接続済み", Brushes.LimeGreen);
                        BtnConnect.Content = "切断";
                        _log?.LogAdd("サーバー接続成功", _log.INFO);
                    }
                    else
                    {
                        UpdateConnectionStatus("初期化失敗", Brushes.Red);
                        await DisconnectAsync();
                        MessageBox.Show("初期化に失敗しました", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    UpdateConnectionStatus("接続失敗", Brushes.Red);
                    _client?.Dispose();
                    _client = null;
                    MessageBox.Show("サーバーに接続できませんでした", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateConnectionStatus("エラー", Brushes.Red);
                _client?.Dispose();
                _client = null;
                _log?.LogAdd($"接続エラー: {ex.Message}", _log.ERR);
                MessageBox.Show($"接続エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnConnect.IsEnabled = true;
            }
        }

        /// <summary>
        /// サーバーから切断
        /// </summary>
        private async System.Threading.Tasks.Task DisconnectAsync()
        {
            try
            {
                _isManualDisconnect = true;
                UpdateConnectionStatus("切断中...", Brushes.Orange);
                BtnConnect.IsEnabled = false;

                if (_client != null)
                {
                    await _client.DisconnectAsync();
                    _client.Dispose();
                    _client = null;
                }

                UpdateConnectionStatus("未接続", Brushes.Gray);
                BtnConnect.Content = "サーバー接続";
                _log?.LogAdd("サーバー切断", _log.INFO);
            }
            finally
            {
                BtnConnect.IsEnabled = true;
            }
        }

        /// <summary>
        /// 接続状態表示を更新
        /// </summary>
        private void UpdateConnectionStatus(string status, Brush color)
        {
            TxtConnectionStatus.Text = status;
            StatusIndicator.Fill = color;
        }

        /// <summary>
        /// スクリーン選択変更
        /// </summary>
        private void CmbScreenSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbScreenSelect.SelectedIndex >= 0)
            {
                _selectedScreenIndex = CmbScreenSelect.SelectedIndex;
                _log?.LogAdd($"スクリーン{_selectedScreenIndex + 1}を選択", _log.INFO);
            }
        }

        /// <summary>
        /// 全画面ミラーウィンドウを初回のみ作成する（常時保持・Hidden で待機）。
        /// </summary>
        private void EnsureFullScreenWindowCreated()
        {
            if (_fullScreenWindow != null) return;

            _fullScreenWindow = new DisplayWindow();
            _fullScreenWindow.Title = "表示ウィンドウ（スクリーン）";
            _fullScreenWindow.Closed += FullScreenWindow_Closed;
            _fullScreenWindow.WindowStyle = WindowStyle.None;
            _fullScreenWindow.ResizeMode  = ResizeMode.NoResize;
            _fullScreenWindow.WindowState = WindowState.Normal;
            _fullScreenWindow.Topmost = true;
            _fullScreenWindow.Show();
            _fullScreenWindow.Visibility = Visibility.Hidden;

            // オフスクリーンウィンドウが既にあればミラーソースを設定
            if (_offScreenWindow != null)
                _fullScreenWindow.SetMirrorSource(_offScreenWindow.LayeredContentGrid);

            _log?.LogAdd("全画面ミラーウィンドウを作成", _log.INFO);
        }

        /// <summary>
        /// 全画面ウィンドウを指定されたスクリーンに配置して表示する。
        /// </summary>
        private void PositionDisplayWindow(int screenIndex)
        {
            var screens = WinForms.Screen.AllScreens;
            _log?.LogAdd($"利用可能なスクリーン数: {screens.Length}", _log.INFO);

            if (screenIndex >= screens.Length) return;

            // オフスクリーンを先に確保（全画面のミラーソース設定に必要）
            EnsureOffScreenWindowCreated();
            // 全画面ミラーウィンドウを確保
            EnsureFullScreenWindowCreated();
            if (_fullScreenWindow == null) return;

            var screen = screens[screenIndex];
            _log?.LogAdd($"スクリーン{screenIndex + 1}（物理ピクセル）: Left={screen.Bounds.Left}, Top={screen.Bounds.Top}, Width={screen.Bounds.Width}, Height={screen.Bounds.Height}", _log.INFO);

            // DPIスケールを取得
            var presSource = PresentationSource.FromVisual(this);
            double dpiScaleX = presSource?.CompositionTarget.TransformToDevice.M11 ?? 1.0;
            double dpiScaleY = presSource?.CompositionTarget.TransformToDevice.M22 ?? 1.0;
            _log?.LogAdd($"DPIスケール: X={dpiScaleX}, Y={dpiScaleY}", _log.INFO);

            // 物理ピクセル → WPF論理ピクセルに変換してスクリーンに配置
            _fullScreenWindow.Left   = screen.Bounds.Left   / dpiScaleX;
            _fullScreenWindow.Top    = screen.Bounds.Top    / dpiScaleY;
            _fullScreenWindow.Width  = screen.Bounds.Width  / dpiScaleX;
            _fullScreenWindow.Height = screen.Bounds.Height / dpiScaleY;

            // 表彰式タブのクロマキモードが有効な場合、全画面ウィンドウの背景も合わせる
            bool awardIsChroma = (_awardDisplay == AwardDisplayMode.ChromaList)
                              || (_awardDisplay == AwardDisplayMode.ChromaIndividual);
            if (awardIsChroma)
            {
                try
                {
                    var colorStr = AppSettings.Instance.ChromaKeySettings.BackgroundColor;
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                    _fullScreenWindow.SetBackgroundColor(color.R, color.G, color.B);
                }
                catch { _fullScreenWindow.ClearBackground(); }
            }

            _fullScreenWindow.Visibility = Visibility.Visible;
            _activeScreenIndex = screenIndex;
            UpdateToggleDisplayButton(true);

            _log?.LogAdd($"スクリーン{screenIndex + 1}に全画面表示: Left={_fullScreenWindow.Left}, Top={_fullScreenWindow.Top}", _log.INFO);
        }

        /// <summary>
        /// 表示/非表示ボタンの見た目を更新
        /// </summary>
        private void UpdateToggleDisplayButton(bool isVisible)
        {
            if (isVisible)
            {
                BtnToggleDisplay.Content = "👁 表示中";
                BtnToggleDisplay.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Colors.Green);
            }
            else
            {
                BtnToggleDisplay.Content = "🚫 非表示";
                BtnToggleDisplay.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF9800"));
            }
        }

        /// <summary>
        /// 表示/非表示切り替え（全画面ウィンドウのみ）
        /// </summary>
        private void BtnToggleDisplay_Click(object sender, RoutedEventArgs e)
        {
            if (_fullScreenWindow != null && _fullScreenWindow.IsVisible)
            {
                // 表示中 → 非表示
                _fullScreenWindow.Visibility = Visibility.Hidden;
                _activeScreenIndex = -1;
                UpdateToggleDisplayButton(false);
                _log?.LogAdd("全画面表示を非表示", _log.INFO);
            }
            else
            {
                // 非表示 or 未作成 → 選択されたスクリーンに表示
                if (_selectedScreenIndex < 0)
                {
                    _log?.LogAdd("スクリーンが選択されていません", _log.WARNING);
                    MessageBox.Show("スクリーンを選択してください", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // スクリーンが変わっている or ウィンドウ未作成 → 配置（初回のみ作成）
                if (_fullScreenWindow == null || _activeScreenIndex != _selectedScreenIndex)
                {
                    PositionDisplayWindow(_selectedScreenIndex);
                }
                else
                {
                    // 同じスクリーン・ウィンドウ既存 → Visible に戻すだけ
                    _fullScreenWindow.Visibility = Visibility.Visible;
                    UpdateToggleDisplayButton(true);
                    _log?.LogAdd($"スクリーン{_selectedScreenIndex + 1}に全画面表示を再表示", _log.INFO);
                }
            }
        }

        /// <summary>
        /// テスト表示ボタンクリック（トグル式）
        /// </summary>
        private void BtnTestDisplay_Click(object sender, RoutedEventArgs e)
        {
            EnsureOffScreenWindowCreated();

            if (_isTestDisplayActive)
            {
                // テスト表示中の場合は、画面をクリア
                _offScreenWindow?.ClearScreen();
                _isTestDisplayActive = false;
                BtnTestDisplay.Content = "🔍 テスト表示";
                BtnTestDisplay.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9C27B0"));
                _log?.LogAdd("テスト表示を終了", _log.INFO);
            }
            else
            {
                // テスト表示していない場合は、テスト画面を表示
                var testScreen = new TestDisplayScreen();
                _offScreenWindow?.ShowScreen(testScreen);
                _isTestDisplayActive = true;
                BtnTestDisplay.Content = "✕ テスト終了";
                BtnTestDisplay.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F44336"));
                _log?.LogAdd("テスト表示画面を表示", _log.INFO);
            }
        }

        /// <summary>
        /// 再生ボタンクリック（メイン）
        /// </summary>
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            EnsureOffScreenWindowCreated();
            ExecuteCurrentStep();
        }

        /// <summary>
        /// クリアボタンクリック（全画面共通）
        /// </summary>
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _offScreenWindow?.ClearScreen();
            _offScreenWindow?.ClearSubScreen();
            _log?.LogAdd("画面クリア（メイン＋SUB）", _log.INFO);
        }

        /// <summary>
        /// メインクリアボタンクリック（AJSタブのメイン画面進行のみクリア）
        /// </summary>
        private void BtnMainClear_Click(object sender, RoutedEventArgs e)
        {
            _offScreenWindow?.ClearScreen();
            _log?.LogAdd("メイン画面クリア", _log.INFO);
        }

        /// <summary>
        /// SUB再生ボタンクリック
        /// </summary>
        private void BtnSubPlay_Click(object sender, RoutedEventArgs e)
        {
            EnsureOffScreenWindowCreated();
            ExecuteAjsSubStep();
        }

        /// <summary>
        /// SUBクリアボタンクリック
        /// </summary>
        private void BtnSubClear_Click(object sender, RoutedEventArgs e)
        {
            _offScreenWindow?.ClearSubScreen();
            _log?.LogAdd("SUB画面クリア", _log.INFO);
        }

        /// <summary>
        /// 設定ボタンクリック
        /// </summary>
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("設定画面は未実装です", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// シナリオの背景設定をすべての DisplayWindow（オフスクリーン・モニター・全画面）に適用する。
        /// </summary>
        /// <param name="bg">背景設定。null の場合はデフォルト（黒）にリセットする。</param>
        private void ApplyScenarioBackground(Scenario.AjsBackground? bg)
        {
            void Apply(DisplayWindow? window)
            {
                if (window == null) return;

                if (bg == null)
                {
                    window.ClearBackground();
                    return;
                }

                switch (bg.GetBackgroundType())
                {
                    case Scenario.AjsBackgroundType.Image:
                        if (!string.IsNullOrEmpty(bg.ImageFile))
                        {
                            var (packUri, exists) = window.SetBackgroundImage(bg.ImageFile);
                            _log?.LogAdd($"背景イメージ参照URI: {packUri}", _log.INFO);
                            _log?.LogAdd($"背景イメージ存在確認: {(exists ? "OK（設定済み）" : "NG（リソースが見つかりません）")}",
                                         exists ? _log.INFO : _log.ERR);
                        }
                        break;

                    case Scenario.AjsBackgroundType.Color:
                        window.SetBackgroundColor(bg.R, bg.G, bg.B);
                        _log?.LogAdd($"背景色を設定: RGB({bg.R},{bg.G},{bg.B})", _log.INFO);
                        break;

                    case Scenario.AjsBackgroundType.ChromaKey:
                        try
                        {
                            var colorStr = AppSettings.Instance.ChromaKeySettings.BackgroundColor;
                            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                            window.SetBackgroundColor(color.R, color.G, color.B);
                            _log?.LogAdd($"クロマキ背景色を設定: {colorStr}", _log.INFO);
                        }
                        catch (Exception ex)
                        {
                            _log?.LogAdd($"クロマキ背景色の設定に失敗: {ex.Message}", _log.ERR);
                            window.ClearBackground();
                        }
                        break;

                    default: // None
                        window.ClearBackground();
                        break;
                }
            }

            Apply(_offScreenWindow);
            Apply(_displayWindow);
            Apply(_fullScreenWindow);
        }

        /// <summary>
        /// 複数競技会リスト受信時に呼ばれるコールバック。
        /// UIスレッドでダイアログを表示して選択された CmpNo を返す。
        /// </summary>
        private System.Threading.Tasks.Task<string?> OnSelectCompetitionAsync(
            System.Collections.Generic.List<Messages.CompetitionInfo> competitions)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<string?>();

            Dispatcher.Invoke(() =>
            {
                var dialog = new CompetitionSelectDialog(competitions) { Owner = this };

                if (dialog.ShowDialog() == true)
                    tcs.SetResult(dialog.SelectedCmpNo);
                else
                    tcs.SetResult(null);
            });

            return tcs.Task;
        }

        #endregion

        #region 進行タブ

        // ---- enum / フィールド ----

        private enum ProgressDisplayMode { ProgressOnly, Heat, Final }
        private enum ProgressSize        { Large, Small }

        private ProgressDisplayMode _progressMode = ProgressDisplayMode.ProgressOnly;
        private ProgressSize        _progressSize = ProgressSize.Large;
        private bool                _autoProgress = true;

        private 画面.DSDspScreenBase? _currentProgressScreen = null;
        private string _currentProgressScreenId = string.Empty;

        // ---- データクラス ----

        private class ProgressListItem
        {
            public string PrgNo   { get; set; } = string.Empty;
            public string KbnNo   { get; set; } = string.Empty;
            public string RndNo   { get; set; } = string.Empty;
            public string KbnName { get; set; } = string.Empty;
            public string RndName { get; set; } = string.Empty;
            public override string ToString() => $"{PrgNo}  {KbnName}　{RndName}";
        }

        // ---- UIイベント ----

        private void LstProgressItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentProgressIndex = LstProgressItems.SelectedIndex;
            _log?.LogAdd($"進行項目選択: {_currentProgressIndex}", _log.DEBUG);
            UpdateProgressScreenIdLabel();
        }

        private void RbProgressSize_Changed(object sender, RoutedEventArgs e)
        {
            _progressSize = (RbSmall?.IsChecked == true) ? ProgressSize.Small : ProgressSize.Large;
            _currentProgressScreen = null;
            _currentProgressScreenId = string.Empty;
            UpdateProgressScreenIdLabel();
        }

        private void RbProgressMode_Changed(object sender, RoutedEventArgs e)
        {
            if (RbModeHeat?.IsChecked == true)
                _progressMode = ProgressDisplayMode.Heat;
            else if (RbModeFinal?.IsChecked == true)
                _progressMode = ProgressDisplayMode.Final;
            else
                _progressMode = ProgressDisplayMode.ProgressOnly;
            _currentProgressScreen = null;
            _currentProgressScreenId = string.Empty;
            UpdateProgressScreenIdLabel();
        }

        private void BtnLoadProgressList_Click(object sender, RoutedEventArgs e)
            => LoadProgressList();

        private void TglAutoProgress_Click(object sender, RoutedEventArgs e)
        {
            _autoProgress = TglAutoProgress?.IsChecked == true;
            if (TglAutoProgress != null)
            {
                TglAutoProgress.Content    = _autoProgress ? "⟳ 自動更新 ON" : "⟳ 自動更新 OFF";
                TglAutoProgress.Background = new System.Windows.Media.SolidColorBrush(
                    _autoProgress
                        ? (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#388E3C")
                        : (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9E9E9E"));
            }
        }

        private void CmbProgressDance_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void CmbProgressHeat_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void BtnIntervalInterrupt_Click(object sender, RoutedEventArgs e)
        {
            // インターバル割り込み（DSP_MSG_001 が存在すれば使う、なければ簡易メッセージ）
            ShowProgressInterrupt("インターバル");
        }

        private void BtnAdjustInterrupt_Click(object sender, RoutedEventArgs e)
        {
            ShowProgressInterrupt("調整中\nお待ちください");
        }

        private void BtnCustomInterrupt_Click(object sender, RoutedEventArgs e)
        {
            var text = Microsoft.VisualBasic.Interaction.InputBox(
                "表示するメッセージを入力してください", "任意メッセージ", "");
            if (!string.IsNullOrWhiteSpace(text))
                ShowProgressInterrupt(text);
        }

        private void TglAutoGroupDisplay_Click(object sender, RoutedEventArgs e) { }

        // ---- 進行一覧の構築 ----

        private void LoadProgressList()
        {
            var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;
            var dsStatus = dm?.DS_Status;
            var daMaster = dm?.DA_Master;

            if (dsStatus == null)
            {
                LstProgressItems.ItemsSource = new[] { "（DS_Status 未受信）" };
                TxtProgressCurrentPrg.Text = "";
                return;
            }

            var floors = dsStatus["DS_FLOORs"]?.AsArray();
            if (floors == null || floors.Count == 0)
            {
                LstProgressItems.ItemsSource = new[] { "（DS_FLOORs データなし）" };
                TxtProgressCurrentPrg.Text = "";
                return;
            }

            // 全フロアの PRGRS を SortOrder 昇順で収集（区分・ラウンド単位で重複排除）
            var seen  = new System.Collections.Generic.HashSet<string>();
            var items = new System.Collections.Generic.List<ProgressListItem>();

            var allPrgrs = new System.Collections.Generic.List<(int SortOrder, string PrgNo, string KbnNo, string RndNo)>();
            foreach (var floor in floors)
            {
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) continue;
                foreach (var prg in prgrs)
                {
                    var sortOrder = prg?["DS_SortOrder"]?.GetValue<int>() ?? 0;
                    var prgNo     = prg?["DS_PrgNo"]?.ToString() ?? "";
                    var kbnNo     = prg?["DS_KbnNo"]?.ToString() ?? "";
                    var rndNo     = prg?["DS_RndNo"]?.ToString() ?? "";
                    allPrgrs.Add((sortOrder, prgNo, kbnNo, rndNo));
                }
            }
            allPrgrs.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

            foreach (var (_, prgNo, kbnNo, rndNo) in allPrgrs)
            {
                var key = $"{kbnNo}-{rndNo}";
                if (!seen.Add(key)) continue;   // 同一区分・ラウンドは最初の1件のみ

                var kbnName = daMaster != null ? 画面.DSDspDataHelper.Get区分名(daMaster, kbnNo) : kbnNo;
                var rndName = daMaster != null ? 画面.DSDspDataHelper.Getラウンド名(daMaster, kbnNo, rndNo) : rndNo;

                items.Add(new ProgressListItem
                {
                    PrgNo   = prgNo,
                    KbnNo   = kbnNo,
                    RndNo   = rndNo,
                    KbnName = kbnName,
                    RndName = rndName,
                });
            }

            LstProgressItems.ItemsSource = items;
            TxtProgressCurrentPrg.Text   = items.Count > 0 ? $"（{items.Count} 件）" : "";

            if (items.Count > 0 && LstProgressItems.SelectedIndex < 0)
            {
                LstProgressItems.SelectedIndex = 0;
                _currentProgressIndex = 0;
            }

            UpdateProgressScreenIdLabel();
            _log?.LogAdd($"進行一覧構築: {items.Count} 件", _log.INFO);
        }

        /// <summary>TxtProgressScreenId に現在の画面ID と選択情報を反映する。</summary>
        private void UpdateProgressScreenIdLabel()
        {
            if (TxtProgressScreenId == null) return;
            var screenId = GetProgressScreenId();
            TxtProgressScreenId.Text = $"画面: {screenId}";
        }

        /// <summary>大/小 × モードから使用する画面ID を返す。</summary>
        private string GetProgressScreenId()
        {
            bool isSmall = (_progressSize == ProgressSize.Small);
            return _progressMode switch
            {
                ProgressDisplayMode.Heat  when !isSmall => "DSP_PRG_004",
                ProgressDisplayMode.Heat  when isSmall  => "DSP_PRG_005",
                ProgressDisplayMode.Final when !isSmall => "DSP_PRG_006",
                ProgressDisplayMode.Final when isSmall  => "DSP_PRG_007",
                _                         when !isSmall => "DSP_PRG_001",
                _                                       => "DSP_PRG_002",
            };
        }

        // ---- 割り込みメッセージ ----

        private void ShowProgressInterrupt(string message)
        {
            EnsureOffScreenWindowCreated();
            if (_offScreenWindow == null) return;
            // DSP_MSG_001 が実装済みなら利用（現在はメッセージボックスで代替）
            _log?.LogAdd($"割り込みメッセージ: {message}", _log.INFO);
            MessageBox.Show(message, "割り込み", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ---- ScreenCompleted ----

        private void OnProgressScreenCompleted(object? sender, EventArgs e)
        {
            if (sender is 画面.DSDspScreenBase s)
                s.ScreenCompleted -= OnProgressScreenCompleted;
            _currentProgressScreen = null;

            // 自動更新ON の場合、次の進行に選択を移す
            if (_autoProgress)
            {
                Dispatcher.Invoke(() =>
                {
                    var items = LstProgressItems.ItemsSource as System.Collections.Generic.List<ProgressListItem>;
                    if (items == null) return;
                    int nextIdx = _currentProgressIndex + 1;
                    if (nextIdx < items.Count)
                    {
                        _currentProgressIndex = nextIdx;
                        LstProgressItems.SelectedIndex = nextIdx;
                        _log?.LogAdd($"進行: 次の項目へ自動遷移 Index={nextIdx}", _log.INFO);
                    }
                });
            }
        }

        #endregion

        #region AJSタブ

        private void CmbAjsScenario_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbAjsScenario.SelectedItem == null || _scenarioManager == null) return;

            var fileName = CmbAjsScenario.SelectedItem.ToString();
            if (string.IsNullOrEmpty(fileName)) return;

            // 新モデルで読み込み（バリデーション含む）
            _currentAjsScenario = _scenarioManager.LoadAjsScenario(fileName);
            _currentAjsProgressItems = null;
            LstAjsProgress.ItemsSource = null;

            if (_currentAjsScenario == null)
            {
                MessageBox.Show($"AJSシナリオの読み込みに失敗しました。\nログを確認してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // シナリオの背景設定を全ウィンドウに適用
            ApplyScenarioBackground(_currentAjsScenario.Background);

            // DA_Masterから区分一覧を取得
            var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;
            if (dm?.DA_Master != null)
            {
                var categories = _scenarioManager.GetAjsCategoriesFromDaMaster(dm.DA_Master);

                _ajsCategoryKeys.Clear();
                var displayTexts = new List<string>();

                foreach (var category in categories)
                {
                    var parts = category.Split('|');
                    if (parts.Length == 2)
                    {
                        _ajsCategoryKeys[parts[1]] = parts[0];
                        displayTexts.Add(parts[1]);
                    }
                }

                CmbAjsCategory.ItemsSource = displayTexts;
                if (displayTexts.Count > 0)
                    CmbAjsCategory.SelectedIndex = 0;

                _log?.LogAdd($"AJS区分一覧をDA_Masterから取得: {displayTexts.Count}件", _log.INFO);
            }
            else
            {
                _log?.LogAdd("DA_Masterが未取得のため、区分一覧を表示できません", _log.WARNING);
            }
        }

        private async void CmbAjsCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _log?.LogAdd("CmbAjsCategory_SelectionChanged開始", _log.DEBUG);

            if (CmbAjsCategory.SelectedItem == null || _currentAjsScenario == null || _scenarioManager == null)
                return;

            var displayText = CmbAjsCategory.SelectedItem.ToString();
            if (string.IsNullOrEmpty(displayText)) return;

            if (!_ajsCategoryKeys.TryGetValue(displayText, out var key))
            {
                _log?.LogAdd($"Dictionaryにキーが見つかりません: {displayText}", _log.WARNING);
                return;
            }

            // キー形式: "区分No-ラウンドNo"
            var keyParts = key.Split('-');
            if (keyParts.Length != 2) return;

            var kbnNo   = keyParts[0];
            var roundNo = keyParts[1];

            // DS_Status と DA_Master から画面進行一覧を動的生成
            var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;

            _currentAjsProgressItems = null;
            LstAjsProgress.ItemsSource = null;
            _currentAjsIndex = -1;

            // SUB画面進行もリセット
            _currentAjsSubProgressItems = null;
            LstAjsSubProgress.ItemsSource = null;
            _currentAjsSubIndex = -1;

            if (dm?.DS_Status != null && dm?.DA_Master != null)
            {
                // メイン画面進行一覧を生成
                _currentAjsProgressItems = _scenarioManager.BuildProgressList(
                    _currentAjsScenario, dm.DS_Status, dm.DA_Master, kbnNo, roundNo);

                if (_currentAjsProgressItems != null)
                {
                    LstAjsProgress.ItemsSource = _currentAjsProgressItems;
                    _log?.LogAdd($"AJS画面進行一覧生成: {_currentAjsProgressItems.Count}件 (区分={kbnNo}, ラウンド={roundNo})", _log.INFO);
                    UpdateResultReadyLabel();
                }
                else
                {
                    _log?.LogAdd("AJS画面進行一覧の生成に失敗しました", _log.ERR);
                    MessageBox.Show("画面進行一覧の生成に失敗しました。\nDS_StatusにこのラウンドのDE_DncSGが設定されているか確認してください。\nログを参照してください。",
                        "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // SUBシナリオが定義されている場合はSUB画面進行一覧を生成
                if (_currentAjsScenario.SubScenario != null)
                {
                    // AjsSubScenarioDefinition を AjsScenarioDefinition にラップして BuildProgressList を流用
                    var subScenarioDef = new Scenario.AjsScenarioDefinition
                    {
                        ScenarioName = _currentAjsScenario.SubScenario.ScenarioName,
                        ScenarioType = "AJS",
                        Description  = _currentAjsScenario.SubScenario.Description,
                        Screens      = _currentAjsScenario.SubScenario.Screens,
                        Background   = null   // SUBは透明背景
                    };

                    _currentAjsSubProgressItems = _scenarioManager.BuildProgressList(
                        subScenarioDef, dm.DS_Status, dm.DA_Master, kbnNo, roundNo);

                    if (_currentAjsSubProgressItems != null)
                    {
                        LstAjsSubProgress.ItemsSource = _currentAjsSubProgressItems;
                        _log?.LogAdd($"AJS SUB画面進行一覧生成: {_currentAjsSubProgressItems.Count}件 (区分={kbnNo}, ラウンド={roundNo})", _log.INFO);
                    }
                    else
                    {
                        _log?.LogAdd("AJS SUB画面進行一覧の生成に失敗しました", _log.WARNING);
                    }
                }
            }
            else
            {
                _log?.LogAdd("DS_StatusまたはDA_Masterが未取得のため、画面進行一覧を生成できません", _log.WARNING);
            }

            // サーバーに DP_ASK_DV_RESULT 電文を送信
            if (_client != null && _client.IsConnected)
            {
                _log?.LogAdd($"DP_ASK_DV_RESULT送信: 区分={kbnNo}, ラウンド={roundNo}", _log.INFO);
                bool ok = await _client.RequestDV_ResultAsync(kbnNo, roundNo);
                if (!ok)
                    _log?.LogAdd("DP_ASK_DV_RESULT送信失敗", _log.WARNING);
            }
        }

        private void LstAjsProgress_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAjsSelectionChanged) return;

            _currentAjsIndex = LstAjsProgress.SelectedIndex;
            _log?.LogAdd($"AJS項目選択: {_currentAjsIndex}", _log.DEBUG);

            UpdateResultReadyLabel();
        }

        private void LstAjsSubProgress_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAjsSubSelectionChanged) return;

            _currentAjsSubIndex = LstAjsSubProgress.SelectedIndex;
            _log?.LogAdd($"AJS SUB項目選択: {_currentAjsSubIndex}", _log.DEBUG);
        }

        #endregion

        #region 表彰式タブ

        // ---- enum / フィールド ----

        private enum AwardDisplayMode { Full, ChromaList, ChromaIndividual }
        private enum AwardOrderMode { Page, Asc, Desc }

        private AwardDisplayMode _awardDisplay = AwardDisplayMode.Full;
        private AwardOrderMode   _awardOrder   = AwardOrderMode.Asc;

        private (string KbnNo, string RndNo, string KbnName, string Display)? _awardSelectedCategory = null;
        private bool _awardSelectedIsAwardTitle = false;
        private bool _awardSelectedIsAwardEnd   = false;
        private 画面.DSDspScreenBase? _currentAwardScreen = null;

        // ---- データクラス ----

        private class AwardCategoryItem
        {
            public string KbnNo   { get; set; } = string.Empty;
            public string RndNo   { get; set; } = string.Empty;
            public string KbnName { get; set; } = string.Empty;
            public string Display { get; set; } = string.Empty;
            public bool IsAwardTitle { get; set; } = false;
            public bool IsAwardEnd   { get; set; } = false;
            public override string ToString() => Display;
        }

        private class AwardPreviewItem
        {
            public string 順位   { get; set; } = string.Empty;
            public string 背番号 { get; set; } = string.Empty;
            public string 選手名 { get; set; } = string.Empty;
            public string 所属   { get; set; } = string.Empty;
            public string 得点   { get; set; } = string.Empty;
            public override string ToString() => $"{順位}  {背番号}  {選手名}";
        }

        // ---- UIイベント ----

        private void RbAwardDisplay_Changed(object sender, RoutedEventArgs e)
        {
            if (RbAwardChromaList?.IsChecked == true)
                _awardDisplay = AwardDisplayMode.ChromaList;
            else if (RbAwardChromaIndividual?.IsChecked == true)
                _awardDisplay = AwardDisplayMode.ChromaIndividual;
            else
                _awardDisplay = AwardDisplayMode.Full;
            _currentAwardScreen = null;
        }

        private void RbAwardOrder_Changed(object sender, RoutedEventArgs e)
        {
            if (RbAwardPage?.IsChecked == true)
                _awardOrder = AwardOrderMode.Page;
            else if (RbAwardDesc?.IsChecked == true)
                _awardOrder = AwardOrderMode.Desc;
            else
                _awardOrder = AwardOrderMode.Asc;
            _currentAwardScreen = null;
        }

        private void BtnAwardRefresh_Click(object sender, RoutedEventArgs e)
            => LoadAwardCategoryList();

        private async void LstAwardCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstAwardCategories.SelectedItem is not AwardCategoryItem item) return;

            _awardSelectedIsAwardTitle = item.IsAwardTitle;
            _awardSelectedIsAwardEnd   = item.IsAwardEnd;
            _currentAwardScreen = null;

            if (item.IsAwardTitle)
            {
                _awardSelectedCategory = null;
                AwardPreviewList.ItemsSource = null;
                UpdateAwardStatus("表彰式タイトル選択済み — 再生ボタンで表示");
            }
            else if (item.IsAwardEnd)
            {
                _awardSelectedCategory = null;
                AwardPreviewList.ItemsSource = null;
                UpdateAwardStatus("終了選択済み — 再生ボタンで表示");
            }
            else
            {
                _awardSelectedCategory = (item.KbnNo, item.RndNo, item.KbnName, item.Display);
                UpdateAwardStatus($"DV_Result 要求中: {item.Display} ...");

                // サーバーに DV_Result を要求（テストデータ使用時はスキップ）
                if (_testDataManager == null && _client != null && _client.IsConnected)
                {
                    _log?.LogAdd($"表彰式 DP_ASK_DV_RESULT送信: 区分={item.KbnNo}, ラウンド={item.RndNo}", _log.INFO);
                    bool ok = await _client.RequestDV_ResultAsync(item.KbnNo, item.RndNo);
                    if (!ok)
                        _log?.LogAdd("表彰式 DP_ASK_DV_RESULT送信失敗", _log.WARNING);
                }
                else
                {
                    // テストデータ使用時はキャッシュを即時参照
                    UpdateAwardStatus("区分選択済み — 再生ボタンで表示");
                    UpdateAwardPreview(item.KbnNo, item.RndNo);
                }
            }
        }

        // ---- リスト読み込み ----

        private void LoadAwardCategoryList()
        {
            var daMaster = _testDataManager?.DA_Master ?? _client?.DataManager.DA_Master;

            var items = new List<AwardCategoryItem>();
            items.Add(new AwardCategoryItem { IsAwardTitle = true, Display = "（表彰式タイトル）" });

            if (daMaster != null)
            {
                var kubuns = daMaster["DB_KUBUNs"]?.AsArray();
                if (kubuns != null)
                {
                    foreach (var kubun in kubuns)
                    {
                        if (kubun == null) continue;
                        var kbnNo   = kubun["DB_KbnNo"]?.ToString()   ?? string.Empty;
                        var kbnName = kubun["DB_KbnName"]?.ToString() ?? string.Empty;

                        var rounds = kubun["DC_ROUNDs"]?.AsArray();
                        if (rounds == null) continue;

                        foreach (var round in rounds)
                        {
                            if (round == null) continue;
                            var rndNo = round["DC_RndNo"]?.ToString() ?? string.Empty;
                            if (rndNo != "300" && rndNo != "400") continue;

                            if (items.Any(x => !x.IsAwardTitle && x.KbnNo == kbnNo && x.RndNo == rndNo)) continue;

                            items.Add(new AwardCategoryItem
                            {
                                KbnNo   = kbnNo,
                                RndNo   = rndNo,
                                KbnName = kbnName,
                                Display = $"{kbnNo}  {kbnName}",
                            });
                        }
                    }
                }
            }

            items.Add(new AwardCategoryItem { IsAwardEnd = true, Display = "（終了）" });

            LstAwardCategories.ItemsSource = items;
            if (items.Count > 0) LstAwardCategories.SelectedIndex = 0;
            int count = items.Count - 2; // タイトルと終了を除いた区分件数
            UpdateAwardStatus(count > 0
                ? $"{count} 件の区分を表示（区分を選択すると結果を取得します）"
                : (daMaster == null ? "DA_Master 未受信" : "決勝ラウンド（300/400）を持つ区分がありません"));
        }

        #endregion

        #region ジャッジ紹介タブ

        // ---- enum / フィールド ----

        private enum JudgeDisplayMode { Full, ChromaList, ChromaIndividual }
        private JudgeDisplayMode _judgeDisplay = JudgeDisplayMode.Full;

        // 現在選択中のジャッジグループID（null=全員）
        private string? _currentJudgeGroupId = null;

        // 現在の画面インスタンス
        private 画面.DSDspScreenBase? _currentJudgeScreen = null;

        // 現在選択中のジャッジ（クロマキ個別用）
        private JudgeListItem? _selectedJudge = null;

        // 「終了」行が選択されているか
        private bool _judgeSelectedIsEnd = false;

        // ---- データクラス ----

        private class JudgeListItem
        {
            public string JdgCd       { get; set; } = string.Empty;
            public string JdgDispName { get; set; } = string.Empty;
            public string JdgCtry     { get; set; } = string.Empty;
            public bool   IsEnd       { get; set; } = false;
            public override string ToString() => IsEnd ? "（終了）" : $"{JdgCd}  {JdgDispName}";
        }

        // ---- UIイベント ----

        private void RbJudgeDisplay_Changed(object sender, RoutedEventArgs e)
        {
            if (RbJudgeChromaList?.IsChecked == true)
                _judgeDisplay = JudgeDisplayMode.ChromaList;
            else if (RbJudgeChromaIndividual?.IsChecked == true)
                _judgeDisplay = JudgeDisplayMode.ChromaIndividual;
            else
                _judgeDisplay = JudgeDisplayMode.Full;
            _currentJudgeScreen = null;
        }

        private void BtnJudgeRefresh_Click(object sender, RoutedEventArgs e)
            => LoadJudgeGroupTabs();

        private void TabJudgeGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabJudgeGroups.SelectedItem is TabItem tab)
            {
                // タグに null が入っている場合は「全員」タブ
                _currentJudgeGroupId = tab.Tag as string;
                UpdateJudgeList();
                _currentJudgeScreen = null;
                UpdateJudgeStatus("グループ変更 — 再生ボタンで表示");
            }
        }

        private void LstJudgeItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstJudgeItems.SelectedItem is JudgeListItem item)
            {
                _judgeSelectedIsEnd = item.IsEnd;
                _currentJudgeScreen = null;
                if (item.IsEnd)
                {
                    _selectedJudge = null;
                    UpdateJudgeStatus("終了選択済み — 再生ボタンで画面をクリア");
                }
                else
                {
                    _selectedJudge = item;
                    UpdateJudgeStatus($"選択: {item.JdgCd}  {item.JdgDispName}");
                }
            }
        }

        // ---- リスト読み込み ----

        private void LoadJudgeGroupTabs()
        {
            var daMaster = _testDataManager?.DA_Master ?? _client?.DataManager.DA_Master;

            TabJudgeGroups.SelectionChanged -= TabJudgeGroups_SelectionChanged;
            TabJudgeGroups.Items.Clear();

            // 「全員」タブを先頭に追加（Tag=null）
            var allTab = new TabItem { Header = "全員", Tag = null };
            TabJudgeGroups.Items.Add(allTab);

            if (daMaster != null)
            {
                var grpList = 画面.DSDspDataHelper.Getジャッジグループリスト(daMaster);
                foreach (var grp in grpList)
                {
                    var tabItem = new TabItem { Header = grp, Tag = grp };
                    TabJudgeGroups.Items.Add(tabItem);
                }
            }

            TabJudgeGroups.SelectedIndex = 0;
            TabJudgeGroups.SelectionChanged += TabJudgeGroups_SelectionChanged;

            // 初期表示
            _currentJudgeGroupId = null;
            UpdateJudgeList();
            _currentJudgeScreen = null;

            int cnt = DSDspDataHelper.Getジャッジリスト(daMaster).Count;
            UpdateJudgeStatus(cnt > 0
                ? $"{cnt} 件のジャッジを表示"
                : (daMaster == null ? "DA_Master 未受信" : "ジャッジが登録されていません"));
        }

        private void UpdateJudgeList()
        {
            var daMaster = _testDataManager?.DA_Master ?? _client?.DataManager.DA_Master;
            var items = 画面.DSDspDataHelper.Getジャッジリスト_ByGroup(daMaster, _currentJudgeGroupId)
                .Select(j => new JudgeListItem
                {
                    JdgCd       = j.JdgCd,
                    JdgDispName = j.JdgDispName,
                    JdgCtry     = j.JdgCtry,
                })
                .ToList();
            // 末尾に「終了」行を追加
            items.Add(new JudgeListItem { IsEnd = true });
            LstJudgeItems.ItemsSource = items;
            _judgeSelectedIsEnd = false;
            if (items.Count > 0)
                LstJudgeItems.SelectedIndex = 0;
        }

        // ---- ステップ実行 ----

        private void ExecuteJudgeStep()
        {
            EnsureOffScreenWindowCreated();
            if (_offScreenWindow == null) return;

            var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;

            // 終了
            if (_judgeSelectedIsEnd)
            {
                ExecuteJudgeEndScreen(dm);
                return;
            }

            // クロマキ個別
            if (_judgeDisplay == JudgeDisplayMode.ChromaIndividual)
            {
                ExecuteJudgeIndividualStep(dm);
                return;
            }

            // 全画面 / クロマキリスト
            var judgeList = DSDspDataHelper.Getジャッジリスト_ByGroup(dm?.DA_Master, _currentJudgeGroupId);
            int count = judgeList.Count;

            // 使用する画面ID を決定
            string screenId;
            if (_judgeDisplay == JudgeDisplayMode.ChromaList)
                screenId = "DSP_PRG_014";
            else
                screenId = (count <= 10) ? "DSP_PRG_012" : "DSP_PRG_013";

            // クロマキ背景を適用
            bool isChroma = (_judgeDisplay == JudgeDisplayMode.ChromaList);
            ApplyJudgeWindowBackground(isChroma);

            // 同じ画面・同じグループなら継続
            bool isSameScreen = _currentJudgeScreen != null
                && _currentJudgeScreen.ScreenId == screenId
                && (_currentJudgeScreen is 画面.DSP_PRG_012_ジャッジ紹介10_大 s12 && s12.JudgeGroupId == _currentJudgeGroupId
                 || _currentJudgeScreen is 画面.DSP_PRG_013_ジャッジ紹介20_大 s13 && s13.JudgeGroupId == _currentJudgeGroupId
                 || _currentJudgeScreen is 画面.DSP_PRG_014_ジャッジ紹介10_小 s14 && s14.JudgeGroupId == _currentJudgeGroupId);

            if (!isSameScreen)
            {
                if (_currentJudgeScreen != null)
                    _currentJudgeScreen.ScreenCompleted -= OnJudgeScreenCompleted;

                画面.DSDspScreenBase? newScreen = screenId switch
                {
                    "DSP_PRG_012" => new 画面.DSP_PRG_012_ジャッジ紹介10_大 { JudgeGroupId = _currentJudgeGroupId },
                    "DSP_PRG_013" => new 画面.DSP_PRG_013_ジャッジ紹介20_大 { JudgeGroupId = _currentJudgeGroupId },
                    "DSP_PRG_014" => new 画面.DSP_PRG_014_ジャッジ紹介10_小 { JudgeGroupId = _currentJudgeGroupId },
                    _ => null
                };

                if (newScreen == null) return;

                newScreen.ScreenId  = screenId;
                newScreen.DA_Master = dm?.DA_Master;
                newScreen.DS_Status = dm?.DS_Status;
                newScreen.DV_Result = dm?.DV_Result;
                newScreen.ScreenCompleted += OnJudgeScreenCompleted;

                _currentJudgeScreen = newScreen;
                _offScreenWindow.ShowScreen(newScreen, screenId);
                _log?.LogAdd($"ジャッジ紹介画面表示: {screenId}", _log.INFO);
            }

            var screenForStatus = _currentJudgeScreen;
            screenForStatus!.Advance();
            UpdateJudgeStatus(_currentJudgeScreen != null
                ? $"表示中  Step={_currentJudgeScreen.CurrentStep}"
                : "画面終了");
        }

        private void ExecuteJudgeIndividualStep(Data.DataManager? dm)
        {
            if (_selectedJudge == null)
            {
                MessageBox.Show("ジャッジを選択してください", "ジャッジ紹介", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            const string screenId = "DSP_PRG_010";
            ApplyJudgeWindowBackground(true);

            bool isSameScreen = _currentJudgeScreen?.ScreenId == screenId
                && _currentJudgeScreen is 画面.DSP_PRG_010_選手紹介_小 s10prev
                && s10prev.HonorBango == _selectedJudge.JdgCd;

            if (!isSameScreen)
            {
                if (_currentJudgeScreen != null)
                    _currentJudgeScreen.ScreenCompleted -= OnJudgeScreenCompleted;

                var s010 = new 画面.DSP_PRG_010_選手紹介_小();
                s010.ScreenId          = screenId;
                s010.DA_Master         = dm?.DA_Master;
                s010.DS_Status         = dm?.DS_Status;
                s010.DV_Result         = dm?.DV_Result;
                s010.HonorMode         = true;
                s010.HonorBango        = _selectedJudge.JdgCd;
                s010.HonorAffiliation  = _selectedJudge.JdgCtry;
                // COM002テキストのオーバーライド: "表彰式" → "ジャッジ紹介"
                s010.COM002TextOverride    = "ジャッジ紹介";
                // HonorMode 時: LB_区分名=ブランク、LB_選手名=ジャッジ表記名
                s010.HonorLB区分名Override = string.Empty;
                s010.HonorLB選手名Override = _selectedJudge.JdgDispName;
                // LB_順位=ジャッジ記号（HonorBango）、LB_所属=ジャッジ所属（HonorAffiliation）

                s010.ScreenCompleted += OnJudgeScreenCompleted;
                _currentJudgeScreen  = s010;
                _offScreenWindow!.ShowScreen(s010, screenId);
                _log?.LogAdd($"ジャッジ紹介個別表示: {_selectedJudge.JdgCd}  {_selectedJudge.JdgDispName}", _log.INFO);
            }

            var screenForStatus2 = _currentJudgeScreen;
            screenForStatus2!.Advance();
            UpdateJudgeStatus(_currentJudgeScreen != null
                ? $"個別表示  {_selectedJudge.JdgCd}  Step={_currentJudgeScreen.CurrentStep}"
                : "画面終了");
        }

        /// <summary>ジャッジ紹介 終了: COM001マーク+競技会名のみ残す。</summary>
        private void ExecuteJudgeEndScreen(Data.DataManager? dm)
        {
            EnsureOffScreenWindowCreated();
            if (_offScreenWindow == null) return;

            string 競技会名 = 画面.DSDspDataHelper.Get競技会名(dm?.DA_Master);

            // 現在の表示モードに合わせてクロマキ/全画面を選択
            bool isChroma = (_judgeDisplay == JudgeDisplayMode.ChromaList
                          || _judgeDisplay == JudgeDisplayMode.ChromaIndividual);
            ApplyJudgeWindowBackground(isChroma);

            string screenId = isChroma ? "DSP_PRG_010" : "DSP_PRG_012";

            bool canReuse = _currentJudgeScreen != null
                && (_currentJudgeScreen.ScreenId == "DSP_PRG_010"
                    || _currentJudgeScreen.ScreenId == "DSP_PRG_012");

            if (!canReuse)
            {
                if (_currentJudgeScreen != null)
                    _currentJudgeScreen.ScreenCompleted -= OnJudgeScreenCompleted;

                if (isChroma)
                {
                    var s = new 画面.DSP_PRG_010_選手紹介_小();
                    s.ScreenId   = screenId;
                    s.DA_Master  = dm?.DA_Master;
                    s.DS_Status  = dm?.DS_Status;
                    s.DV_Result  = dm?.DV_Result;
                    s.ScreenCompleted += OnJudgeScreenCompleted;
                    _currentJudgeScreen = s;
                    _offScreenWindow.ShowScreen(s, screenId);
                    s.EnsurePartsInitialized();
                    s.PartsCOM001.IM_JDSFマーク.Source =
                        new System.Windows.Media.Imaging.BitmapImage(
                            new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
                    s.PartsCOM001.TB_左上1.Text = 競技会名;
                    s.PartsCOM001.TB_左上2.Text = string.Empty;
                    // COM002/COM003/PRG006 を非表示
                    s.PartsCOM002.LB_右上.Visibility   = Visibility.Collapsed;
                    s.PartsCOM003.LB_右上.Visibility   = Visibility.Collapsed;
                    s.PartsPRG006.LB_区分名.Visibility = Visibility.Collapsed;
                    s.PartsPRG006.LB_順位.Visibility   = Visibility.Collapsed;
                    s.PartsPRG006.LB_選手名.Visibility = Visibility.Collapsed;
                    s.PartsPRG006.LB_所属.Visibility   = Visibility.Collapsed;
                    s.PartsPRG006.LB_得点.Visibility   = Visibility.Collapsed;
                    s.PartsPRG006.IM_種目1.Visibility  = Visibility.Collapsed;
                    s.PartsPRG006.IM_種目2.Visibility  = Visibility.Collapsed;
                }
                else
                {
                    var s = new 画面.DSP_PRG_012_ジャッジ紹介10_大();
                    s.ScreenId   = screenId;
                    s.DA_Master  = dm?.DA_Master;
                    s.DS_Status  = dm?.DS_Status;
                    s.DV_Result  = dm?.DV_Result;
                    s.ScreenCompleted += OnJudgeScreenCompleted;
                    _currentJudgeScreen = s;
                    _offScreenWindow.ShowScreen(s, screenId);
                    s.EnsurePartsInitialized();
                    if (s.PartsCOM001.FindName("IM_JDSFマーク") is System.Windows.Controls.Image im)
                        im.Source = new System.Windows.Media.Imaging.BitmapImage(
                            new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
                    if (s.PartsCOM001.FindName("TB_左上1") is System.Windows.Controls.TextBlock tb1)
                        tb1.Text = 競技会名;
                    if (s.PartsCOM001.FindName("TB_左上2") is System.Windows.Controls.TextBlock tb2)
                        tb2.Text = string.Empty;
                    // LST002 / COM002 / COM003 を非表示
                    if (s.PartsCOM002.FindName("LB_右上") is System.Windows.Controls.Label lb002)
                        lb002.Visibility = Visibility.Collapsed;
                    if (s.PartsCOM003.FindName("LB_右上") is System.Windows.Controls.Label lb003)
                        lb003.Visibility = Visibility.Collapsed;
                    s.PartsLST002.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // 画面を再利用する場合も、COM001以外を確実に非表示にする
                if (_currentJudgeScreen is 画面.DSP_PRG_010_選手紹介_小 s010)
                {
                    s010.PartsCOM002.LB_右上.Visibility   = Visibility.Collapsed;
                    s010.PartsCOM003.LB_右上.Visibility   = Visibility.Collapsed;
                    s010.PartsPRG006.LB_区分名.Visibility = Visibility.Collapsed;
                    s010.PartsPRG006.LB_順位.Visibility   = Visibility.Collapsed;
                    s010.PartsPRG006.LB_選手名.Visibility = Visibility.Collapsed;
                    s010.PartsPRG006.LB_所属.Visibility   = Visibility.Collapsed;
                    s010.PartsPRG006.LB_得点.Visibility   = Visibility.Collapsed;
                    s010.PartsPRG006.IM_種目1.Visibility  = Visibility.Collapsed;
                    s010.PartsPRG006.IM_種目2.Visibility  = Visibility.Collapsed;
                }
                else if (_currentJudgeScreen is 画面.DSP_PRG_012_ジャッジ紹介10_大 s012)
                {
                    if (s012.PartsCOM002.FindName("LB_右上") is System.Windows.Controls.Label lb002)
                        lb002.Visibility = Visibility.Collapsed;
                    if (s012.PartsCOM003.FindName("LB_右上") is System.Windows.Controls.Label lb003)
                        lb003.Visibility = Visibility.Collapsed;
                    s012.PartsLST002.Visibility = Visibility.Collapsed;
                }
            }

            UpdateJudgeStatus("終了 — COM001のみ表示中");
            _log?.LogAdd("ジャッジ紹介 終了画面", _log.INFO);
        }

        private void OnJudgeScreenCompleted(object? sender, EventArgs e)
        {
            if (sender is 画面.DSDspScreenBase s)
                s.ScreenCompleted -= OnJudgeScreenCompleted;
            _currentJudgeScreen = null;

            Dispatcher.Invoke(() =>
            {
                // クロマキ個別モードの場合、リスト選択を次のジャッジへ自動移動
                if (_judgeDisplay == JudgeDisplayMode.ChromaIndividual)
                {
                    int cur = LstJudgeItems.SelectedIndex;
                    int next = cur + 1;
                    if (next < LstJudgeItems.Items.Count)
                    {
                        // SelectionChanged イベントで _currentJudgeScreen がリセットされるが、
                        // ここでは既に null になっているので問題なし
                        LstJudgeItems.SelectedIndex = next;
                        LstJudgeItems.ScrollIntoView(LstJudgeItems.SelectedItem);
                        UpdateJudgeStatus($"次のジャッジに移動: {_selectedJudge?.JdgCd}  {_selectedJudge?.JdgDispName}");
                    }
                    else
                    {
                        UpdateJudgeStatus("最後のジャッジを表示しました");
                    }
                }
                else
                {
                    UpdateJudgeStatus("画面終了");
                }
            });
        }

        private void UpdateJudgeStatus(string text)
        {
            if (TxtJudgeStatus != null)
                TxtJudgeStatus.Text = text;
        }

        private void ApplyJudgeWindowBackground(bool isChroma)
        {
            void Apply(DisplayWindow? window)
            {
                if (window == null) return;
                if (isChroma)
                {
                    try
                    {
                        var colorStr = AppSettings.Instance.ChromaKeySettings.BackgroundColor;
                        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                        window.SetBackgroundColor(color.R, color.G, color.B);
                    }
                    catch { window.ClearBackground(); }
                }
                else
                {
                    window.ClearBackground();
                }
            }
            Apply(_offScreenWindow);
            Apply(_displayWindow);
            Apply(_fullScreenWindow);
        }

        #endregion

        #region ステップ実行

        /// <summary>
        /// 再生ボタンから呼ばれる。現在のタブに応じてステップを実行する。
        /// </summary>
        private void ExecuteCurrentStep()
        {
            if (TabControl.SelectedIndex == 1)      // AJSタブ
                ExecuteAjsStep();
            else if (TabControl.SelectedIndex == 2) // 表彰式タブ
                ExecuteAwardStep();
            else if (TabControl.SelectedIndex == 3) // オナーダンスタブ
                ExecuteHonorStep();
            else if (TabControl.SelectedIndex == 4) // ジャッジ紹介タブ
                ExecuteJudgeStep();
            else                                    // 進行タブ
                ExecuteProgressStep();
        }

        /// <summary>
        /// 進行タブのステップを実行
        /// </summary>
        private void ExecuteProgressStep()
        {
            var items = LstProgressItems.ItemsSource as System.Collections.Generic.List<ProgressListItem>;
            if (items == null || _currentProgressIndex < 0 || _currentProgressIndex >= items.Count)
            {
                _log?.LogAdd("進行項目が選択されていません", _log.WARNING);
                return;
            }

            EnsureOffScreenWindowCreated();
            if (_offScreenWindow == null) return;

            var item     = items[_currentProgressIndex];
            var screenId = GetProgressScreenId();
            var dm       = (_testDataManager != null) ? _testDataManager : _client?.DataManager;

            // 画面の切り替え判定
            bool isSameScreen = _currentProgressScreen != null
                && _currentProgressScreenId == screenId
                && _currentProgressScreen.区分番号  == item.KbnNo
                && _currentProgressScreen.ラウンド番号 == item.RndNo;

            if (!isSameScreen)
            {
                if (_currentProgressScreen != null)
                    _currentProgressScreen.ScreenCompleted -= OnProgressScreenCompleted;

                画面.DSDspScreenBase? newScreen = screenId switch
                {
                    "DSP_PRG_002" => new 画面.DSP_PRG_002_進行表示1面_小(),
                    "DSP_PRG_004" => new 画面.DSP_PRG_004_進行表示ヒート表_大(),
                    "DSP_PRG_005" => new 画面.DSP_PRG_005_進行表示ヒート表_小(),
                    "DSP_PRG_006" => new 画面.DSP_PRG_006_決勝進出者_大(),
                    "DSP_PRG_007" => new 画面.DSP_PRG_007_決勝進出者_小(),
                    _             => new 画面.DSP_PRG_001_進行表示1面_大(),
                };

                newScreen.ScreenId     = screenId;
                newScreen.DA_Master    = dm?.DA_Master;
                newScreen.DS_Status    = dm?.DS_Status;
                newScreen.DV_Result    = dm?.DV_Result;
                newScreen.区分番号     = item.KbnNo;
                newScreen.ラウンド番号  = item.RndNo;
                newScreen.ScreenCompleted += OnProgressScreenCompleted;

                _currentProgressScreen   = newScreen;
                _currentProgressScreenId = screenId;
                _offScreenWindow.ShowScreen(newScreen, item.KbnNo);
                _log?.LogAdd($"進行画面表示: {screenId}  KbnNo={item.KbnNo} RndNo={item.RndNo}", _log.INFO);
            }

            _currentProgressScreen!.Advance();
            _log?.LogAdd($"進行 Advance: {screenId}  Step={_currentProgressScreen.CurrentStep}", _log.INFO);
        }

        /// <summary>
        /// AJSタブ：現在の画面に Advance() を送る。
        /// 初回（_currentAjsIndex が示す画面が未表示）の場合は画面を生成して表示した後 Advance() する。
        /// ScreenCompleted を受け取ったときに次の画面へ遷移する。
        /// </summary>
        private void ExecuteAjsStep()
        {
            EnsureOffScreenWindowCreated();

            if (_currentAjsProgressItems == null || _currentAjsIndex < 0 || _currentAjsIndex >= _currentAjsProgressItems.Count)
            {
                _log?.LogAdd("AJS項目が選択されていません", _log.WARNING);
                return;
            }

            if (CmbAjsCategory.SelectedItem == null) return;
            var displayText = CmbAjsCategory.SelectedItem.ToString();
            if (string.IsNullOrEmpty(displayText)) return;
            if (!_ajsCategoryKeys.TryGetValue(displayText, out var key)) return;
            var keyParts = key.Split('-');
            if (keyParts.Length != 2) return;
            var kbnNo   = keyParts[0];
            var roundNo = keyParts[1];

            var item = _currentAjsProgressItems[_currentAjsIndex];

            // 現在表示中の画面がこの item に対応するものかチェック
            // tag（AjsProgressItem の参照）が一致しない場合は新しい画面を生成する
            var currentScreen = _offScreenWindow?.CurrentScreen as DSDspScreenBase;
            bool isNewScreen = (currentScreen == null || _offScreenWindow?.CurrentScreenTag != (object)item);

            if (isNewScreen)
            {
                // 画面インスタンスを生成してデータを注入
                var screen = CreateAjsScreen(item.ScreenId);
                if (screen == null)
                {
                    _log?.LogAdd($"未対応の画面ID: {item.ScreenId}", _log.WARNING);
                    MessageBox.Show($"未対応の画面ID: {item.ScreenId}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;
                if (dm?.DA_Master != null) screen.DA_Master = dm.DA_Master;
                if (dm?.DS_Status != null) screen.DS_Status = dm.DS_Status;
                if (dm?.DV_Result != null) screen.DV_Result = dm.DV_Result;

                screen.区分番号          = kbnNo;
                screen.ラウンド番号       = roundNo;
                screen.種目番号          = item.DanceNo;
                screen.ヒート番号        = item.HeatNo;
                screen.IsOverviewMode    = item.IsOverviewMode;
                screen.IsLastHeatInDance = item.IsLastHeatInDance;
                screen.ChromaKeyMode     = _currentAjsScenario?.ChromaKeyMode ?? false;

                // ScreenCompleted を受け取ったら次の画面へ進む
                screen.ScreenCompleted += OnAjsScreenCompleted;

                _offScreenWindow?.ShowScreen(screen, item);
                _log?.LogAdd($"AJS画面表示: {item.ScreenId} 種目{item.DanceNo} ヒート{item.HeatNo}", _log.INFO);

                currentScreen = screen;
            }

            if (currentScreen == null) return;

            _log?.LogAdd($"AJS Advance: {item.ScreenId} Step={currentScreen.CurrentStep}", _log.INFO);
            currentScreen.Advance();
        }

        /// <summary>
        /// ScreenCompleted 受信時の処理：次のAJS画面へ遷移する。
        /// </summary>
        private void OnAjsScreenCompleted(object? sender, EventArgs e)
        {
            if (sender is DSDspScreenBase completedScreen)
                completedScreen.ScreenCompleted -= OnAjsScreenCompleted;

            _currentAjsIndex++;
            if (_currentAjsProgressItems == null || _currentAjsIndex >= _currentAjsProgressItems.Count)
            {
                _log?.LogAdd("AJS: すべての画面が完了しました", _log.INFO);
                return;
            }

            // リストの選択を次の項目に移す
            _suppressAjsSelectionChanged = true;
            LstAjsProgress.SelectedIndex = _currentAjsIndex;
            _suppressAjsSelectionChanged = false;
            UpdateResultReadyLabel();
            _log?.LogAdd($"AJS: 次の画面へ移動 Index={_currentAjsIndex}", _log.INFO);

            // HoldsAfterFadeOut=true の画面（DSP_GRP_001 等）はここで停止。
            // 次の再生ボタンで ExecuteAjsStep() が呼ばれ、新画面を生成して表示する。
            // それ以外の画面は次の画面を即時表示・Advance() して Step0 を実行する。
            if (sender is DSDspScreenBase s && s.HoldsAfterFadeOut)
            {
                s.OnHoldsAfterFadeOut();
                _log?.LogAdd($"AJS: HoldsAfterFadeOut 停止 Index={_currentAjsIndex}", _log.INFO);
                return;
            }

            // 即時遷移：次の画面を生成して Step0 (Advance) を実行
            ExecuteAjsStep();
        }

        /// <summary>
        /// AJS SUBタブのステップを実行する。
        /// </summary>
        private void ExecuteAjsSubStep()
        {
            EnsureOffScreenWindowCreated();

            if (_currentAjsSubProgressItems == null || _currentAjsSubIndex < 0 || _currentAjsSubIndex >= _currentAjsSubProgressItems.Count)
            {
                _log?.LogAdd("AJS SUB項目が選択されていません", _log.WARNING);
                return;
            }

            if (CmbAjsCategory.SelectedItem == null) return;
            var displayText = CmbAjsCategory.SelectedItem.ToString();
            if (string.IsNullOrEmpty(displayText)) return;
            if (!_ajsCategoryKeys.TryGetValue(displayText, out var key)) return;
            var keyParts = key.Split('-');
            if (keyParts.Length != 2) return;
            var kbnNo   = keyParts[0];
            var roundNo = keyParts[1];

            var item = _currentAjsSubProgressItems[_currentAjsSubIndex];

            var currentSubScreen = _offScreenWindow?.CurrentSubScreen as DSDspScreenBase;
            bool isNewScreen = (currentSubScreen == null || _offScreenWindow?.CurrentSubScreenTag != (object)item);

            if (isNewScreen)
            {
                var screen = CreateAjsScreen(item.ScreenId);
                if (screen == null)
                {
                    _log?.LogAdd($"SUB: 未対応の画面ID: {item.ScreenId}", _log.WARNING);
                    MessageBox.Show($"SUB: 未対応の画面ID: {item.ScreenId}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;
                if (dm?.DA_Master != null) screen.DA_Master = dm.DA_Master;
                if (dm?.DS_Status != null) screen.DS_Status = dm.DS_Status;
                if (dm?.DV_Result != null) screen.DV_Result = dm.DV_Result;

                screen.区分番号       = kbnNo;
                screen.ラウンド番号    = roundNo;
                screen.種目番号       = item.DanceNo;
                screen.ヒート番号     = item.HeatNo;
                screen.IsOverviewMode = item.IsOverviewMode;

                screen.ScreenCompleted += OnAjsSubScreenCompleted;

                _offScreenWindow?.ShowSubScreen(screen, item);
                _log?.LogAdd($"AJS SUB画面表示: {item.ScreenId}", _log.INFO);

                currentSubScreen = screen;
            }

            if (currentSubScreen == null) return;

            _log?.LogAdd($"AJS SUB Advance: {item.ScreenId} Step={currentSubScreen.CurrentStep}", _log.INFO);
            currentSubScreen.Advance();
        }

        /// <summary>
        /// SUB ScreenCompleted 受信時の処理：次のSUB画面へ遷移する。
        /// </summary>
        private void OnAjsSubScreenCompleted(object? sender, EventArgs e)
        {
            if (sender is DSDspScreenBase completedScreen)
                completedScreen.ScreenCompleted -= OnAjsSubScreenCompleted;

            _currentAjsSubIndex++;
            if (_currentAjsSubProgressItems == null || _currentAjsSubIndex >= _currentAjsSubProgressItems.Count)
            {
                _log?.LogAdd("AJS SUB: すべての画面が完了しました", _log.INFO);
                return;
            }

            _suppressAjsSubSelectionChanged = true;
            LstAjsSubProgress.SelectedIndex = _currentAjsSubIndex;
            _suppressAjsSubSelectionChanged = false;
            _log?.LogAdd($"AJS SUB: 次の画面へ移動 Index={_currentAjsSubIndex}", _log.INFO);

            if (sender is DSDspScreenBase s && s.HoldsAfterFadeOut)
            {
                s.OnHoldsAfterFadeOut();
                return;
            }

            ExecuteAjsSubStep();
        }

        /// <summary>
        /// 画面ID から DSDspScreenBase インスタンスを生成する。
        /// </summary>
        private static DSDspScreenBase? CreateAjsScreen(string screenId) => screenId switch
        {
            "DSP_TIT_001" => new 画面.DSP_TIT_001_区分ラウンド紹介(),
            "DSP_TIT_002" => new 画面.DSP_TIT_002_種目紹介大(),
            "DSP_TIT_003" => new 画面.DSP_TIT_003_種目紹介小(),
            "DSP_SOL_001" => new 画面.DSP_SOL_001_ソロ選手紹介_大(),
            "DSP_SOL_002" => new 画面.DSP_SOL_002_ソロ選手紹介_小(),
            "DSP_SOL_003" => new 画面.DSP_SOL_003_ソロ選手結果GD_大(),
            "DSP_SOL_004" => new 画面.DSP_SOL_004_ソロ選手結果GD_小(),
            "DSP_SOL_005" => new 画面.DSP_SOL_005_ソロ選手結果PD_大(),
            "DSP_SOL_006" => new 画面.DSP_SOL_006_ソロ選手結果PD_小(),
            "DSP_SOL_007" => new 画面.DSP_SOL_007_ソロ途中結果_大(),
            "DSP_SOL_008" => new 画面.DSP_SOL_008_ソロ途中結果_小(),
            "DSP_GRP_001" => new 画面.DSP_GRP_001_出場選手一覧_大(),
            "DSP_GRP_002" => new 画面.DSP_GRP_002_出場選手一覧_小(),
            "DSP_GRP_003" => new 画面.DSP_GRP_003_結果一覧_大(),
            "DSP_GRP_004" => new 画面.DSP_GRP_004_結果一覧_小(),
            "DSP_DUE_001" => new 画面.DSP_DUE_001_DUE選手紹介_大(),
            "DSP_DUE_002" => new 画面.DSP_DUE_002_DUE選手紹介_小(),
            "DSP_DUE_003" => new 画面.DSP_DUE_003_DUE選手結果_大(),
            "DSP_DUE_004" => new 画面.DSP_DUE_004_DUE選手結果_小(),
            "DSP_COM_001" => new 画面.DSP_COM_001_総合結果一覧_大(),
            "DSP_COM_002" => new 画面.DSP_COM_002_総合結果一覧_小(),
            "DSP_TIT_999" => new 画面.DSP_TIT_999_終了(),
            _ => null
        };

        /// <summary>表彰式タブのステップを実行</summary>
        private void ExecuteAwardStep()
        {
            if (_awardSelectedCategory == null && !_awardSelectedIsAwardTitle && !_awardSelectedIsAwardEnd)
            {
                MessageBox.Show("区分を選択してください", "表彰式", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            EnsureOffScreenWindowCreated();
            if (_offScreenWindow == null) return;

            var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;

            // 終了画面
            if (_awardSelectedIsAwardEnd)
            {
                const string endScreenId = "DSP_TIT_999";
                bool isSameEndScreen = _currentAwardScreen?.ScreenId == endScreenId;

                if (!isSameEndScreen)
                {
                    if (_currentAwardScreen != null)
                        _currentAwardScreen.ScreenCompleted -= OnAwardScreenCompleted;

                    var endScreen = new 画面.DSP_TIT_999_終了();
                    endScreen.ScreenId  = endScreenId;
                    endScreen.DA_Master = dm?.DA_Master;
                    endScreen.DS_Status = dm?.DS_Status;
                    _currentAwardScreen = endScreen;
                    _offScreenWindow.ShowScreen(endScreen, endScreenId);
                    _log?.LogAdd($"表彰式終了画面表示: {endScreenId}", _log.INFO);
                }

                _currentAwardScreen!.Advance();
                UpdateAwardStatus($"表彰式終了  Step={_currentAwardScreen.CurrentStep}");
                return;
            }

            // タイトル画面
            if (_awardSelectedIsAwardTitle)
            {
                const string titleScreenId = "DSP_PRG_011";
                bool isSameTitleScreen = _currentAwardScreen?.ScreenId == titleScreenId;

                if (!isSameTitleScreen)
                {
                    if (_currentAwardScreen != null)
                        _currentAwardScreen.ScreenCompleted -= OnAwardScreenCompleted;

                    var titleScreen = new 画面.DSP_PRG_011_タイトル紹介();
                    titleScreen.ScreenId       = titleScreenId;
                    titleScreen.DA_Master      = dm?.DA_Master;
                    titleScreen.DS_Status      = dm?.DS_Status;
                    titleScreen.Title1Override = "表彰式";
                    titleScreen.Title2Override = string.Empty;
                    titleScreen.ScreenCompleted += OnAwardScreenCompleted;
                    _currentAwardScreen = titleScreen;
                    _offScreenWindow.ShowScreen(titleScreen, titleScreenId);
                    _log?.LogAdd($"表彰式タイトル画面表示: {titleScreenId}", _log.INFO);
                }

                _currentAwardScreen!.Advance();
                UpdateAwardStatus($"表彰式タイトル  Step={_currentAwardScreen.CurrentStep}");
                return;
            }

            if (_awardSelectedCategory == null) return;
    
                var (kbnNo, rndNo, _, _) = _awardSelectedCategory.Value;
                var dvResult = GetDvResultFor(kbnNo, rndNo);
    
                // 画面IDを決定
                // 全画面 → DSP_PRG_008
                // クロマキ_リスト → DSP_PRG_009
                // クロマキ_個別（昇順/降順） → DSP_PRG_010
                // クロマキ_個別（一括） → DSP_PRG_008（クロマキ背景）
                bool isChromaIndividualPage = (_awardDisplay == AwardDisplayMode.ChromaIndividual
                                               && _awardOrder == AwardOrderMode.Page);
                string screenId;
                if (_awardDisplay == AwardDisplayMode.ChromaList)
                    screenId = "DSP_PRG_009";
                else if (_awardDisplay == AwardDisplayMode.ChromaIndividual && !isChromaIndividualPage)
                    screenId = "DSP_PRG_010";
                else
                    screenId = "DSP_PRG_008";

                bool isSameScreen = _currentAwardScreen?.ScreenId == screenId
                    && _currentAwardScreen?.区分番号 == kbnNo
                    && _currentAwardScreen?.ラウンド番号 == rndNo;

                // クロマキ表示かどうか（DisplayWindow の背景色に反映）
                bool isChromaMode = (_awardDisplay == AwardDisplayMode.ChromaList)
                                 || (_awardDisplay == AwardDisplayMode.ChromaIndividual);
                ApplyAwardWindowBackground(isChromaMode);

                if (!isSameScreen)
                {
                    if (_currentAwardScreen != null)
                        _currentAwardScreen.ScreenCompleted -= OnAwardScreenCompleted;

                    画面.DSDspScreenBase? newScreen = screenId switch
                    {
                        "DSP_PRG_009" => new 画面.DSP_PRG_009_決勝結果_小(),
                        "DSP_PRG_010" => new 画面.DSP_PRG_010_選手紹介_小(),
                        _ => new 画面.DSP_PRG_008_決勝結果_大()
                    };

                    newScreen.ScreenId    = screenId;
                    newScreen.DA_Master   = dm?.DA_Master;
                    newScreen.DS_Status   = dm?.DS_Status;
                    newScreen.DV_Result   = dvResult;
                    newScreen.区分番号    = kbnNo;
                    newScreen.ラウンド番号 = rndNo;

                    if (newScreen is 画面.DSP_PRG_008_決勝結果_大 s008)
                    {
                        s008.昇順表示  = (_awardOrder != AwardOrderMode.Desc);
                        s008.IsPageMode = (_awardOrder == AwardOrderMode.Page);
                        // クロマキ個別_一括: DSP_PRG_008にクロマキ背景を設定
                        if (isChromaIndividualPage)
                        {
                            try
                            {
                                var colorStr = AppSettings.Instance.ChromaKeySettings.BackgroundColor;
                                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                                s008.Background = new System.Windows.Media.SolidColorBrush(color);
                            }
                            catch { }
                        }
                    }
                    else if (newScreen is 画面.DSP_PRG_009_決勝結果_小 s009)
                    {
                        s009.IsPageMode = (_awardOrder == AwardOrderMode.Page);
                        s009.昇順表示  = (_awardOrder != AwardOrderMode.Desc);
                    }
                    else if (newScreen is 画面.DSP_PRG_010_選手紹介_小 s010)
                    {
                        s010.順位番号 = (_awardOrder == AwardOrderMode.Desc) ? GetMaxRank(dvResult) : 1;
                    }

                    newScreen.ScreenCompleted += OnAwardScreenCompleted;
                    _currentAwardScreen = newScreen;
                    _offScreenWindow.ShowScreen(newScreen, screenId);
                    _log?.LogAdd($"表彰式画面表示: {screenId}  KbnNo={kbnNo}", _log.INFO);
                }
    
                var screenForAdvance = _currentAwardScreen;
                screenForAdvance!.Advance();
                UpdateAwardStatus(_currentAwardScreen != null
                    ? $"表示中  Step={_currentAwardScreen.CurrentStep}"
                    : "画面終了");
        }

        private void OnAwardScreenCompleted(object? sender, EventArgs e)
        {
            if (sender is 画面.DSDspScreenBase s)
                s.ScreenCompleted -= OnAwardScreenCompleted;

            // クロマキ個別（DSP_PRG_010）: 次の選手に進む
            if (sender is 画面.DSP_PRG_010_選手紹介_小 s010 && _awardSelectedCategory != null)
            {
                Dispatcher.Invoke(() =>
                {
                    bool isAsc = (_awardOrder != AwardOrderMode.Desc);
                    int nextRank = isAsc ? s010.順位番号 + 1 : s010.順位番号 - 1;
                    var dvResult = GetDvResultFor(_awardSelectedCategory.Value.KbnNo, _awardSelectedCategory.Value.RndNo);
                    int maxRank = GetMaxRank(dvResult);
                    bool hasNext = isAsc ? nextRank <= maxRank : nextRank >= 1;

                    if (hasNext && _offScreenWindow != null)
                    {
                        var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;
                        var newS010 = new 画面.DSP_PRG_010_選手紹介_小();
                        newS010.ScreenId    = "DSP_PRG_010";
                        newS010.DA_Master   = dm?.DA_Master;
                        newS010.DS_Status   = dm?.DS_Status;
                        newS010.DV_Result   = dvResult;
                        newS010.区分番号    = _awardSelectedCategory.Value.KbnNo;
                        newS010.ラウンド番号 = _awardSelectedCategory.Value.RndNo;
                        newS010.順位番号    = nextRank;
                        newS010.ScreenCompleted += OnAwardScreenCompleted;
                        _currentAwardScreen = newS010;
                        _offScreenWindow.ShowScreen(newS010, "DSP_PRG_010");
                        newS010.Advance();
                        _log?.LogAdd($"クロマキ個別 次選手表示: 順位={nextRank}", _log.INFO);
                        UpdateAwardStatus($"クロマキ個別  順位={nextRank}");
                    }
                    else
                    {
                        _currentAwardScreen = null;
                        UpdateAwardStatus("画面終了");
                    }
                });
                return;
            }

            _currentAwardScreen = null;
            Dispatcher.Invoke(() => UpdateAwardStatus("画面終了"));
        }

        /// <summary>DV_Result から最大順位番号を返す</summary>
        private static int GetMaxRank(System.Text.Json.Nodes.JsonNode? dvResult)
        {
            if (dvResult == null) return 1;
            var 結果リスト = 画面.DSDspDataHelper.Get総合結果リスト(dvResult);
            return 結果リスト.Count > 0 ? 結果リスト.Max(r => r.順位番号) : 1;
        }

        private System.Text.Json.Nodes.JsonNode? GetDvResultFor(string kbnNo, string rndNo)
        {
            var dvResult = _testDataManager?.DV_Result ?? _client?.DataManager.DV_Result;
            if (dvResult == null) return null;

            if (dvResult is System.Text.Json.Nodes.JsonArray arr)
            {
                foreach (var el in arr)
                {
                    if (el?["区分番号"]?.ToString() == kbnNo &&
                        el?["ラウンド番号"]?.ToString() == rndNo)
                        return el;
                }
                return null;
            }
            return dvResult;
        }

        private void UpdateAwardStatus(string text)
        {
            if (TxtAwardStatus != null)
                TxtAwardStatus.Text = text;
        }

        /// <summary>
        /// 表彰式タブのクロマキ表示モード切替に合わせて、すべての DisplayWindow の背景色を設定する。
        /// クロマキ時は ChromaKeySettings の色、全画面時は黒（デフォルト）に戻す。
        /// </summary>
        private void ApplyAwardWindowBackground(bool isChromaMode)
        {
            void Apply(DisplayWindow? window)
            {
                if (window == null) return;
                if (isChromaMode)
                {
                    try
                    {
                        var colorStr = AppSettings.Instance.ChromaKeySettings.BackgroundColor;
                        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                        window.SetBackgroundColor(color.R, color.G, color.B);
                    }
                    catch { window.ClearBackground(); }
                }
                else
                {
                    window.ClearBackground();
                }
            }

            Apply(_offScreenWindow);
            Apply(_displayWindow);
            Apply(_fullScreenWindow);
        }

        #endregion

        #region 画面インスタンス生成

        /// <summary>
        /// 既存の画面と同じ種別・同じデータで新しい画面インスタンスを生成する。
        /// スクリーン（全画面）ウィンドウへのミラー表示に使用。
        /// </summary>
        private 画面.DSDspScreenBase? CreateScreenInstance(画面.DSDspScreenBase source)
        {
            画面.DSDspScreenBase? dest = source switch
            {
                画面.DSP_TIT_001_区分ラウンド紹介    => new 画面.DSP_TIT_001_区分ラウンド紹介(),
                画面.DSP_TIT_002_種目紹介大          => new 画面.DSP_TIT_002_種目紹介大(),
                画面.DSP_SOL_001_ソロ選手紹介_大     => new 画面.DSP_SOL_001_ソロ選手紹介_大(),
                画面.DSP_SOL_002_ソロ選手紹介_小     => new 画面.DSP_SOL_002_ソロ選手紹介_小(),
                画面.DSP_SOL_003_ソロ選手結果GD_大   => new 画面.DSP_SOL_003_ソロ選手結果GD_大(),
                画面.DSP_SOL_004_ソロ選手結果GD_小   => new 画面.DSP_SOL_004_ソロ選手結果GD_小(),
                画面.DSP_SOL_005_ソロ選手結果PD_大   => new 画面.DSP_SOL_005_ソロ選手結果PD_大(),
                画面.DSP_SOL_006_ソロ選手結果PD_小   => new 画面.DSP_SOL_006_ソロ選手結果PD_小(),
                画面.DSP_SOL_007_ソロ途中結果_大     => new 画面.DSP_SOL_007_ソロ途中結果_大(),
                画面.DSP_SOL_008_ソロ途中結果_小     => new 画面.DSP_SOL_008_ソロ途中結果_小(),
                画面.DSP_GRP_001_出場選手一覧_大     => new 画面.DSP_GRP_001_出場選手一覧_大(),
                画面.DSP_GRP_002_出場選手一覧_小     => new 画面.DSP_GRP_002_出場選手一覧_小(),
                画面.DSP_GRP_003_結果一覧_大         => new 画面.DSP_GRP_003_結果一覧_大(),
                画面.DSP_GRP_004_結果一覧_小         => new 画面.DSP_GRP_004_結果一覧_小(),
                画面.DSP_COM_001_総合結果一覧_大     => new 画面.DSP_COM_001_総合結果一覧_大(),
                画面.DSP_COM_002_総合結果一覧_小     => new 画面.DSP_COM_002_総合結果一覧_小(),
                画面.DSP_DUE_001_DUE選手紹介_大      => new 画面.DSP_DUE_001_DUE選手紹介_大(),
                画面.DSP_DUE_002_DUE選手紹介_小      => new 画面.DSP_DUE_002_DUE選手紹介_小(),
                画面.DSP_DUE_003_DUE選手結果_大      => new 画面.DSP_DUE_003_DUE選手結果_大(),
                画面.DSP_DUE_004_DUE選手結果_小      => new 画面.DSP_DUE_004_DUE選手結果_小(),
                画面.DSP_TIT_999_終了                => new 画面.DSP_TIT_999_終了(),
                _ => null
            };

            if (dest != null)
            {
                dest.DA_Master   = source.DA_Master;
                dest.DS_Status   = source.DS_Status;
                dest.DV_Result   = source.DV_Result;
                dest.区分番号    = source.区分番号;
                dest.ラウンド番号 = source.ラウンド番号;
                dest.種目番号    = source.種目番号;
                dest.ヒート番号  = source.ヒート番号;
            }

            return dest;
        }

        #endregion

        #region イベントハンドラ

        private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!e.IsConnected)
                {
                    UpdateConnectionStatus("切断", Brushes.Red);
                    BtnConnect.Content = "サーバー接続";

                    // 手動切断でなければ自動再接続を試みる
                    if (!_isManualDisconnect)
                        StartAutoReconnect();
                }
            });
        }

        /// <summary>
        /// 5秒間隔で自動再接続を試みる。接続成功または手動切断フラグが立つまで繰り返す。
        /// </summary>
        private async void StartAutoReconnect()
        {
            _log?.LogAdd("自動再接続ループ開始", _log.INFO);

            while (!_isManualDisconnect)
            {
                await System.Threading.Tasks.Task.Delay(5000);

                if (_isManualDisconnect) break;
                if (_client != null && _client.IsConnected) break;

                _log?.LogAdd("自動再接続試行...", _log.INFO);
                Dispatcher.Invoke(() => UpdateConnectionStatus("再接続中...", Brushes.Orange));

                try
                {
                    // 既存の壊れたクライアントを破棄して新規作成
                    if (_client != null)
                    {
                        _client.ConnectionStateChanged -= OnConnectionStateChanged;
                        _client.DA_MasterReceived -= OnDA_MasterReceived;
                        _client.DS_StatusReceived -= OnDS_StatusReceived;
                        _client.DV_ResultReceived -= OnDV_ResultReceived;
                        _client.ErrorReceived -= OnErrorReceived;
                        _client.HeatEndNotifyReceived -= OnHeatEndNotifyReceived;
                        _client.Dispose();
                        _client = null;
                    }

                    _client = new DSDspClient();
                    _client.ConnectionStateChanged += OnConnectionStateChanged;
                    _client.DA_MasterReceived += OnDA_MasterReceived;
                    _client.DS_StatusReceived += OnDS_StatusReceived;
                    _client.DV_ResultReceived += OnDV_ResultReceived;
                    _client.ErrorReceived += OnErrorReceived;
                    _client.HeatEndNotifyReceived += OnHeatEndNotifyReceived;
                    _client.CompetitionSelector = OnSelectCompetitionAsync;

                    bool connected = await _client.ConnectAsync();
                    if (connected)
                    {
                        bool initialized = await _client.InitializeAsync();
                        if (initialized)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                UpdateConnectionStatus("接続済み", Brushes.LimeGreen);
                                BtnConnect.Content = "切断";
                            });
                            _log?.LogAdd("自動再接続成功", _log.INFO);
                            break;
                        }
                        else
                        {
                            _log?.LogAdd("自動再接続後の初期化失敗", _log.WARNING);
                            _client?.Dispose();
                            _client = null;
                        }
                    }
                    else
                    {
                        _log?.LogAdd("自動再接続失敗（接続不可）", _log.WARNING);
                        _client?.Dispose();
                        _client = null;
                    }
                }
                catch (Exception ex)
                {
                    _log?.LogAdd($"自動再接続エラー: {ex.Message}", _log.ERR);
                    _client?.Dispose();
                    _client = null;
                }

                if (!_isManualDisconnect)
                    Dispatcher.Invoke(() => UpdateConnectionStatus("切断（再接続待機中）", Brushes.Red));
            }

            _log?.LogAdd("自動再接続ループ終了", _log.INFO);
        }

        private void OnDA_MasterReceived(object? sender, EventArgs e)
        {
            if (_client?.DataManager.DA_Master == null) return;
            
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // DataManagerで設定されたCmpNoを使用
                    var cmpNo = _client.DataManager.CmpNo ?? "";
                    
                    // DA_CompNameフィールドから競技会名を取得
                    var cmpName = _client.DataManager.DA_Master["DA_CompName"]?.ToString() ?? "";
                    
                    _log?.LogAdd($"DA_Master受信: CmpNo={cmpNo}, CompName={cmpName}", _log.INFO);
                    
                    if (!string.IsNullOrEmpty(cmpNo) && !string.IsNullOrEmpty(cmpName))
                    {
                        TxtCompetitionInfo.Text = $"競技会NO: {cmpNo}  {cmpName}";
                        _log?.LogAdd($"競技会情報表示: {cmpNo} - {cmpName}", _log.INFO);
                    }
                    else if (!string.IsNullOrEmpty(cmpName))
                    {
                        // 競技会番号がない場合は名前のみ表示
                        TxtCompetitionInfo.Text = cmpName;
                        _log?.LogAdd($"競技会情報表示: {cmpName}", _log.INFO);
                    }
                    else
                    {
                        _log?.LogAdd("競技会情報が取得できませんでした", _log.WARNING);
                    }

                    // AJSタブの区分選択にAJS採点方式の区分をリスト
                    if (_scenarioManager != null && _client.DataManager.DA_Master != null)
                    {
                        var ajsCategories = _scenarioManager.GetAjsCategoriesFromDaMaster(_client.DataManager.DA_Master);
                        
                        if (ajsCategories.Count > 0)
                        {
                            // Dictionaryをクリアして再構築
                            _ajsCategoryKeys.Clear();
                            var displayTexts = new List<string>();
                            
                            foreach (var category in ajsCategories)
                            {
                                // 形式: "区分No-ラウンドNo|区分番号 区分名 ラウンド名"
                                var parts = category.Split('|');
                                if (parts.Length == 2)
                                {
                                    var key = parts[0];           // "区分No-ラウンドNo"
                                    var displayText = parts[1];   // "区分番号 区分名 ラウンド名"
                                    _ajsCategoryKeys[displayText] = key;
                                    displayTexts.Add(displayText);
                                }
                            }
                            
                            CmbAjsCategory.ItemsSource = displayTexts;
                            // 初期選択はしない（シナリオ選択後に自動選択される）
                            _log?.LogAdd($"AJS区分を設定: {displayTexts.Count}件", _log.INFO);
                        }
                        else
                        {
                            _log?.LogAdd("AJS採点方式の区分が見つかりませんでした", _log.WARNING);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log?.LogAdd($"競技会情報表示エラー: {ex.Message}", _log.ERR);
                }
            });
        }

        private void OnDS_StatusReceived(object? sender, EventArgs e)
        {
            if (_client?.DataManager.DS_Status == null) return;
            var version = _client.DataManager.DS_StatusVersion;
            _log?.LogAdd($"DS_Status受信: Version={version}", _log.INFO);
        }

        /// <summary>
        /// MC_HEAT_NOTIFY（END）受信時の進行画面自動遷移ハンドラ。
        ///
        /// 処理方針:
        ///   1. 通知された進行番号（PrgNo）と一致する ProgressListItem を探す。
        ///      現在表示中の進行が異なる場合でも電文の内容を優先し、
        ///      一致する進行にカーソルを移動する。
        ///   2. 次の表示対象を決定する:
        ///      a) 終了したのが同一進行内の途中ヒート
        ///            → 現在の進行画面に Advance()（ヒート毎更新モード）
        ///      b) 進行内の最終ヒート（かつ最終種目）
        ///            → _currentProgressIndex を次に進め、新しい進行を ExecuteProgressStep() で表示
        ///   3. _autoProgress が OFF のときは実行しない。
        /// </summary>
        private void OnHeatEndNotifyReceived(object? sender, Handlers.HeatEndNotifyEventArgs e)
        {
            if (!_autoProgress) return;

            Dispatcher.Invoke(() =>
            {
                var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;
                if (dm?.DS_Status == null) return;

                var items = LstProgressItems.ItemsSource as System.Collections.Generic.List<ProgressListItem>;
                if (items == null || items.Count == 0) return;

                _log?.LogAdd(
                    $"HeatEnd通知: floor={e.FloorCd}, prg={e.PrgNo}, dance={e.DncNo}, heat={e.HeatNo}",
                    _log.INFO);

                // ── 1. 電文の進行番号に対応する ProgressListItem を探す ──────────
                // DS_Status の PrgNo（DS_PrgNo）は ProgressListItem の PrgNo と一致する。
                // 区分・ラウンド単位でデdup しているため、同一区分ラウンドの最初の PrgNo と比較。
                // → DS_Status から kbnNo / rndNo を引いて items を絞り込む。
                var floors = dm.DS_Status["DS_FLOORs"]?.AsArray();
                if (floors == null) return;

                string kbnNo = "";
                string rndNo = "";
                foreach (var floor in floors)
                {
                    if (floor?["DS_FlrCd"]?.ToString() != e.FloorCd) continue;
                    var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                    if (prgrs == null) break;
                    foreach (var prg in prgrs)
                    {
                        if (prg?["DS_PrgNo"]?.ToString() == e.PrgNo)
                        {
                            kbnNo = prg?["DS_KbnNo"]?.ToString() ?? "";
                            rndNo = prg?["DS_RndNo"]?.ToString() ?? "";
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(kbnNo)) break;
                }

                if (string.IsNullOrEmpty(kbnNo)) return;

                // ProgressListItem は kbnNo + rndNo で一意
                int targetIdx = items.FindIndex(x => x.KbnNo == kbnNo && x.RndNo == rndNo);
                if (targetIdx < 0) return;

                // 現在の選択が通知と異なる場合はカーソルを電文の進行に合わせる（電文優先）
                if (_currentProgressIndex != targetIdx)
                {
                    _currentProgressIndex = targetIdx;
                    LstProgressItems.SelectedIndex = targetIdx;
                    _log?.LogAdd($"HeatEnd: 進行カーソルを電文に合わせて移動 → Index={targetIdx}", _log.INFO);
                }

                // ── 2. 次ヒートがあるか判定 ──────────────────────────────────────
                var nextHeat = 画面.DSDspDataHelper.Get次ヒート情報(
                    dm.DS_Status, dm.DA_Master, kbnNo, rndNo, e.DncNo, e.HeatNo);

                if (nextHeat.HasValue)
                {
                    // 2a. 同一進行内に次ヒートが存在 → 現在の進行画面に Advance()
                    // 現在の進行画面がこの進行に対応していない場合は新規生成してから Advance
                    EnsureOffScreenWindowCreated();
                    if (_offScreenWindow == null) return;

                    var item     = items[targetIdx];
                    var screenId = GetProgressScreenId();
                    bool isSameScreen = _currentProgressScreen != null
                        && _currentProgressScreenId == screenId
                        && _currentProgressScreen.区分番号  == item.KbnNo
                        && _currentProgressScreen.ラウンド番号 == item.RndNo;

                    if (!isSameScreen)
                    {
                        // DS_Status が更新されているのでデータを注入して新規生成
                        if (_currentProgressScreen != null)
                            _currentProgressScreen.ScreenCompleted -= OnProgressScreenCompleted;

                        画面.DSDspScreenBase? newScreen = screenId switch
                        {
                            "DSP_PRG_002" => new 画面.DSP_PRG_002_進行表示1面_小(),
                            "DSP_PRG_004" => new 画面.DSP_PRG_004_進行表示ヒート表_大(),
                            "DSP_PRG_005" => new 画面.DSP_PRG_005_進行表示ヒート表_小(),
                            "DSP_PRG_006" => new 画面.DSP_PRG_006_決勝進出者_大(),
                            "DSP_PRG_007" => new 画面.DSP_PRG_007_決勝進出者_小(),
                            _             => new 画面.DSP_PRG_001_進行表示1面_大(),
                        };

                        newScreen.ScreenId      = screenId;
                        newScreen.DA_Master     = dm.DA_Master;
                        newScreen.DS_Status     = dm.DS_Status;
                        newScreen.DV_Result     = dm.DV_Result;
                        newScreen.区分番号      = item.KbnNo;
                        newScreen.ラウンド番号   = item.RndNo;
                        newScreen.ScreenCompleted += OnProgressScreenCompleted;

                        _currentProgressScreen   = newScreen;
                        _currentProgressScreenId = screenId;
                        _offScreenWindow.ShowScreen(newScreen, item.KbnNo);
                        _log?.LogAdd($"HeatEnd: 進行画面再生成 {screenId} KbnNo={item.KbnNo} RndNo={item.RndNo}", _log.INFO);
                    }
                    else
                    {
                        // 既存画面の DS_Status を最新に更新
                        _currentProgressScreen!.DS_Status = dm.DS_Status;
                    }

                    _currentProgressScreen!.Advance();
                    _log?.LogAdd(
                        $"HeatEnd: 次ヒートへ Advance (dance={nextHeat.Value.DncNo}, heat={nextHeat.Value.HeatNo})",
                        _log.INFO);
                }
                else
                {
                    // 2b. 進行内の全ヒート終了 → 次の進行へ
                    int nextIdx = targetIdx + 1;
                    if (nextIdx < items.Count)
                    {
                        _currentProgressIndex = nextIdx;
                        LstProgressItems.SelectedIndex = nextIdx;
                        _currentProgressScreen = null;
                        _currentProgressScreenId = string.Empty;

                        // 次の進行を即時表示（STEP1 から実行）
                        EnsureOffScreenWindowCreated();
                        ExecuteProgressStep();
                        _log?.LogAdd($"HeatEnd: 次の進行へ自動遷移 Index={nextIdx}", _log.INFO);
                    }
                    else
                    {
                        _log?.LogAdd("HeatEnd: 全進行が終了しました", _log.INFO);
                    }
                }
            });
        }

        private void OnDV_ResultReceived(object? sender, EventArgs e)
        {
            _log?.LogAdd("DV_Result受信: 結果状態ラベル更新", _log.DEBUG);
            Dispatcher.Invoke(() =>
            {
                UpdateResultReadyLabel();

                // 表彰式タブが選択中であれば、区分選択済みステータスを更新してプレビューを表示
                if (TabControl.SelectedIndex == 2 && _awardSelectedCategory != null)
                {
                    UpdateAwardStatus("区分選択済み — 再生ボタンで表示");
                    UpdateAwardPreview(_awardSelectedCategory.Value.KbnNo, _awardSelectedCategory.Value.RndNo);
                }

                // オナーダンスタブが選択中であれば、入賞者リストを更新する
                if (TabControl.SelectedIndex == 3 && _honorSelectedCategory != null)  // ← オナーダンスは index=3
                {
                    UpdateHonorStatus("区分選択済み — 入賞者を選択してください");
                    LoadHonorPlayerList(_honorSelectedCategory.Value.KbnNo, _honorSelectedCategory.Value.RndNo);
                }
            });
        }

        /// <summary>結果プレビューリストを更新する</summary>
        private void UpdateAwardPreview(string kbnNo, string rndNo)
        {
            var dvResult = GetDvResultFor(kbnNo, rndNo);
            var daMaster = _testDataManager?.DA_Master ?? _client?.DataManager.DA_Master;

            if (dvResult == null || daMaster == null)
            {
                AwardPreviewList.ItemsSource = null;
                return;
            }

            var 総合リスト = 画面.DSDspDataHelper.Get総合結果リスト(dvResult);
            var items = new List<AwardPreviewItem>();

            foreach (var (rankNo, bango, score, rankStr) in 総合リスト)
            {
                var 選手情報 = 画面.DSDspDataHelper.Get選手情報(daMaster, bango, kbnNo);
                string 選手名L = 画面.DSDspDataHelper.Get選手名L(選手情報);
                string 選手名P = 画面.DSDspDataHelper.Get選手名P(選手情報);
                string 選手名  = string.IsNullOrEmpty(選手名P) ? 選手名L : $"{選手名L}・{選手名P}";
                string 所属   = 画面.DSDspDataHelper.Get所属(選手情報);
                string 順位   = 画面.DSDspDataHelper.Format順位テキスト(rankNo, rankStr);

                items.Add(new AwardPreviewItem
                {
                    順位   = 順位,
                    背番号 = bango,
                    選手名 = 選手名,
                    所属   = 所属,
                    得点   = score > 0 ? score.ToString("F3") : string.Empty,
                });
            }

            AwardPreviewList.ItemsSource = items;
            _log?.LogAdd($"結果プレビュー更新: {items.Count}件", _log.DEBUG);
        }

        /// <summary>
        /// 「次の表示予定画面」（_currentAjsIndex + 1）が結果画面の場合、
        /// DV_Result に対象の種目・ヒートのデータが揃っているかをチェックし
        /// TxtResultReady の表示を更新する。
        /// </summary>
        private void UpdateResultReadyLabel()
        {
            // 対象となる結果画面ID
            var resultScreenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "DSP_DUE_003", "DSP_DUE_004",
                "DSP_GRP_003", "DSP_GRP_004",
                "DSP_SOL_003", "DSP_SOL_004", "DSP_SOL_005", "DSP_SOL_006",
            };

            // 次の画面アイテムを取得
            var items = _currentAjsProgressItems;
            int nextIndex = _currentAjsIndex + 1;
            if (items == null || nextIndex < 0 || nextIndex >= items.Count)
            {
                TxtResultReady.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            var nextItem = items[nextIndex];
            if (!resultScreenIds.Contains(nextItem.ScreenId))
            {
                TxtResultReady.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            // DV_Result を取得
            var dvResult = _client?.DataManager.DV_Result
                        ?? _testDataManager?.DV_Result;
            if (dvResult == null)
            {
                ShowResultLabel("結果未受領", "#FFCC00", "#000000");
                return;
            }

            bool ok = CheckDvResultReady(dvResult, nextItem.DanceCd, nextItem.HeatNo);
            if (ok)
                ShowResultLabel("結果OK", "#00BCD4", "#FFFFFF");
            else
                ShowResultLabel("結果未受領", "#FFCC00", "#000000");
        }

        /// <summary>
        /// TxtResultReady の文字列・背景色・文字色を設定して表示する。
        /// </summary>
        private void ShowResultLabel(string text, string bgHex, string fgHex)
        {
            TxtResultReady.Text = text;
            TxtResultReady.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bgHex));
            TxtResultReady.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fgHex));
            TxtResultReady.Visibility = System.Windows.Visibility.Visible;
        }

        /// <summary>
        /// DV_Result に対象種目・ヒートの結果データが揃っているか確認する。
        /// 揃っている条件: 該当ヒートの全選手の PCS 配列のうちいずれかの「PCS得点」が 0 以外。
        /// </summary>
        /// <param name="dvResult">DV_Result の JsonNode</param>
        /// <param name="danceCd">種目記号（例: "WL"）</param>
        /// <param name="heatNo">ヒート番号（0 の場合は全選手対象）</param>
        private static bool CheckDvResultReady(
            System.Text.Json.Nodes.JsonNode dvResult,
            string danceCd,
            int heatNo)
        {
            if (string.IsNullOrEmpty(danceCd)) return false;

            var 種目結果List = dvResult["種目結果"]?.AsArray();
            if (種目結果List == null) return false;

            // 種目記号が一致する種目を探す
            System.Text.Json.Nodes.JsonNode? target = null;
            foreach (var 種目 in 種目結果List)
            {
                if (string.Equals(種目?["種目記号"]?.GetValue<string>(), danceCd, StringComparison.OrdinalIgnoreCase))
                {
                    target = 種目;
                    break;
                }
            }
            if (target == null) return false;

            var 選手結果List = target["選手結果"]?.AsArray();
            if (選手結果List == null || 選手結果List.Count == 0) return false;

            // ヒート番号で絞り込み（heatNo == 0 の場合は全選手対象）
            var 対象選手 = heatNo == 0
                ? 選手結果List.Where(p => p != null).ToList()
                : 選手結果List.Where(p => p?["ヒート番号"]?.GetValue<int>() == heatNo).ToList();

            if (対象選手.Count == 0) return false;

            // 全選手の PCS 配列にいずれかの PCS得点が 0 以外であれば OK
            return 対象選手.All(p =>
            {
                var pcsArray = p?["PCS"]?.AsArray();
                if (pcsArray == null || pcsArray.Count == 0) return false;
                return pcsArray.Any(item => (item?["PCS得点"]?.GetValue<double>() ?? 0.0) != 0.0);
            });
        }

        private void OnErrorReceived(object? sender, Handlers.ErrorReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"サーバーエラー: {e.ErrorMessage}", "エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        #endregion

        #region テストデータ読み込み

        /// <summary>
        /// テストデータ読み込みボタン：DV_Result / DA_Master / DS_Status の JSON ファイルを
        /// ファイルダイアログで選択し、テスト用DataManagerに投入する。
        /// </summary>
        private void BtnLoadTestData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "テストデータ JSON を選択（DV_Result / DA_Master / DS_Status）",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Multiselect = true,
                InitialDirectory = System.IO.Path.GetFullPath("./Scenarios/TestData")
            };

            if (dialog.ShowDialog() != true) return;

            // 初回はDataManagerを生成
            if (_testDataManager == null)
                _testDataManager = new DataManager(_log ?? new LOG_C());

            int loaded = 0;
            foreach (var path in dialog.FileNames)
            {
                try
                {
                    var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                    if (fileName.StartsWith("dv_result") || fileName.Contains("dv_result"))
                    {
                        _testDataManager.SetDV_Result(json);
                        _log?.LogAdd($"テストデータ DV_Result 読み込み完了: {path}", _log.INFO);
                        loaded++;
                    }
                    else if (fileName.StartsWith("da_master") || fileName.Contains("da_master"))
                    {
                        _testDataManager.SetDA_Master(json);
                        _log?.LogAdd($"テストデータ DA_Master 読み込み完了: {path}", _log.INFO);
                        loaded++;
                    }
                    else if (fileName.StartsWith("ds_status") || fileName.Contains("ds_status"))
                    {
                        _testDataManager.SetDS_Status(json);
                        _log?.LogAdd($"テストデータ DS_Status 読み込み完了: {path}", _log.INFO);
                        loaded++;
                    }
                    else
                    {
                        // ファイル名で判別できない場合はユーザーに選ばせる
                        var result = MessageBox.Show(
                            $"「{System.IO.Path.GetFileName(path)}」の種別を選択してください。\n\n[はい]=DV_Result　[いいえ]=DA_Master　[キャンセル]=DS_Status",
                            "データ種別選択",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            _testDataManager.SetDV_Result(json);
                            _log?.LogAdd($"テストデータ DV_Result 読み込み完了: {path}", _log.INFO);
                        }
                        else if (result == MessageBoxResult.No)
                        {
                            _testDataManager.SetDA_Master(json);
                            _log?.LogAdd($"テストデータ DA_Master 読み込み完了: {path}", _log.INFO);
                        }
                        else
                        {
                            _testDataManager.SetDS_Status(json);
                            _log?.LogAdd($"テストデータ DS_Status 読み込み完了: {path}", _log.INFO);
                        }
                        loaded++;
                    }
                }
                catch (Exception ex)
                {
                    _log?.LogAdd($"テストデータ読み込みエラー: {path}: {ex.Message}", _log.ERR);
                    MessageBox.Show($"読み込みエラー:\n{path}\n\n{ex.Message}", "エラー",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (loaded > 0)
            {
                // テストデータ読み込み後、AJS実行に必要な状態を自動セットアップ
                SetupAjsForTest();

                MessageBox.Show($"{loaded} 件のテストデータを読み込みました。\n\nAJSタブ→DSP_SOL_007 を選択して「▶ 再生」してください。",
                    "テストデータ読み込み完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// テストデータ読み込み後に AJS 実行に必要な状態を自動セットアップする。
        /// ・AJS_サンプル.json をシナリオとしてロード
        /// ・テスト用ダミー区分キー（01-010）を区分コンボに設定
        /// ・DSP_SOL_007 の行を選択状態にする
        /// ・_currentStep をリセット
        /// </summary>
        private void SetupAjsForTest()
        {
            if (_scenarioManager == null) return;

            // AJS_サンプル.json を強制ロード（新モデル）
            _currentAjsScenario = _scenarioManager.LoadAjsScenario("AJS_サンプル.json");
            if (_currentAjsScenario == null) return;

            // テスト用ダミー区分キーをセット（テストデータJSONの区分番号/ラウンド番号に合わせる）
            const string testKey  = "01-010";
            const string testText = "[テスト] 一般 決勝";
            _ajsCategoryKeys.Clear();
            _ajsCategoryKeys[testText] = testKey;

            // コンボボックスにテスト区分を表示・選択
            CmbAjsCategory.ItemsSource = new List<string> { testText };
            CmbAjsCategory.SelectedIndex = 0;

            // DS_Status / DA_Master が揃っていれば画面進行一覧を動的生成
            var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;
            _currentAjsProgressItems = null;
            LstAjsProgress.ItemsSource = null;

            if (dm?.DS_Status != null && dm?.DA_Master != null)
            {
                _currentAjsProgressItems = _scenarioManager.BuildProgressList(
                    _currentAjsScenario, dm.DS_Status, dm.DA_Master, "01", "010");

                if (_currentAjsProgressItems != null)
                {
                    LstAjsProgress.ItemsSource = _currentAjsProgressItems;

                    // DSP_SOL_007 の行を自動選択
                    int sol007Index = _currentAjsProgressItems
                        .FindIndex(i => i.ScreenId == "DSP_SOL_007");
                    _currentAjsIndex = sol007Index >= 0 ? sol007Index : 0;
                    LstAjsProgress.SelectedIndex = _currentAjsIndex;
                    UpdateResultReadyLabel();
                }
            }

            // AJSタブに切り替え
            TabControl.SelectedIndex = 1;

            _log?.LogAdd($"テスト用AJSセットアップ完了: index={_currentAjsIndex}", _log.INFO);
        }

        #endregion

        // ════════════════════════════════════════════════════════════════
        #region オナーダンスタブ
        // ════════════════════════════════════════════════════════════════

        // ---- フィールド ----

        private enum HonorDisplayMode { Full, Chroma }
        private HonorDisplayMode _honorDisplay = HonorDisplayMode.Full;

        private enum HonorAffiliationMode { Couple, Split }
        private HonorAffiliationMode _honorAffiliation = HonorAffiliationMode.Couple;

        /// <summary>現在選択中の区分エントリ</summary>
        private (string KbnNo, string RndNo, string KbnName)? _honorSelectedCategory = null;

        /// <summary>現在選択中の選手エントリ（順位番号, 背番号, 順位表示）</summary>
        private (int RankNo, string Bango, string RankStr)? _honorSelectedPlayer = null;

        /// <summary>現在選択中の区分エントリのタイトルフラグ</summary>
        private bool _honorSelectedIsTitle = false;

        /// <summary>現在選択中の区分エントリの終了フラグ</summary>
        private bool _honorSelectedIsEnd = false;

        /// <summary>現在表示中のオナーダンス画面</summary>
        private 画面.DSDspScreenBase? _currentHonorScreen = null;

        // ---- データクラス ----
        private class HonorCategoryItem
        {
            public string KbnNo   { get; set; } = string.Empty;
            public string RndNo   { get; set; } = string.Empty;
            public string KbnName { get; set; } = string.Empty;
            public string Display { get; set; } = string.Empty;
            public bool IsTitle  { get; set; } = false;
            public bool IsEnd    { get; set; } = false;
            public override string ToString() => Display;
        }

        private class HonorPlayerItem
        {
            public int    RankNo   { get; set; }
            public string 順位表示 { get; set; } = string.Empty;
            public string 背番号  { get; set; } = string.Empty;
            public string 選手名  { get; set; } = string.Empty;
            public string 所属    { get; set; } = string.Empty;
            public string RankStr { get; set; } = string.Empty;
            public override string ToString() => $"{順位表示}  {背番号}  {選手名}";
        }

        // ---- UIイベント ----

        private void RbHonorDisplay_Changed(object sender, RoutedEventArgs e)
        {
            _honorDisplay = (RbHonorChroma?.IsChecked == true)
                ? HonorDisplayMode.Chroma
                : HonorDisplayMode.Full;
            _currentHonorScreen = null;
        }

        private void RbHonorAffiliation_Changed(object sender, RoutedEventArgs e)
        {
            _honorAffiliation = (RbHonorAffSplit?.IsChecked == true)
                ? HonorAffiliationMode.Split
                : HonorAffiliationMode.Couple;
        }

        private void BtnHonorRefresh_Click(object sender, RoutedEventArgs e)
            => LoadHonorCategoryList();

        private async void LstHonorCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstHonorCategories.SelectedItem is not HonorCategoryItem item) return;

            _honorSelectedIsTitle = item.IsTitle;
            _honorSelectedIsEnd   = item.IsEnd;
            _currentHonorScreen   = null;

            if (!item.IsTitle && !item.IsEnd)
            {
                _honorSelectedCategory = (item.KbnNo, item.RndNo, item.KbnName);
                LstHonorPlayers.ItemsSource = null;
                UpdateHonorStatus($"DV_Result 要求中: {item.Display} ...");

                // サーバーに DV_Result を要求（テストデータ使用時はスキップ）
                if (_testDataManager == null && _client != null && _client.IsConnected)
                {
                    _log?.LogAdd($"オナーダンス DP_ASK_DV_RESULT送信: 区分={item.KbnNo}, ラウンド={item.RndNo}", _log.INFO);
                    bool ok = await _client.RequestDV_ResultAsync(item.KbnNo, item.RndNo);
                    if (!ok)
                        _log?.LogAdd("オナーダンス DP_ASK_DV_RESULT送信失敗", _log.WARNING);
                }
                else
                {
                    // テストデータ使用時はキャッシュを即時参照
                    UpdateHonorStatus("区分選択済み — 入賞者を選択してください");
                    LoadHonorPlayerList(item.KbnNo, item.RndNo);
                }
            }
            else if (item.IsTitle)
            {
                _honorSelectedCategory = null;
                LstHonorPlayers.ItemsSource = null;
                UpdateHonorStatus("オナーダンスタイトル選択済み — 再生ボタンでタイトル表示");
            }
            else // IsEnd
            {
                _honorSelectedCategory = null;
                LstHonorPlayers.ItemsSource = null;
                UpdateHonorStatus("終了 — 再生ボタンで切り替え表示");
            }
        }

        private void LstHonorPlayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstHonorPlayers.SelectedItem is not HonorPlayerItem item) return;
            _honorSelectedPlayer = (item.RankNo, item.背番号, item.RankStr);
            _currentHonorScreen  = null;
            UpdateHonorStatus($"{item.順位表示}  {item.選手名} — 再生ボタンで開始");
        }

        // ---- リスト読み込み ----

        /// <summary>DV_Result から決勝結果のある区分を LstHonorCategories に一覧表示する。</summary>
        private void LoadHonorCategoryList()
        {
            var daMaster = _testDataManager?.DA_Master ?? _client?.DataManager.DA_Master;

            var items = new List<HonorCategoryItem>();

            // 先頭: オナーダンスタイトル
            items.Add(new HonorCategoryItem { IsTitle = true, Display = "（オナーダンスタイトル）" });

            if (daMaster != null)
            {
                var kubuns = daMaster["DB_KUBUNs"]?.AsArray();
                if (kubuns != null)
                {
                    foreach (var kubun in kubuns)
                    {
                        if (kubun == null) continue;
                        var kbnNo   = kubun["DB_KbnNo"]?.ToString()   ?? string.Empty;
                        var kbnName = kubun["DB_KbnName"]?.ToString() ?? string.Empty;

                        var rounds = kubun["DC_ROUNDs"]?.AsArray();
                        if (rounds == null) continue;

                        foreach (var round in rounds)
                        {
                            if (round == null) continue;
                            var rndNo = round["DC_RndNo"]?.ToString() ?? string.Empty;
                            if (rndNo != "300" && rndNo != "400") continue;

                            if (items.Any(x => !x.IsTitle && x.KbnNo == kbnNo && x.RndNo == rndNo)) continue;

                            items.Add(new HonorCategoryItem
                            {
                                KbnNo   = kbnNo,
                                RndNo   = rndNo,
                                KbnName = kbnName,
                                Display = $"{kbnNo}  {kbnName}",
                            });
                        }
                    }
                }
            }

            // 末尾: 終了エントリ
            items.Add(new HonorCategoryItem { IsEnd = true, Display = "（終了）" });

            LstHonorCategories.ItemsSource = items;
            if (items.Count > 0) LstHonorCategories.SelectedIndex = 0;
            int count = items.Count - 2;
            UpdateHonorStatus(count > 0
                ? $"{count} 件の区分を表示"
                : (daMaster == null ? "DA_Master 未受信" : "決勝ラウンド（300/400）を持つ区分がありません"));
        }

        /// <summary>指定区分の入賞者リストを LstHonorPlayers に表示する。</summary>
        private void LoadHonorPlayerList(string kbnNo, string rndNo)
        {
            LstHonorPlayers.ItemsSource = null;

            var dvResult = GetDvResultFor(kbnNo, rndNo);
            var daMaster = _testDataManager?.DA_Master ?? _client?.DataManager.DA_Master;
            if (dvResult == null) return;

            var 結果リスト = 画面.DSDspDataHelper.Get総合結果リスト(dvResult);

            var items = new List<HonorPlayerItem>();
            foreach (var (rankNo, bango, _, rankStr) in 結果リスト)
            {
                var 選手情報 = daMaster != null
                    ? 画面.DSDspDataHelper.Get選手情報(daMaster, bango, kbnNo)
                    : null;
                string lName = 画面.DSDspDataHelper.Get選手名L(選手情報);
                string pName = 画面.DSDspDataHelper.Get選手名P(選手情報);
                string 選手名 = string.IsNullOrEmpty(pName) ? lName : $"{lName}・{pName}";
                string 所属  = GetHonorAffiliation(選手情報);

                string honorRankStr = rankNo switch
                {
                    1 => "チャンピオン",
                    2 => "準優勝",
                    _ => $"第{rankNo}位"
                };

                items.Add(new HonorPlayerItem
                {
                    RankNo  = rankNo,
                    順位表示 = honorRankStr,
                    背番号  = bango,
                    選手名  = 選手名,
                    所属    = 所属,
                    RankStr = honorRankStr,
                });
            }

            LstHonorPlayers.ItemsSource = items;
        }

        /// <summary>所属表示モードに応じた所属文字列を返す。</summary>
        private string GetHonorAffiliation(System.Text.Json.Nodes.JsonNode? 選手情報)
        {
            if (_honorAffiliation == HonorAffiliationMode.Couple)
                return 画面.DSDspDataHelper.Get所属(選手情報);

            // Split: L所属 + "/" + P所属
            string lCtry = 選手情報?["DM_Ctry"]?.ToString()  ?? string.Empty;
            string pCtry = 選手情報?["DM_PCtry"]?.ToString() ?? string.Empty;
            return string.IsNullOrEmpty(pCtry) ? lCtry : $"{lCtry}/{pCtry}";
        }

        // ---- ステップ実行 ----

        private void ExecuteHonorStep()
        {
            EnsureOffScreenWindowCreated();
            if (_offScreenWindow == null) return;

            var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;

            // --- (A) タイトル画面 ---
            if (_honorSelectedIsTitle)
            {
                const string titleId = "DSP_PRG_011";
                bool isSame = _currentHonorScreen?.ScreenId == titleId;

                if (!isSame)
                {
                    if (_currentHonorScreen != null)
                        _currentHonorScreen.ScreenCompleted -= OnHonorScreenCompleted;

                    var ts = new 画面.DSP_PRG_011_タイトル紹介();
                    ts.ScreenId       = titleId;
                    ts.DA_Master      = dm?.DA_Master;
                    ts.DS_Status      = dm?.DS_Status;
                    ts.Title1Override = "オナーダンス";
                    ts.Title2Override = string.Empty;
                    ts.ScreenCompleted += OnHonorScreenCompleted;
                    _currentHonorScreen = ts;
                    _offScreenWindow.ShowScreen(ts, titleId);
                    _log?.LogAdd($"オナーダンス タイトル表示: {titleId}", _log.INFO);
                }

                _currentHonorScreen!.Advance();
                UpdateHonorStatus($"タイトル  Step={_currentHonorScreen.CurrentStep}");
                return;
            }

            // --- (B) 終了画面 ---
            if (_honorSelectedIsEnd)
            {
                ExecuteHonorEndScreen(dm);
                return;
            }

            // --- (C) 選手表示 ---
            if (_honorSelectedPlayer == null)
            {
                MessageBox.Show("入賞者リストから選手を選択してください", "オナーダンス",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_honorSelectedCategory == null) return;

            var (kbnNo, rndNo, kbnName) = _honorSelectedCategory.Value;
            var (rankNo, bango, rankStr) = _honorSelectedPlayer.Value;
            var dvResult = GetDvResultFor(kbnNo, rndNo);

            // 演技順テキスト: 区分名 + "  " + 順位
            string rankSuffix = rankNo switch
            {
                1 => "チャンピオン",
                2 => "準優勝",
                _ => $"第{rankNo}位"
            };
            string orderText = $"{kbnName}  {rankSuffix}";

            // 選手情報取得
            var 選手情報 = dm?.DA_Master != null
                ? 画面.DSDspDataHelper.Get選手情報(dm.DA_Master, bango, kbnNo)
                : null;
            string lName = 画面.DSDspDataHelper.Get選手名L(選手情報);
            string pName = 画面.DSDspDataHelper.Get選手名P(選手情報);
            string 所属  = GetHonorAffiliation(選手情報);
            string 競技会名 = 画面.DSDspDataHelper.Get競技会名(dm?.DA_Master);

            string screenId  = _honorDisplay == HonorDisplayMode.Chroma ? "DSP_PRG_010" : "DSP_SOL_001";
            string com003Text = $"{rankSuffix}  {lName}・{pName}";

            bool isSameScreen = _currentHonorScreen?.ScreenId == screenId
                && _currentHonorScreen?.区分番号 == kbnNo
                && _currentHonorScreen?.ラウンド番号 == rndNo;

            if (!isSameScreen)
            {
                if (_currentHonorScreen != null)
                    _currentHonorScreen.ScreenCompleted -= OnHonorScreenCompleted;

                if (_honorDisplay == HonorDisplayMode.Full)
                {
                    var sol001 = new 画面.DSP_SOL_001_ソロ選手紹介_大();
                    sol001.ScreenId    = screenId;
                    sol001.DA_Master   = dm?.DA_Master;
                    sol001.DS_Status   = dm?.DS_Status;
                    sol001.DV_Result   = dvResult;
                    sol001.区分番号    = kbnNo;
                    sol001.ラウンド番号 = rndNo;
                    sol001.ScreenCompleted += OnHonorScreenCompleted;
                    _currentHonorScreen = sol001;
                    _offScreenWindow.ShowScreen(sol001, screenId);

                    // COM001/COM002/COM003 の初期設定（Advance前に確定する静的テキスト）
                    sol001.EnsurePartsInitialized();
                    sol001.PartsCOM001.IM_JDSFマーク.Source =
                        new System.Windows.Media.Imaging.BitmapImage(
                            new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
                    sol001.PartsCOM001.TB_左上1.Text          = 競技会名;
                    sol001.PartsCOM001.TB_左上2.Text          = kbnName;
                    sol001.PartsCOM003.LB_右上.Content        = string.Empty;
                    sol001.PartsCOM003.LB_右上.Visibility     = Visibility.Collapsed;

                    // Advance()（Step1+Step2）を実行してから TIT004 を上書き
                    sol001.Advance();
                    sol001.PartsCOM002.LB_右上.Content        = "オナーダンス";
                    sol001.PartsTIT004.LB_演技順.Content      = orderText;
                    sol001.PartsTIT004.LB_背番号.Visibility   = Visibility.Collapsed;
                    sol001.PartsTIT004.LB_選手名L.Content     = lName;
                    sol001.PartsTIT004.LB_選手名P.Content     = pName;
                    sol001.PartsTIT004.LB_所属.Content        = 所属;
                    ApplyHonorFontAdjustments(sol001);
                    _log?.LogAdd($"オナーダンス 全画面生成: {kbnName} #{bango} {orderText}", _log.INFO);
                }
                else // クロマキ: DSP_PRG_010
                {
                    var prg010 = new 画面.DSP_PRG_010_選手紹介_小();
                    prg010.ScreenId        = screenId;
                    prg010.DA_Master       = dm?.DA_Master;
                    prg010.DS_Status       = dm?.DS_Status;
                    prg010.DV_Result       = dvResult;
                    prg010.区分番号        = kbnNo;
                    prg010.ラウンド番号    = rndNo;
                    prg010.順位番号        = rankNo;
                    prg010.HonorMode       = true;
                    prg010.HonorBango      = bango;
                    prg010.HonorAffiliation = 所属;
                    prg010.ScreenCompleted += OnHonorScreenCompleted;
                    _currentHonorScreen = prg010;
                    _offScreenWindow.ShowScreen(prg010, screenId);

                    // STEP1: COM001/COM002 初期化 → COM002 を「オナーダンス」に上書き
                    prg010.Advance();
                    prg010.PartsCOM002.LB_右上.Content = "オナーダンス";

                    // STEP2: LB_区分名 に orderText を事前セット（Step2() 内の HonorMode 分岐で参照する）
                    prg010.PartsPRG006.LB_区分名.Content = orderText;
                    prg010.Advance();
                    _log?.LogAdd($"オナーダンス クロマキ生成: {kbnName} #{bango} {orderText}", _log.INFO);
                }
            }
            else
            {
                // 同じ画面で Step が 1 のとき（フェードアウト後）→ COM003 に表示
                if (_currentHonorScreen?.CurrentStep == 1)
                {
                    if (_honorDisplay == HonorDisplayMode.Full
                        && _currentHonorScreen is 画面.DSP_SOL_001_ソロ選手紹介_大 s001)
                    {
                        s001.PartsCOM003.LB_右上.Content    = com003Text;
                        s001.PartsCOM003.LB_右上.Visibility = Visibility.Visible;
                    }
                }
                _currentHonorScreen!.Advance();
            }

            // クロマキモードのとき DisplayWindow の背景色をクロマキ色に設定する
            if (_honorDisplay == HonorDisplayMode.Chroma)
                ApplyAwardWindowBackground(true);

            if (_currentHonorScreen != null)
                UpdateHonorStatus($"表示中  Step={_currentHonorScreen.CurrentStep}  {kbnName} #{bango}");
            _log?.LogAdd($"オナーダンス Advance: step={_currentHonorScreen?.CurrentStep}", _log.INFO);
        }

        /// <summary>オナーダンス 終了: COM001マーク+競技会名のみ残す。</summary>
        private void ExecuteHonorEndScreen(Data.DataManager? dm)
        {
            EnsureOffScreenWindowCreated();
            if (_offScreenWindow == null) return;

            string screenId = _honorDisplay == HonorDisplayMode.Chroma ? "DSP_PRG_010" : "DSP_SOL_001";
            string 競技会名 = 画面.DSDspDataHelper.Get競技会名(dm?.DA_Master);

            bool canReuse = _currentHonorScreen != null
                && (_currentHonorScreen.ScreenId == "DSP_SOL_001"
                    || _currentHonorScreen.ScreenId == "DSP_PRG_010");

            if (!canReuse)
            {
                if (_currentHonorScreen != null)
                    _currentHonorScreen.ScreenCompleted -= OnHonorScreenCompleted;

                if (_honorDisplay == HonorDisplayMode.Full)
                {
                    var s = new 画面.DSP_SOL_001_ソロ選手紹介_大();
                    s.ScreenId = screenId;
                    s.DA_Master = dm?.DA_Master;
                    s.ScreenCompleted += OnHonorScreenCompleted;
                    _currentHonorScreen = s;
                    _offScreenWindow.ShowScreen(s, screenId);
                    s.EnsurePartsInitialized();
                    s.PartsCOM001.IM_JDSFマーク.Source =
                        new System.Windows.Media.Imaging.BitmapImage(
                            new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
                    s.PartsCOM001.TB_左上1.Text = 競技会名;
                    s.PartsCOM001.TB_左上2.Text = string.Empty;
                }
                else // クロマキ: DSP_PRG_010
                {
                    var s = new 画面.DSP_PRG_010_選手紹介_小();
                    s.ScreenId = screenId;
                    s.DA_Master = dm?.DA_Master;
                    s.ScreenCompleted += OnHonorScreenCompleted;
                    _currentHonorScreen = s;
                    _offScreenWindow.ShowScreen(s, screenId);
                    s.EnsurePartsInitialized();
                    s.PartsCOM001.IM_JDSFマーク.Source =
                        new System.Windows.Media.Imaging.BitmapImage(
                            new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
                    s.PartsCOM001.TB_左上1.Text = 競技会名;
                    s.PartsCOM001.TB_左上2.Text = string.Empty;
                }
            }

            // COM001以外を非表示
            if (_currentHonorScreen is 画面.DSP_SOL_001_ソロ選手紹介_大 sol1)
            {
                sol1.PartsCOM001.TB_左上2.Text = string.Empty;
                sol1.PartsCOM002.LB_右上.Visibility  = Visibility.Collapsed;
                sol1.PartsCOM003.LB_右上.Visibility  = Visibility.Collapsed;
                sol1.PartsTIT004.LB_演技順.Visibility = Visibility.Collapsed;
                sol1.PartsTIT004.LB_背番号.Visibility  = Visibility.Collapsed;
                sol1.PartsTIT004.LB_選手名L.Visibility = Visibility.Collapsed;
                sol1.PartsTIT004.LB_選手名P.Visibility = Visibility.Collapsed;
                sol1.PartsTIT004.LB_所属.Visibility   = Visibility.Collapsed;
                sol1.PartsTIT004.IM_種目1.Visibility  = Visibility.Collapsed;
                sol1.PartsTIT004.IM_種目2.Visibility  = Visibility.Collapsed;
            }
            else if (_currentHonorScreen is 画面.DSP_PRG_010_選手紹介_小 sol010)
            {
                sol010.PartsCOM001.TB_左上2.Text        = string.Empty;
                sol010.PartsCOM002.LB_右上.Visibility   = Visibility.Collapsed;
                sol010.PartsCOM003.LB_右上.Visibility   = Visibility.Collapsed;
                sol010.PartsPRG006.LB_区分名.Visibility = Visibility.Collapsed;
                sol010.PartsPRG006.LB_順位.Visibility   = Visibility.Collapsed;
                sol010.PartsPRG006.LB_選手名.Visibility = Visibility.Collapsed;
                sol010.PartsPRG006.LB_所属.Visibility   = Visibility.Collapsed;
                sol010.PartsPRG006.LB_得点.Visibility   = Visibility.Collapsed;
                sol010.PartsPRG006.IM_種目1.Visibility  = Visibility.Collapsed;
                sol010.PartsPRG006.IM_種目2.Visibility  = Visibility.Collapsed;
            }

            UpdateHonorStatus("終了 — COM001のみ表示中");
            _log?.LogAdd("オナーダンス 終了画面", _log.INFO);
        }

        private void OnHonorScreenCompleted(object? sender, EventArgs e)
        {
            if (sender is 画面.DSDspScreenBase s)
                s.ScreenCompleted -= OnHonorScreenCompleted;
            _currentHonorScreen = null;
            Dispatcher.Invoke(() => UpdateHonorStatus("画面終了"));
            _log?.LogAdd("オナーダンス 画面完了", _log.INFO);
        }

        private void UpdateHonorStatus(string text)
        {
            if (TxtHonorStatus != null)
                TxtHonorStatus.Text = text;
        }

        /// <summary>SOL_001 パーツのフォントサイズ自動調整。</summary>
        private void ApplyHonorFontAdjustments(画面.DSP_SOL_001_ソロ選手紹介_大 s)
        {
            var pm = s.PartsMainInstance;
            if (pm == null) return;
            pm.フォントサイズ自動調整(s.PartsTIT004.LB_演技順,  s.PartsTIT004.LB_演技順.Content?.ToString()  ?? "", 400, 16, 6, "Segoe UI Semibold");
            pm.フォントサイズ自動調整(s.PartsTIT004.LB_選手名L, s.PartsTIT004.LB_選手名L.Content?.ToString() ?? "", 400, 22, 8, "Segoe UI Semibold");
            pm.フォントサイズ自動調整(s.PartsTIT004.LB_選手名P, s.PartsTIT004.LB_選手名P.Content?.ToString() ?? "", 400, 22, 8, "Segoe UI Semibold");
            pm.フォントサイズ自動調整(s.PartsTIT004.LB_所属,    s.PartsTIT004.LB_所属.Content?.ToString()    ?? "", 400, 20, 8, "Segoe UI Semibold");
        }

        #endregion
    }
}

// Made with Bob
