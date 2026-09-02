using System;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DSDsp.画面
{
    /// <summary>
    /// DSDsp画面の基底クラス。
    ///
    /// 【ステップ進行の仕組み】
    ///   外部（MainWindow）から Advance() を呼ぶたびに次のステップへ進む。
    ///   画面が全ステップを完了したとき ScreenCompleted イベントを発火する。
    ///   MainWindow は ScreenCompleted を受け取ったときだけ次の画面へ遷移する。
    ///   → MainWindow 側にステップカウンターは不要。
    ///
    /// 【完了の通知タイミング】
    ///   フェードアウトアニメーションがある最終ステップ：
    ///     Storyboard.Completed コールバックから RaiseScreenCompleted() を呼ぶ。
    ///   フェードアウトがない最終ステップ：
    ///     Advance() 内で _currentStep が TotalSteps を超えたとき自動発火。
    ///
    /// 【途中停止（クロマキーモード等）】
    ///   画面クラスが自分で判断して Advance() 呼び出しを無視するか、
    ///   あるいは次ステップを実行した後 ScreenCompleted を発火しないことで停止する。
    ///   HoldsAfterFadeOut=true の画面はフェードアウト完了後も停止し、
    ///   次の Advance() で ScreenCompleted を発火する。
    /// </summary>
    public abstract class DSDspScreenBase : UserControl, IDisposable
    {
        #region フィールド
        protected パーツ.COM000_PartsMain? _partsMain;
        protected DispatcherTimer? _timer;
        private   DispatcherTimer? _clockTimer;
        protected int _currentStep = 0;
        protected bool _disposed = false;

        // データソース
        private JsonNode? _daMaster;
        private JsonNode? _dsStatus;
        private JsonNode? _dvResult;
        private string _kbnNo   = string.Empty;
        private string _rndNo   = string.Empty;
        private string _dGrpNo  = string.Empty;
        private int _dncNo = 0;
        private int _heatNo = 0;
        private bool _isOverviewMode = false;
        private bool _chromaKeyMode = false;
        #endregion

        #region イベント
        /// <summary>
        /// 画面の全ステップが完了したときに発火する。
        /// MainWindow はこのイベントを受け取ったときに次の画面へ遷移する。
        /// </summary>
        public event EventHandler? ScreenCompleted;

        // 旧互換：LastStepFadeOutCompleted は ScreenCompleted の別名として残す
        [Obsolete("ScreenCompleted を使用してください")]
        public event EventHandler? LastStepFadeOutCompleted
        {
            add    => ScreenCompleted += value;
            remove => ScreenCompleted -= value;
        }
        #endregion

        #region プロパティ
        public JsonNode? DA_Master  { get => _daMaster;  set => _daMaster  = value; }
        public JsonNode? DS_Status  { get => _dsStatus;  set => _dsStatus  = value; }
        public JsonNode? DV_Result  { get => _dvResult;  set => _dvResult  = value; }
        public string    区分番号   { get => _kbnNo;     set => _kbnNo     = value; }
        public string    ラウンド番号{ get => _rndNo;     set => _rndNo     = value; }
        public string    DGrpNo     { get => _dGrpNo;    set => _dGrpNo    = value; }
        public int       種目番号   { get => _dncNo;     set => _dncNo     = value; }
        public int       ヒート番号
        {
            get => _heatNo;
            set
            {
                _heatNo = value;
                OnHeatNoChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// ヒート番号プロパティが変化したときに呼ばれるコールバック。
        /// MainWindow がセットして、コンボボックスをリアルタイム同期するために使用する。
        /// </summary>
        public Action<int>? OnHeatNoChanged { get; set; }

        /// <summary>
        /// 画面ID（例: "DSP_GRP_001"）。MainWindow が設定する。
        /// 自動表示ロジックでの画面判定に使用する。
        /// </summary>
        public string ScreenId { get; set; } = string.Empty;

        /// <summary>デュエルヒート表の一覧表示モード。</summary>
        public bool IsOverviewMode  { get => _isOverviewMode; set => _isOverviewMode = value; }

        /// <summary>
        /// クロマキーモードかどうか。
        /// DSP_GRP_001/002 で Step5（LST005フェードイン）後に自動進行せず停止する。
        /// </summary>
        public bool ChromaKeyMode   { get => _chromaKeyMode;  set => _chromaKeyMode  = value; }

        /// <summary>種目内の最終ヒートかどうか。</summary>
        public bool IsLastHeatInDance { get; set; } = false;

        /// <summary>
        /// HeatEnd 通知受信時に MainWindow から呼ばれる。
        /// 派生クラスは、フェーズが未完了であれば必要なステップを補完し、
        /// 次ヒートへの切り替えを自律的に処理する。
        /// デフォルト実装は何もしない（Advance() にフォールバックさせる）。
        /// </summary>
        public virtual void NotifyHeatChanged() { }

        /// <summary>タイマー間隔（秒）- 派生クラスでオーバーライド可能</summary>
        protected virtual int TimerIntervalSeconds => 10;

        /// <summary>ステップ数 - 派生クラスでオーバーライド必須</summary>
        protected abstract int TotalSteps { get; }

        // ── 旧互換プロパティ（MainWindow 移行後に除去予定） ──

        /// <summary>
        /// [旧互換] 最終ステップのフェードアウト完了後に ScreenCompleted を発火する画面では true。
        /// 新設計では各画面が RaiseScreenCompleted() を適切なタイミングで呼ぶため不要。
        /// </summary>
        [Obsolete("新設計では各画面が RaiseScreenCompleted() を呼ぶため不要")]
        public virtual bool WaitsForLastStepFadeOut => false;

        /// <summary>
        /// [旧互換] フェードアウト完了後に自動遷移せず停止する画面では true。
        /// 新設計では HoldsAfterFadeOut=true の画面は Advance() を一度無視することで停止する。
        /// </summary>
        public virtual bool HoldsAfterFadeOut => false;

        /// <summary>
        /// HoldsAfterFadeOut=true でフェードアウト完了後の停止時に MainWindow から呼ばれる。
        /// </summary>
        public virtual void OnHoldsAfterFadeOut() { }
        #endregion

        #region コンストラクタ
        protected DSDspScreenBase()
        {
            this.Loaded   += DSDspScreenBase_Loaded;
            this.Unloaded += DSDspScreenBase_Unloaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSDspScreenBase_Loaded(object sender, RoutedEventArgs e)
        {
            EnsurePartsMainInitialized();
        }

        private void DSDspScreenBase_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }
        #endregion

        #region パブリックメソッド

        /// <summary>
        /// 再生ボタンが押されたときに MainWindow から呼ばれる。
        /// 内部で _currentStep を進めて ExecuteCurrentStep() を実行する。
        /// 全ステップ完了かつフェードアウトがない画面は、ここで ScreenCompleted を発火する。
        /// </summary>
        public void Advance()
        {
            ExecuteCurrentStep();
            _currentStep++;

            // フェードアウトを伴わない画面は最終ステップ実行後に自動完了
            // フェードアウトがある画面（WaitsForLastStepFadeOut=true 相当）は
            // 自分の Storyboard.Completed から RaiseScreenCompleted() を呼ぶ
            if (!WaitsForLastStepFadeOut && _currentStep >= TotalSteps)
            {
                RaiseScreenCompleted();
            }
        }

        /// <summary>
        /// 指定されたステップを実行（旧互換。MainWindow 移行後は Advance() に統一）。
        /// </summary>
        [Obsolete("Advance() を使用してください")]
        public void ExecuteStep(int step)
        {
            if (step > _currentStep)
                _currentStep = step;
            ExecuteCurrentStep();
        }

        /// <summary>自動実行（タイマー）を開始する。</summary>
        public void StartAutoExecution()
        {
            _currentStep = 0;
            StopTimer();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(TimerIntervalSeconds);
            _timer.Tick += Timer_Tick;
            ExecuteCurrentStep();
            _timer.Start();
        }

        /// <summary>自動実行を停止する。</summary>
        public void StopAutoExecution() => StopTimer();

        /// <summary>現在のステップ番号を取得。</summary>
        public int CurrentStep => _currentStep;

        /// <summary>
        /// ステップカウンターを 0 にリセットする。
        /// クロマキ_個別モードで同一画面インスタンスを次選手に再利用する際に使用。
        /// </summary>
        public void ResetSteps() => _currentStep = 0;

        /// <summary>総ステップ数を取得（旧互換）。</summary>
        [Obsolete("外部からの参照は不要になりました")]
        public int GetTotalSteps() => TotalSteps;
        #endregion

        #region プロテクテッドメソッド

        /// <summary>
        /// 全ステップ完了を通知する。
        /// フェードアウトアニメーション完了コールバックからこれを呼ぶ。
        /// </summary>
        protected void RaiseScreenCompleted()
        {
            ScreenCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// [旧互換] RaiseScreenCompleted() の旧名。
        /// </summary>
        [Obsolete("RaiseScreenCompleted() を使用してください")]
        protected void RaiseLastStepFadeOutCompleted() => RaiseScreenCompleted();

        /// <summary>
        /// 現在のステップを実行 - 派生クラスでオーバーライド必須。
        /// _currentStep の値を見てステップごとの処理を実行する。
        /// このメソッド内で _currentStep を変更してはならない（Advance() が管理する）。
        /// </summary>
        protected abstract void ExecuteCurrentStep();

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _currentStep++;
            if (_currentStep < TotalSteps)
                ExecuteCurrentStep();
            else
                StopTimer();
        }

        protected void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
                _timer = null;
            }
        }

        /// <summary>
        /// COM002（右上）の現在時刻表示を30秒ごとに自動更新するタイマーを開始する。
        /// 各画面で COM002 に時刻をセットした直後に呼ぶ。
        /// 画面が Unload（Dispose）されると自動停止する。
        /// </summary>
        protected void StartClock()
        {
            StopClock();
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
        }

        /// <summary>時刻自動更新タイマーを停止する。</summary>
        protected void StopClock()
        {
            if (_clockTimer != null)
            {
                _clockTimer.Stop();
                _clockTimer.Tick -= ClockTimer_Tick;
                _clockTimer = null;
            }
        }

        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClock();
        }

        /// <summary>COM002 の LB_右上 を現在時刻で更新する。</summary>
        private void UpdateClock()
        {
            if (this.FindName("PartsCOM002") is not FrameworkElement com002) return;
            if (com002.FindName("LB_右上") is Label lb)
                lb.Content = $"現在時刻　{DateTime.Now:HH:mm}";
        }

        protected virtual void EnsurePartsMainInitialized()
        {
            if (_partsMain == null)
                _partsMain = new パーツ.COM000_PartsMain();
        }

        /// <summary>外部から PartsMain の初期化を呼び出すためのパブリックラッパー。</summary>
        public void EnsurePartsInitialized() => EnsurePartsMainInitialized();

        /// <summary>PartsMain インスタンスへのパブリックアクセサー（フォントサイズ調整などに使用）。</summary>
        public パーツ.COM000_PartsMain? PartsMainInstance => _partsMain;

        #endregion

        #region 共通UIヘルパー
        protected string SetCommonHeader(
            System.Windows.Controls.TextBlock tb左上1,
            System.Windows.Controls.TextBlock tb左上2,
            System.Windows.Controls.Label lb右上)
        {
            if (DA_Master == null)
            {
                tb左上1.Text = "データなし";
                tb左上2.Text = string.Empty;
                lb右上.Content = string.Empty;
                return string.Empty;
            }

            tb左上1.Text = DSDspDataHelper.Get競技会名(DA_Master);

            string 区分名 = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
            string ラウンド名 = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);
            tb左上2.Text = 区分名 + "　" + ラウンド名;

            lb右上.Content = DSDspDataHelper.Get種目表示テキスト(DA_Master, 区分番号, ラウンド番号, 種目番号);

            return DSDspDataHelper.Get採点方式ID(DA_Master, 区分番号, ラウンド番号);
        }

        protected void CreateAndStartSlideAnimation(UIElement target, double fromOffset, double durationSeconds = 1.0)
        {
            var storyboard = new Storyboard();
            var slideAnimation = new DoubleAnimation
            {
                From = fromOffset,
                To = 0,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            Storyboard.SetTarget(slideAnimation, target);
            Storyboard.SetTargetProperty(slideAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            storyboard.Children.Add(slideAnimation);
            storyboard.Begin();
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                StopTimer();
                StopClock();
                _partsMain = null;
            }
            _disposed = true;
        }

        ~DSDspScreenBase()
        {
            Dispose(false);
        }
        #endregion
    }
}
