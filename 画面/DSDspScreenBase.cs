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
    /// DSDsp画面の基底クラス
    /// 共通プロパティとタイマー制御を提供
    /// </summary>
    public abstract class DSDspScreenBase : UserControl, IDisposable
    {
        #region フィールド
        protected パーツ.COM000_PartsMain? _partsMain;
        protected DispatcherTimer? _timer;
        protected int _currentStep = 0;
        protected bool _disposed = false;

        /// <summary>
        /// 最終ステップのフェードアウトアニメーションが完了したときに発火するイベント。
        /// このイベントが設定されている場合、MainWindowは次の画面の表示をこのイベント完了まで待つ。
        /// </summary>
        public event EventHandler? LastStepFadeOutCompleted;
        
        // データソース
        private JsonNode? _daMaster;
        private JsonNode? _dsStatus;
        private JsonNode? _dvResult;
        private string _kbnNo = string.Empty;
        private string _rndNo = string.Empty;
        private int _dncNo = 0;
        private int _heatNo = 0;
        private bool _isOverviewMode = false;
        #endregion

        #region プロパティ
        /// <summary>
        /// DA_Master（競技会マスタ）
        /// </summary>
        public JsonNode? DA_Master
        {
            get => _daMaster;
            set => _daMaster = value;
        }

        /// <summary>
        /// DS_Status（競技会進行状況）
        /// </summary>
        public JsonNode? DS_Status
        {
            get => _dsStatus;
            set => _dsStatus = value;
        }

        /// <summary>
        /// DV_Result（採点結果）
        /// </summary>
        public JsonNode? DV_Result
        {
            get => _dvResult;
            set => _dvResult = value;
        }

        /// <summary>
        /// 区分番号
        /// </summary>
        public string 区分番号
        {
            get => _kbnNo;
            set => _kbnNo = value;
        }

        /// <summary>
        /// ラウンド番号
        /// </summary>
        public string ラウンド番号
        {
            get => _rndNo;
            set => _rndNo = value;
        }

        /// <summary>
        /// 種目番号
        /// </summary>
        public int 種目番号
        {
            get => _dncNo;
            set => _dncNo = value;
        }

        /// <summary>
        /// ヒート番号
        /// </summary>
        public int ヒート番号
        {
            get => _heatNo;
            set => _heatNo = value;
        }

        /// <summary>
        /// デュエルヒート表の一覧表示モード。
        /// true の場合、種目内の全ヒート選手一覧をヒート番号付きで表示する。
        /// </summary>
        public bool IsOverviewMode
        {
            get => _isOverviewMode;
            set => _isOverviewMode = value;
        }

        /// <summary>
        /// タイマー間隔（秒）- 派生クラスでオーバーライド可能
        /// </summary>
        protected virtual int TimerIntervalSeconds => 10;

        /// <summary>
        /// ステップ数 - 派生クラスでオーバーライド必須
        /// </summary>
        protected abstract int TotalSteps { get; }
        #endregion

        #region コンストラクタ
        protected DSDspScreenBase()
        {
            this.Loaded += DSDspScreenBase_Loaded;
            this.Unloaded += DSDspScreenBase_Unloaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSDspScreenBase_Loaded(object sender, RoutedEventArgs e)
        {
            // 画面読み込み時は自動実行しない（外部から制御）
            // 初期化のみ実行
            EnsurePartsMainInitialized();
        }

        private void DSDspScreenBase_Unloaded(object sender, RoutedEventArgs e)
        {
            // 画面アンロード時にリソースを解放
            Dispose();
        }
        #endregion

        #region パブリックメソッド
        /// <summary>
        /// 指定間隔でステップを自動実行
        /// </summary>
        public void StartAutoExecution()
        {
            _currentStep = 0;

            // 既存のタイマーがあれば停止
            StopTimer();

            // タイマーを設定
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(TimerIntervalSeconds);
            _timer.Tick += Timer_Tick;

            // 最初のステップを即座に実行
            ExecuteCurrentStep();

            // タイマー開始
            _timer.Start();
        }

        /// <summary>
        /// タイマーを停止
        /// </summary>
        public void StopAutoExecution()
        {
            StopTimer();
        }

        /// <summary>
        /// 指定されたステップを実行（外部から制御する場合に使用）
        /// </summary>
        /// <param name="step">実行するステップ番号</param>
        public void ExecuteStep(int step)
        {
            _currentStep = step;
            ExecuteCurrentStep();
        }

        /// <summary>
        /// 現在のステップ番号を取得
        /// </summary>
        public int CurrentStep => _currentStep;

        /// <summary>
        /// 総ステップ数を取得
        /// </summary>
        public int GetTotalSteps() => TotalSteps;

        /// <summary>
        /// 最終ステップでフェードアウト完了まで次画面への遷移を待機するかどうか。
        /// true にした画面では、最終Step の Storyboard.Completed で RaiseLastStepFadeOutCompleted() を呼ぶこと。
        /// デフォルトは false（即時遷移）。
        /// </summary>
        public virtual bool WaitsForLastStepFadeOut => false;

        /// <summary>
        /// 最終ステップのフェードアウトアニメーション完了を通知する。
        /// 最終 Step の Storyboard.Completed コールバックから呼び出すこと。
        /// </summary>
        protected void RaiseLastStepFadeOutCompleted()
        {
            LastStepFadeOutCompleted?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region プロテクテッドメソッド
        /// <summary>
        /// タイマーティック処理
        /// </summary>
        private void Timer_Tick(object? sender, EventArgs e)
        {
            _currentStep++;

            if (_currentStep < TotalSteps)
            {
                ExecuteCurrentStep();
            }
            else
            {
                // 全ステップ完了したらタイマー停止
                StopTimer();
            }
        }

        /// <summary>
        /// 現在のステップを実行 - 派生クラスでオーバーライド必須
        /// </summary>
        protected abstract void ExecuteCurrentStep();

        /// <summary>
        /// タイマーを停止
        /// </summary>
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
        /// PartsMainの初期化を確認 - 派生クラスでオーバーライド可能
        /// </summary>
        protected virtual void EnsurePartsMainInitialized()
        {
            if (_partsMain == null)
            {
                _partsMain = new パーツ.COM000_PartsMain();
            }
        }
        #endregion

        #region 共通UIヘルパー
        /// <summary>
        /// COM001/COM002 の標準ヘッダ（競技会名・区分ラウンド名・種目情報）を設定する。
        /// PartsCOM001/PartsCOM002 という名前のパーツを持つ派生クラスで呼び出すこと。
        /// 採点方式IDが必要な場合は戻り値（string）として受け取れる。
        /// </summary>
        /// <returns>採点方式ID。データが不足している場合は空文字</returns>
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

        /// <summary>
        /// UIElementをX軸方向にスライドインさせるアニメーションを開始する。
        /// 全画面共通のスライド演出（左右から飛び込み）に使用。
        /// </summary>
        /// <param name="target">アニメーション対象のUI要素</param>
        /// <param name="fromOffset">開始オフセット（負=左から、正=右から）</param>
        /// <param name="durationSeconds">アニメーション時間（秒）。デフォルト1秒</param>
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

        #region IDisposable実装
        /// <summary>
        /// リソースの解放
        /// </summary>
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
                // マネージドリソースの解放
                StopTimer();
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

// Made with Bob
