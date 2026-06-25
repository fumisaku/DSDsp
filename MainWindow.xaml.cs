using DSDsp.Scenario;
using DSDsp.画面;

namespace DSDsp
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private DisplayWindow? _displayWindow;  // モニター用表示ウィンドウ
        private DisplayWindow? _fullScreenWindow;  // 全画面表示用ウィンドウ
        private DSDspClient? _client;
        private ScenarioManager? _scenarioManager;
        private LOG_C? _log;

        // 現在のシナリオ
        private ProgressScenario? _currentProgressScenario;
        private ScreenScenario? _currentAjsScenario;
        private ScreenScenario? _currentAwardScenario;

        // 現在の選択
        private int _currentProgressIndex = -1;
        private int _currentAjsIndex = -1;
        private int _currentAwardIndex = -1;
        private int _currentStep = 0;
        private int _selectedScreenIndex = -1;  // 選択されているスクリーン番号
        private bool _isTestDisplayActive = false;  // テスト表示が有効かどうか
        
        // AJS区分情報（キー: 表示テキスト, 値: "区分No-ラウンドNo"）
        private Dictionary<string, string> _ajsCategoryKeys = new Dictionary<string, string>();

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
            
            // クライアントを破棄
            _client?.Dispose();
            
            // 全画面ウィンドウを閉じる
            _fullScreenWindow?.Close();
            
            // 表示用ウィンドウを閉じる
            _displayWindow?.Close();
        }

        /// <summary>
        /// モニター用表示ウィンドウを作成
        /// </summary>
        private void CreateDisplayWindow()
        {
            if (_displayWindow != null)
            {
                // 既存のウィンドウがある場合は表示状態にする
                if (!_displayWindow.IsVisible)
                {
                    _displayWindow.Show();
                }
                return;
            }

            _displayWindow = new DisplayWindow();
            
            // 表示ウィンドウをコントロール画面の右側に配置（重ならないように）
            _displayWindow.Left = this.Left + this.Width + 10;
            _displayWindow.Top = this.Top;
            
            // ウィンドウが閉じられた時のイベントハンドラを設定
            _displayWindow.Closed += DisplayWindow_Closed;
            
            _displayWindow.Show();
            _log?.LogAdd("モニター用表示ウィンドウを作成", _log.INFO);
        }

        /// <summary>
        /// モニター用表示ウィンドウが閉じられた時の処理
        /// </summary>
        private void DisplayWindow_Closed(object? sender, EventArgs e)
        {
            if (_displayWindow != null)
            {
                _displayWindow.Closed -= DisplayWindow_Closed;
                _displayWindow = null;
                _log?.LogAdd("モニター用表示ウィンドウが閉じられました", _log.INFO);
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
        /// 表示ウィンドウを指定されたスクリーンに配置
        /// </summary>
        private void PositionDisplayWindow(int screenIndex)
        {
            if (_displayWindow == null) return;

            var screens = WinForms.Screen.AllScreens;
            _log?.LogAdd($"利用可能なスクリーン数: {screens.Length}", _log.INFO);
            
            if (screenIndex < screens.Length)
            {
                var screen = screens[screenIndex];
                
                // スクリーン情報をログ出力（物理ピクセル）
                _log?.LogAdd($"スクリーン{screenIndex + 1}（物理ピクセル）: Left={screen.Bounds.Left}, Top={screen.Bounds.Top}, Width={screen.Bounds.Width}, Height={screen.Bounds.Height}", _log.INFO);
                
                // 既存の全画面ウィンドウがあれば閉じる
                if (_fullScreenWindow != null)
                {
                    _fullScreenWindow.Close();
                    _fullScreenWindow = null;
                }
                
                // 新しい全画面表示用ウィンドウを作成
                _fullScreenWindow = new DisplayWindow();
                _fullScreenWindow.Title = $"表示ウィンドウ（スクリーン{screenIndex + 1}）";
                
                // 全画面設定（先に設定）
                _fullScreenWindow.WindowStyle = WindowStyle.None;
                _fullScreenWindow.ResizeMode = ResizeMode.NoResize;
                _fullScreenWindow.WindowState = WindowState.Normal;
                
                // DPIスケールを取得
                var source = PresentationSource.FromVisual(this);
                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;
                
                if (source != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }
                
                _log?.LogAdd($"DPIスケール: X={dpiScaleX}, Y={dpiScaleY}", _log.INFO);
                
                // 物理ピクセルからWPFの論理ピクセルに変換
                double left = screen.Bounds.Left / dpiScaleX;
                double top = screen.Bounds.Top / dpiScaleY;
                double width = screen.Bounds.Width / dpiScaleX;
                double height = screen.Bounds.Height / dpiScaleY;
                
                _log?.LogAdd($"スクリーン{screenIndex + 1}（論理ピクセル）: Left={left}, Top={top}, Width={width}, Height={height}", _log.INFO);
                
                // スクリーンの境界に正確に配置
                _fullScreenWindow.Left = left;
                _fullScreenWindow.Top = top;
                _fullScreenWindow.Width = width;
                _fullScreenWindow.Height = height;
                
                // モニター用と同じ画面を表示中の場合は、全画面ウィンドウにも同じ内容を表示
                var monitorScreen = _displayWindow?.CurrentScreen as 画面.DSDspScreenBase;
                if (monitorScreen != null)
                {
                    var mirrorScreen = CreateScreenInstance(monitorScreen);
                    if (mirrorScreen != null)
                        _fullScreenWindow.ShowScreen(mirrorScreen);
                }

                // 最前面に表示
                _fullScreenWindow.Topmost = true;
                _fullScreenWindow.Show();
                
                _log?.LogAdd($"スクリーン{screenIndex + 1}に全画面表示完了: Left={_fullScreenWindow.Left}, Top={_fullScreenWindow.Top}, Width={_fullScreenWindow.Width}, Height={_fullScreenWindow.Height}", _log.INFO);
            }
        }

        /// <summary>
        /// 表示/非表示切り替え（全画面ウィンドウのみ）
        /// </summary>
        private void BtnToggleDisplay_Click(object sender, RoutedEventArgs e)
        {
            // 全画面ウィンドウがある場合は、それを非表示/表示
            if (_fullScreenWindow != null)
            {
                if (_fullScreenWindow.Visibility == Visibility.Visible)
                {
                    // 表示中の場合は非表示にする
                    _fullScreenWindow.Visibility = Visibility.Hidden;
                    _log?.LogAdd($"全画面表示を非表示", _log.INFO);
                }
                else
                {
                    // 非表示の場合は表示する
                    _fullScreenWindow.Visibility = Visibility.Visible;
                    _log?.LogAdd($"全画面表示を表示", _log.INFO);
                }
            }
            else
            {
                // 全画面ウィンドウがない場合は、選択されたスクリーンに新規作成
                if (_selectedScreenIndex >= 0)
                {
                    PositionDisplayWindow(_selectedScreenIndex);
                }
                else
                {
                    _log?.LogAdd("スクリーンが選択されていません", _log.WARNING);
                    MessageBox.Show("スクリーンを選択してください", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// テスト表示ボタンクリック（トグル式）
        /// </summary>
        private void BtnTestDisplay_Click(object sender, RoutedEventArgs e)
        {
            // ウィンドウが閉じられているか非表示の場合は再表示
            if (_displayWindow == null || !_displayWindow.IsVisible)
            {
                CreateDisplayWindow();
            }

            if (_displayWindow != null)
            {
                if (_isTestDisplayActive)
                {
                    // テスト表示中の場合は、画面をクリア
                    _displayWindow.ClearScreen();
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
                    _displayWindow.ShowScreen(testScreen);
                    _isTestDisplayActive = true;
                    BtnTestDisplay.Content = "✕ テスト終了";
                    BtnTestDisplay.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F44336"));
                    _log?.LogAdd("テスト表示画面を表示", _log.INFO);
                }
            }
        }

        /// <summary>
        /// 再生ボタンクリック
        /// </summary>
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            // ウィンドウが閉じられているか非表示の場合は再表示
            if (_displayWindow == null || !_displayWindow.IsVisible)
            {
                CreateDisplayWindow();
            }

            ExecuteCurrentStep();
            _currentStep++;
        }

        /// <summary>
        /// クリアボタンクリック
        /// </summary>
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            // ウィンドウが閉じられているか非表示の場合は再表示
            if (_displayWindow == null || !_displayWindow.IsVisible)
            {
                CreateDisplayWindow();
            }

            _currentStep = 0;
            _displayWindow?.ClearScreen();
            _fullScreenWindow?.ClearScreen();
            _log?.LogAdd("画面クリア", _log.INFO);
        }

        /// <summary>
        /// 設定ボタンクリック
        /// </summary>
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("設定画面は未実装です", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
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

            _currentAjsScenario = _scenarioManager.LoadScreenScenario(fileName);
            
            if (_currentAjsScenario != null)
            {
                // DA_Masterから区分一覧を取得
                if (_client?.DataManager?.DA_Master != null)
                {
                    var categories = _scenarioManager.GetAjsCategoriesFromDaMaster(_client.DataManager.DA_Master);
                    
                    // Dictionaryをクリアして再構築
                    _ajsCategoryKeys.Clear();
                    var displayTexts = new List<string>();
                    
                    foreach (var category in categories)
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
                    if (displayTexts.Count > 0)
                        CmbAjsCategory.SelectedIndex = 0;
                    
                    _log?.LogAdd($"AJS区分一覧をDA_Masterから取得: {displayTexts.Count}件", _log.INFO);
                }
                else
                {
                    _log?.LogAdd("DA_Masterが未取得のため、区分一覧を表示できません", _log.WARNING);
                    // MessageBox.Show("サーバーに接続してDA_Masterを取得してください", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private async void CmbAjsCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _log?.LogAdd($"CmbAjsCategory_SelectionChanged開始", _log.DEBUG);
            
            if (CmbAjsCategory.SelectedItem == null)
            {
                _log?.LogAdd("CmbAjsCategory.SelectedItemがnull", _log.DEBUG);
                return;
            }
            
            if (_currentAjsScenario == null)
            {
                _log?.LogAdd("_currentAjsScenarioがnull", _log.DEBUG);
                return;
            }
            
            if (_scenarioManager == null)
            {
                _log?.LogAdd("_scenarioManagerがnull", _log.DEBUG);
                return;
            }

            var displayText = CmbAjsCategory.SelectedItem.ToString();
            _log?.LogAdd($"選択された区分: {displayText}", _log.DEBUG);
            
            if (string.IsNullOrEmpty(displayText))
            {
                _log?.LogAdd("displayTextが空", _log.DEBUG);
                return;
            }

            // Dictionaryからキーを取得
            if (!_ajsCategoryKeys.TryGetValue(displayText, out var key))
            {
                _log?.LogAdd($"Dictionaryにキーが見つかりません: {displayText}", _log.WARNING);
                return;
            }

            // キー形式: "区分No-ラウンドNo"
            var keyParts = key.Split('-');
            _log?.LogAdd($"キー: {key}, keyParts.Length={keyParts.Length}", _log.DEBUG);
            
            if (keyParts.Length != 2)
            {
                _log?.LogAdd($"keyParts.Lengthが2ではない: {keyParts.Length}", _log.WARNING);
                return;
            }

            var kbnNo = keyParts[0];
            var roundNo = keyParts[1];

            // シナリオから画面IDリストを取得し、表示形式に変換
            var displayItems = new List<string>();
            _log?.LogAdd($"シナリオアイテム数: {_currentAjsScenario.Items.Count}", _log.DEBUG);
            
            foreach (var item in _currentAjsScenario.Items)
            {
                // 表示形式: "画面ID : 種目1 W : ヒート 1"
                var displayItem = $"{item.ScreenId} : 種目1 W : ヒート 1";
                displayItems.Add(displayItem);
                _log?.LogAdd($"追加: {displayItem}", _log.DEBUG);
            }

            LstAjsProgress.ItemsSource = displayItems;
            _currentAjsIndex = -1;
            _currentStep = 0;
            
            _log?.LogAdd($"AJS画面進行リスト表示: {displayItems.Count}件 (区分={kbnNo}, ラウンド={roundNo})", _log.INFO);

            // サーバーに DP_ASK_DV_RESULT 電文を送信
            if (_client != null && _client.IsConnected)
            {
                _log?.LogAdd($"DP_ASK_DV_RESULT送信: 区分={kbnNo}, ラウンド={roundNo}", _log.INFO);
                bool ok = await _client.RequestDV_ResultAsync(kbnNo, roundNo);
                if (!ok)
                    _log?.LogAdd("DP_ASK_DV_RESULT送信失敗", _log.WARNING);
            }
            else
            {
                _log?.LogAdd("未接続のためDP_ASK_DV_RESULT送信スキップ", _log.DEBUG);
            }
        }

        private void LstAjsProgress_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentAjsIndex = LstAjsProgress.SelectedIndex;
            _currentStep = 0;
            _log?.LogAdd($"AJS項目選択: {_currentAjsIndex}", _log.DEBUG);
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
        /// 現在のステップを実行
        /// </summary>
        private void ExecuteCurrentStep()
        {
            _log?.LogAdd($"ステップ実行: Step{_currentStep}", _log.INFO);
            
            // 現在選択されているタブを判定
            if (TabControl.SelectedIndex == 1) // AJSタブ
            {
                ExecuteAjsStep();
            }
            else if (TabControl.SelectedIndex == 2) // 表彰式タブ
            {
                ExecuteAwardStep();
            }
            else // 進行タブ
            {
                ExecuteProgressStep();
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
        /// AJSタブのステップを実行
        /// </summary>
        private void ExecuteAjsStep()
        {
            // ウィンドウが閉じられているか非表示の場合は再表示
            if (_displayWindow == null || !_displayWindow.IsVisible)
            {
                CreateDisplayWindow();
            }

            if (_currentAjsScenario == null || LstAjsProgress.ItemsSource == null)
            {
                _log?.LogAdd("AJSシナリオが選択されていません", _log.WARNING);
                return;
            }

            var displayItems = LstAjsProgress.ItemsSource as List<string>;
            if (displayItems == null || _currentAjsIndex < 0 || _currentAjsIndex >= displayItems.Count)
            {
                _log?.LogAdd("AJS項目が選択されていません", _log.WARNING);
                return;
            }

            // 選択された区分情報を取得
            if (CmbAjsCategory.SelectedItem == null)
            {
                _log?.LogAdd("区分が選択されていません", _log.WARNING);
                return;
            }

            var displayText = CmbAjsCategory.SelectedItem.ToString();
            if (string.IsNullOrEmpty(displayText)) return;

            // Dictionaryからキーを取得
            if (!_ajsCategoryKeys.TryGetValue(displayText, out var key))
            {
                _log?.LogAdd($"Dictionaryにキーが見つかりません: {displayText}", _log.WARNING);
                return;
            }

            // キー形式: "区分No-ラウンドNo"
            var keyParts = key.Split('-');
            if (keyParts.Length != 2) return;

            var kbnNo = keyParts[0];
            var roundNo = keyParts[1];

            // シナリオから実際の画面IDを取得
            if (_currentAjsIndex >= _currentAjsScenario.Items.Count)
            {
                _log?.LogAdd($"インデックスが範囲外です: {_currentAjsIndex}", _log.ERR);
                return;
            }

            var item = _currentAjsScenario.Items[_currentAjsIndex];
            _log?.LogAdd($"AJSステップ実行: {item.ScreenId} Step{_currentStep} (区分={kbnNo}, ラウンド={roundNo})", _log.INFO);

            // 画面IDに基づいて適切な画面を表示（部分一致で判定）
            DSDspScreenBase? screen = null;
            
            if (item.ScreenId.StartsWith("DSP_TIT_001"))
            {
                screen = new 画面.DSP_TIT_001_区分ラウンド紹介();
            }
            else if (item.ScreenId.StartsWith("DSP_TIT_002"))
            {
                screen = new 画面.DSP_TIT_002_種目紹介大();
            }
            else if (item.ScreenId.StartsWith("DSP_SOL_001"))
            {
                screen = new 画面.DSP_SOL_001_ソロ選手紹介_大();
            }
            else if (item.ScreenId.StartsWith("DSP_SOL_003"))
            {
                screen = new 画面.DSP_SOL_003_ソロ選手結果GD_大();
            }
            else if (item.ScreenId.StartsWith("DSP_SOL_004"))
            {
                screen = new 画面.DSP_SOL_004_ソロ選手結果_小();
            }

            if (screen == null)
            {
                _log?.LogAdd($"未対応の画面ID: {item.ScreenId}", _log.WARNING);
                MessageBox.Show($"未対応の画面ID: {item.ScreenId}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 初回表示時（Step0）は画面を表示してデータを設定
            if (_currentStep == 0)
            {
                // 画面にデータを設定
                if (_client?.DataManager?.DA_Master != null)
                {
                    screen.DA_Master = _client.DataManager.DA_Master;
                }
                if (_client?.DataManager?.DS_Status != null)
                {
                    screen.DS_Status = _client.DataManager.DS_Status;
                }

                // パラメータを設定（新仕様）
                screen.区分番号 = kbnNo;
                screen.ラウンド番号 = roundNo;
                screen.種目番号 = 1;  // 固定値
                screen.ヒート番号 = 1; // 固定値

                _displayWindow?.ShowScreen(screen);
                _log?.LogAdd($"画面表示: {item.ScreenId}", _log.INFO);

                // スクリーン（全画面）にも同じ画面の別インスタンスを表示
                if (_fullScreenWindow != null)
                {
                    var fullScreen = CreateScreenInstance(screen);
                    if (fullScreen != null)
                        _fullScreenWindow.ShowScreen(fullScreen);
                }
            }

            // 現在表示中の画面を取得
            var currentScreen = _displayWindow?.CurrentScreen as DSDspScreenBase;
            if (currentScreen == null)
            {
                _log?.LogAdd("表示中の画面がありません", _log.WARNING);
                return;
            }

            // ステップを実行（モニター用と全画面用の両方）
            currentScreen.ExecuteStep(_currentStep);
            (_fullScreenWindow?.CurrentScreen as DSDspScreenBase)?.ExecuteStep(_currentStep);
            _log?.LogAdd($"{item.ScreenId} Step{_currentStep}実行完了", _log.INFO);

            // 次のステップへ進むか、次の画面へ移動
            if (_currentStep >= currentScreen.GetTotalSteps() - 1)
            {
                // 現在の画面のステップが完了したら次の画面へ
                _currentAjsIndex++;
                _currentStep = 0;
                
                if (_currentAjsIndex < _currentAjsScenario.Items.Count)
                {
                    LstAjsProgress.SelectedIndex = _currentAjsIndex;
                    _log?.LogAdd($"次の画面へ移動: Index={_currentAjsIndex}", _log.INFO);
                    
                    // 次の画面を自動的に表示（Step0を実行）
                    ExecuteAjsStep();
                }
                else
                {
                    _log?.LogAdd("すべての画面が完了しました", _log.INFO);
                    MessageBox.Show("すべての画面が完了しました", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
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
                画面.DSP_TIT_001_区分ラウンド紹介 => new 画面.DSP_TIT_001_区分ラウンド紹介(),
                画面.DSP_TIT_002_種目紹介大      => new 画面.DSP_TIT_002_種目紹介大(),
                画面.DSP_SOL_001_ソロ選手紹介_大  => new 画面.DSP_SOL_001_ソロ選手紹介_大(),
                画面.DSP_SOL_003_ソロ選手結果GD_大 => new 画面.DSP_SOL_003_ソロ選手結果GD_大(),
                画面.DSP_SOL_004_ソロ選手結果_小  => new 画面.DSP_SOL_004_ソロ選手結果_小(),
                _ => null
            };

            if (dest != null)
            {
                dest.DA_Master   = source.DA_Master;
                dest.DS_Status   = source.DS_Status;
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
    }
}

// Made with Bob
