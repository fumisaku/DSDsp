using System;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
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
        
        // データソース
        private JsonNode? _daMaster;
        private JsonNode? _dsStatus;
        private string _kbnNo = string.Empty;
        private string _rndNo = string.Empty;
        private int _dncNo = 0;
        private int _heatNo = 0;
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
