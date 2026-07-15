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
        private int _currentStep = 0;
        private int _currentSubStep = 0;           // SUB用ステップカウンター
        private int _selectedScreenIndex = -1;   // コンボボックスで選択されているスクリーン番号
        private int _activeScreenIndex = -1;     // 現在全画面表示中のスクリーン番号（-1=非表示）
        private bool _isTestDisplayActive = false;  // テスト表示が有効かどうか
        
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

            // 進行シナリオ
            var progressFiles = _scenarioManager.GetScenarioFiles(ScenarioType.Progress);
            CmbProgressScenario.ItemsSource = progressFiles;
            if (progressFiles.Count > 0)
                CmbProgressScenario.SelectedIndex = 0;

            // AJSシナリオ
            var ajsFiles = _scenarioManager.GetScenarioFiles(ScenarioType.AJS);
            CmbAjsScenario.ItemsSource = ajsFiles;
            if (ajsFiles.Count > 0)
                CmbAjsScenario.SelectedIndex = 0;

            // 表彰式シナリオ
            var awardFiles = _scenarioManager.GetScenarioFiles(ScenarioType.Award);
            CmbAwardScenario.ItemsSource = awardFiles;
            if (awardFiles.Count > 0)
                CmbAwardScenario.SelectedIndex = 0;
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
                UpdateConnectionStatus("接続中...", Brushes.Orange);
                BtnConnect.IsEnabled = false;

                _client = new DSDspClient();
                _client.ConnectionStateChanged += OnConnectionStateChanged;
                _client.DA_MasterReceived += OnDA_MasterReceived;
                _client.DS_StatusReceived += OnDS_StatusReceived;
                _client.ErrorReceived += OnErrorReceived;

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

            // ExecuteCurrentStep 内でステップがリセットされた場合は++ しない
            bool stepped = ExecuteCurrentStep();
            if (stepped)
                _currentStep++;
        }

        /// <summary>
        /// クリアボタンクリック（全画面共通）
        /// </summary>
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _currentStep = 0;
            // オフスクリーン側をクリアすればモニター・全画面ミラーも自動反映
            _offScreenWindow?.ClearScreen();
            _offScreenWindow?.ClearSubScreen();
            _currentSubStep = 0;
            _log?.LogAdd("画面クリア（メイン＋SUB）", _log.INFO);
        }

        /// <summary>
        /// メインクリアボタンクリック（AJSタブのメイン画面進行のみクリア）
        /// </summary>
        private void BtnMainClear_Click(object sender, RoutedEventArgs e)
        {
            _currentStep = 0;
            _offScreenWindow?.ClearScreen();
            _log?.LogAdd("メイン画面クリア", _log.INFO);
        }

        /// <summary>
        /// SUB再生ボタンクリック
        /// </summary>
        private void BtnSubPlay_Click(object sender, RoutedEventArgs e)
        {
            EnsureOffScreenWindowCreated();

            bool stepped = ExecuteAjsSubStep();
            if (stepped)
                _currentSubStep++;
        }

        /// <summary>
        /// SUBクリアボタンクリック
        /// </summary>
        private void BtnSubClear_Click(object sender, RoutedEventArgs e)
        {
            _currentSubStep = 0;
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

                    default: // None
                        window.ClearBackground();
                        break;
                }
            }

            Apply(_offScreenWindow);
            Apply(_displayWindow);
            Apply(_fullScreenWindow);
        }

        #endregion

        #region 進行タブ

        private void CmbProgressScenario_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProgressScenario.SelectedItem == null || _scenarioManager == null) return;

            var fileName = CmbProgressScenario.SelectedItem.ToString();
            if (string.IsNullOrEmpty(fileName)) return;

            _currentProgressScenario = _scenarioManager.LoadProgressScenario(fileName);
            
            if (_currentProgressScenario != null)
            {
                LstProgressItems.ItemsSource = _currentProgressScenario.Items;
                _currentProgressIndex = -1;
                _currentStep = 0;
            }
        }

        private void LstProgressItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentProgressIndex = LstProgressItems.SelectedIndex;
            _currentStep = 0;
            _log?.LogAdd($"進行項目選択: {_currentProgressIndex}", _log.DEBUG);
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
            _currentStep = 0;

            // SUB画面進行もリセット
            _currentAjsSubProgressItems = null;
            LstAjsSubProgress.ItemsSource = null;
            _currentAjsSubIndex = -1;
            _currentSubStep = 0;

            if (dm?.DS_Status != null && dm?.DA_Master != null)
            {
                // メイン画面進行一覧を生成
                _currentAjsProgressItems = _scenarioManager.BuildProgressList(
                    _currentAjsScenario, dm.DS_Status, dm.DA_Master, kbnNo, roundNo);

                if (_currentAjsProgressItems != null)
                {
                    LstAjsProgress.ItemsSource = _currentAjsProgressItems;
                    _log?.LogAdd($"AJS画面進行一覧生成: {_currentAjsProgressItems.Count}件 (区分={kbnNo}, ラウンド={roundNo})", _log.INFO);
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
            // プログラムによる変更（ステップ進行中の自動移動）のときは _currentStep をリセットしない
            if (_suppressAjsSelectionChanged) return;

            _currentAjsIndex = LstAjsProgress.SelectedIndex;
            _currentStep = 0;
            _log?.LogAdd($"AJS項目選択: {_currentAjsIndex}", _log.DEBUG);
        }

        private void LstAjsSubProgress_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // プログラムによる変更（ステップ進行中の自動移動）のときは _currentSubStep をリセットしない
            if (_suppressAjsSubSelectionChanged) return;

            _currentAjsSubIndex = LstAjsSubProgress.SelectedIndex;
            _currentSubStep = 0;
            _log?.LogAdd($"AJS SUB項目選択: {_currentAjsSubIndex}", _log.DEBUG);
        }

        #endregion

        #region 表彰式タブ

        private void CmbAwardScenario_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbAwardScenario.SelectedItem == null || _scenarioManager == null) return;

            var fileName = CmbAwardScenario.SelectedItem.ToString();
            if (string.IsNullOrEmpty(fileName)) return;

            _currentAwardScenario = _scenarioManager.LoadScreenScenario(fileName);
            
            if (_currentAwardScenario != null)
            {
                var categories = _scenarioManager.GetCategories(_currentAwardScenario);
                CmbAwardCategory.ItemsSource = categories;
                if (categories.Count > 0)
                    CmbAwardCategory.SelectedIndex = 0;
            }
        }

        private void CmbAwardCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbAwardCategory.SelectedItem == null || _currentAwardScenario == null || _scenarioManager == null) 
                return;

            var category = CmbAwardCategory.SelectedItem.ToString();
            if (string.IsNullOrEmpty(category)) return;

            var parts = category.Split('-');
            if (parts.Length == 2)
            {
                var items = _scenarioManager.GetScreenItems(_currentAwardScenario, parts[0], parts[1]);
                LstAwardProgress.ItemsSource = items;
                _currentAwardIndex = -1;
                _currentStep = 0;
            }
        }

        private void LstAwardProgress_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentAwardIndex = LstAwardProgress.SelectedIndex;
            _currentStep = 0;
            _log?.LogAdd($"表彰式項目選択: {_currentAwardIndex}", _log.DEBUG);
        }

        #endregion

        #region ステップ実行

        /// <summary>
        /// 現在のステップを実行する。
        /// 戻り値: true=呼び出し元で _currentStep++ すべき / false=内部でリセット済みなので不要
        /// </summary>
        private bool ExecuteCurrentStep()
        {
            _log?.LogAdd($"ステップ実行: Step{_currentStep}", _log.INFO);
            
            // 現在選択されているタブを判定
            if (TabControl.SelectedIndex == 1) // AJSタブ
            {
                return ExecuteAjsStep();
            }
            else if (TabControl.SelectedIndex == 2) // 表彰式タブ
            {
                ExecuteAwardStep();
                return true;
            }
            else // 進行タブ
            {
                ExecuteProgressStep();
                return true;
            }
        }

        /// <summary>
        /// 進行タブのステップを実行
        /// </summary>
        private void ExecuteProgressStep()
        {
            if (_currentProgressScenario == null || _currentProgressIndex < 0 ||
                _currentProgressIndex >= _currentProgressScenario.Items.Count)
            {
                _log?.LogAdd("進行項目が選択されていません", _log.WARNING);
                return;
            }

            var item = _currentProgressScenario.Items[_currentProgressIndex];
            _log?.LogAdd($"進行ステップ実行: {item}", _log.INFO);
            
            // TODO: 進行タブの画面表示処理を実装
            MessageBox.Show($"進行Step{_currentStep}: {item}", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// AJSタブのステップを実行する。
        /// 戻り値: true=通常実行（呼び出し元で _currentStep++ すべき）
        ///         false=最終ステップ完了によりリセット済み（++ 不要）
        /// </summary>
        private bool ExecuteAjsStep()
        {
            EnsureOffScreenWindowCreated();

            if (_currentAjsProgressItems == null)
            {
                _log?.LogAdd("AJS画面進行一覧が生成されていません", _log.WARNING);
                return false;
            }

            if (_currentAjsIndex < 0 || _currentAjsIndex >= _currentAjsProgressItems.Count)
            {
                _log?.LogAdd("AJS項目が選択されていません", _log.WARNING);
                return false;
            }

            // 選択された区分情報を取得
            if (CmbAjsCategory.SelectedItem == null) return false;
            var displayText = CmbAjsCategory.SelectedItem.ToString();
            if (string.IsNullOrEmpty(displayText)) return false;

            if (!_ajsCategoryKeys.TryGetValue(displayText, out var key)) return false;
            var keyParts = key.Split('-');
            if (keyParts.Length != 2) return false;

            var kbnNo   = keyParts[0];
            var roundNo = keyParts[1];

            var item = _currentAjsProgressItems[_currentAjsIndex];
            _log?.LogAdd($"AJSステップ実行: {item.ScreenId} Step{_currentStep} 種目{item.DanceNo} ヒート{item.HeatNo}", _log.INFO);

            // 画面IDに基づいて画面インスタンスを生成
            DSDspScreenBase? screen = item.ScreenId switch
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

                _ => null
            };

            if (screen == null)
            {
                _log?.LogAdd($"未対応の画面ID: {item.ScreenId}", _log.WARNING);
                MessageBox.Show($"未対応の画面ID: {item.ScreenId}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // 初回表示時（Step0）は画面を表示してデータを設定
            if (_currentStep == 0)
            {
                var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;
                if (dm?.DA_Master != null) screen.DA_Master = dm.DA_Master;
                if (dm?.DS_Status != null) screen.DS_Status = dm.DS_Status;
                if (dm?.DV_Result != null) screen.DV_Result = dm.DV_Result;

                screen.区分番号    = kbnNo;
                screen.ラウンド番号 = roundNo;
                screen.種目番号    = item.DanceNo;
                screen.ヒート番号  = item.HeatNo;
                screen.IsOverviewMode = item.IsOverviewMode;

                // オフスクリーンに画面を表示（モニター・全画面ミラーに自動反映）
                _offScreenWindow?.ShowScreen(screen);
                _log?.LogAdd($"画面表示: {item.ScreenId}", _log.INFO);
            }

            var currentScreen = _offScreenWindow?.CurrentScreen as DSDspScreenBase;
            if (currentScreen == null)
            {
                _log?.LogAdd("表示中の画面がありません", _log.WARNING);
                return false;
            }

            currentScreen.ExecuteStep(_currentStep);
            _log?.LogAdd($"{item.ScreenId} Step{_currentStep}実行完了", _log.INFO);

            // 最終ステップに到達したら次の画面へ移動
            if (_currentStep >= currentScreen.GetTotalSteps() - 1)
            {
                _currentAjsIndex++;
                _currentStep = 0;

                if (_currentAjsIndex < _currentAjsProgressItems.Count)
                {
                    // HoldsAfterFadeOut=true の場合はフェードアウト完了後も自動遷移せず一旦停止する
                    if (currentScreen.HoldsAfterFadeOut)
                    {
                        // フェードアウトは既に実行中。インデックスだけ進めて停止。
                        // リストの選択を次の項目に移す
                        _suppressAjsSelectionChanged = true;
                        LstAjsProgress.SelectedIndex = _currentAjsIndex;
                        _suppressAjsSelectionChanged = false;
                        _log?.LogAdd($"HoldsAfterFadeOut: 次の画面で停止 Index={_currentAjsIndex}", _log.INFO);
                    }
                    // 最終StepにフェードアウトWait設定がある場合は完了イベントを待つ
                    else if (currentScreen.WaitsForLastStepFadeOut)
                    {
                        // イベントが発火したときに次の画面遷移を実行
                        void OnFadeOutCompleted(object? s, EventArgs e)
                        {
                            currentScreen.LastStepFadeOutCompleted -= OnFadeOutCompleted;
                            // UIスレッドで実行（Storyboard.Completed はUIスレッドで呼ばれるが念のため）
                            Dispatcher.Invoke(() => MoveToNextAjsScreen());
                        }
                        currentScreen.LastStepFadeOutCompleted += OnFadeOutCompleted;
                    }
                    else
                    {
                        // フェードアウト完了イベントなし → 即時遷移（従来動作）
                        MoveToNextAjsScreen();
                    }
                }
                else
                {
                    _log?.LogAdd("すべての画面が完了しました", _log.INFO);
                    MessageBox.Show("すべての画面が完了しました", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// AJS次の画面へ遷移し、Step0を実行する。
        /// フェードアウト完了イベントのコールバックまたは即時遷移の両方から呼ばれる。
        /// </summary>
        private void MoveToNextAjsScreen()
        {
            _suppressAjsSelectionChanged = true;
            LstAjsProgress.SelectedIndex = _currentAjsIndex;
            _suppressAjsSelectionChanged = false;
            _log?.LogAdd($"次の画面へ移動: Index={_currentAjsIndex}", _log.INFO);
            bool nextStepDone = ExecuteAjsStep();
            // ExecuteAjsStep() で次の画面の Step0（Step1+Step2自動実行）が正常完了した場合、
            // _currentStep を 1 に進めておく（次の再生ボタンで Step1 から続きを実行するため）
            if (nextStepDone)
                _currentStep = 1;
        }

        /// <summary>
        /// AJS SUBタブのステップを実行する。
        /// メインと独立してオフスクリーンの SubContentGrid に表示する。
        /// 戻り値: true=通常実行（呼び出し元で _currentSubStep++ すべき）
        ///         false=最終ステップ完了によりリセット済み（++ 不要）
        /// </summary>
        private bool ExecuteAjsSubStep()
        {
            EnsureOffScreenWindowCreated();

            if (_currentAjsSubProgressItems == null)
            {
                _log?.LogAdd("AJS SUB画面進行一覧が生成されていません", _log.WARNING);
                return false;
            }

            if (_currentAjsSubIndex < 0 || _currentAjsSubIndex >= _currentAjsSubProgressItems.Count)
            {
                _log?.LogAdd("AJS SUB項目が選択されていません", _log.WARNING);
                return false;
            }

            // 選択された区分情報を取得
            if (CmbAjsCategory.SelectedItem == null) return false;
            var displayText = CmbAjsCategory.SelectedItem.ToString();
            if (string.IsNullOrEmpty(displayText)) return false;

            if (!_ajsCategoryKeys.TryGetValue(displayText, out var key)) return false;
            var keyParts = key.Split('-');
            if (keyParts.Length != 2) return false;

            var kbnNo   = keyParts[0];
            var roundNo = keyParts[1];

            var item = _currentAjsSubProgressItems[_currentAjsSubIndex];
            _log?.LogAdd($"AJS SUBステップ実行: {item.ScreenId} Step{_currentSubStep} 種目{item.DanceNo} ヒート{item.HeatNo}", _log.INFO);

            // 画面IDに基づいて画面インスタンスを生成
            DSDspScreenBase? screen = item.ScreenId switch
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

                _ => null
            };

            if (screen == null)
            {
                _log?.LogAdd($"SUB: 未対応の画面ID: {item.ScreenId}", _log.WARNING);
                MessageBox.Show($"SUB: 未対応の画面ID: {item.ScreenId}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // 初回表示時（SubStep0）は画面を表示してデータを設定
            if (_currentSubStep == 0)
            {
                var dm = (_testDataManager != null) ? _testDataManager : _client?.DataManager;
                if (dm?.DA_Master != null) screen.DA_Master = dm.DA_Master;
                if (dm?.DS_Status != null) screen.DS_Status = dm.DS_Status;
                if (dm?.DV_Result != null) screen.DV_Result = dm.DV_Result;

                screen.区分番号    = kbnNo;
                screen.ラウンド番号 = roundNo;
                screen.種目番号    = item.DanceNo;
                screen.ヒート番号  = item.HeatNo;
                screen.IsOverviewMode = item.IsOverviewMode;

                // オフスクリーンの SubContentGrid に表示（透明背景でメインの上に重なる）
                _offScreenWindow?.ShowSubScreen(screen);
                _log?.LogAdd($"SUB画面表示: {item.ScreenId}", _log.INFO);
            }

            var currentSubScreen = _offScreenWindow?.CurrentSubScreen as DSDspScreenBase;
            if (currentSubScreen == null)
            {
                _log?.LogAdd("表示中のSUB画面がありません", _log.WARNING);
                return false;
            }

            currentSubScreen.ExecuteStep(_currentSubStep);
            _log?.LogAdd($"SUB {item.ScreenId} Step{_currentSubStep}実行完了", _log.INFO);

            // 最終ステップに到達したら次の画面へ移動
            if (_currentSubStep >= currentSubScreen.GetTotalSteps() - 1)
            {
                _currentAjsSubIndex++;
                _currentSubStep = 0;

                if (_currentAjsSubIndex < _currentAjsSubProgressItems.Count)
                {
                    // HoldsAfterFadeOut=true の場合はフェードアウト完了後も自動遷移せず一旦停止する
                    if (currentSubScreen.HoldsAfterFadeOut)
                    {
                        _suppressAjsSubSelectionChanged = true;
                        LstAjsSubProgress.SelectedIndex = _currentAjsSubIndex;
                        _suppressAjsSubSelectionChanged = false;
                        _log?.LogAdd($"SUB HoldsAfterFadeOut: 次の画面で停止 Index={_currentAjsSubIndex}", _log.INFO);
                    }
                    else if (currentSubScreen.WaitsForLastStepFadeOut)
                    {
                        void OnFadeOutCompleted(object? s, EventArgs e)
                        {
                            currentSubScreen.LastStepFadeOutCompleted -= OnFadeOutCompleted;
                            Dispatcher.Invoke(() => MoveToNextAjsSubScreen());
                        }
                        currentSubScreen.LastStepFadeOutCompleted += OnFadeOutCompleted;
                    }
                    else
                    {
                        MoveToNextAjsSubScreen();
                    }
                }
                else
                {
                    _log?.LogAdd("SUB: すべての画面が完了しました", _log.INFO);
                    MessageBox.Show("SUB: すべての画面が完了しました", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// AJS SUB 次の画面へ遷移し、SubStep0 を実行する。
        /// </summary>
        private void MoveToNextAjsSubScreen()
        {
            _suppressAjsSubSelectionChanged = true;
            LstAjsSubProgress.SelectedIndex = _currentAjsSubIndex;
            _suppressAjsSubSelectionChanged = false;
            _log?.LogAdd($"SUB: 次の画面へ移動: Index={_currentAjsSubIndex}", _log.INFO);
            bool nextStepDone = ExecuteAjsSubStep();
            if (nextStepDone)
                _currentSubStep = 1;
        }

        /// <summary>
        /// 表彰式タブのステップを実行
        /// </summary>
        private void ExecuteAwardStep()
        {
            if (_currentAwardScenario == null || LstAwardProgress.ItemsSource == null)
            {
                _log?.LogAdd("表彰式シナリオが選択されていません", _log.WARNING);
                return;
            }

            var items = LstAwardProgress.ItemsSource as List<ScreenScenarioItem>;
            if (items == null || _currentAwardIndex < 0 || _currentAwardIndex >= items.Count)
            {
                _log?.LogAdd("表彰式項目が選択されていません", _log.WARNING);
                return;
            }

            var item = items[_currentAwardIndex];
            _log?.LogAdd($"表彰式ステップ実行: {item}", _log.INFO);
            
            // TODO: 表彰式タブの画面表示処理を実装
            MessageBox.Show($"表彰式Step{_currentStep}: {item}", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
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
                }
            });
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
                }
            }

            _currentStep = 0;

            // AJSタブに切り替え
            TabControl.SelectedIndex = 1;

            _log?.LogAdd($"テスト用AJSセットアップ完了: index={_currentAjsIndex}", _log.INFO);
        }

        #endregion
    }
}

// Made with Bob
