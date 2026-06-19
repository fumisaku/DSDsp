using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_TIT_001_区分ラウンド紹介.xaml の相互作用ロジック
    /// </summary>
    public partial class DSP_TIT_001_区分ラウンド紹介 : UserControl, IDisposable
    {
        #region 定数定義
        private const int TIMER_INTERVAL_SECONDS = 10;
        private const int ANIMATION_DURATION_SECONDS = 1;
        private const double SLIDE_FROM_LEFT = -1000;
        private const double SLIDE_FROM_RIGHT = 1000;
        private const double SLIDE_TO_LEFT_POSITION = 57;
        private const double SLIDE_TO_RIGHT_POSITION = 50;
        private const int FADE_DELAY_MILLISECONDS = 1000;
        
        // フォントサイズ調整用の定数
        private const double DEFAULT_FONT_SIZE = 20;
        private const double MIN_FONT_SIZE = 12;
        private const int SHORT_TEXT_LENGTH = 15;
        private const int MEDIUM_TEXT_LENGTH = 25;
        #endregion

        #region フィールド
        private パーツ.COM000_PartsMain? _partsMain;
        private DispatcherTimer? _timer;
        private int _currentStep = 0;
        private bool _disposed = false;
        #endregion

        #region プロパティ
        /// <summary>
        /// 競技会名（外部から設定可能）
        /// </summary>
        public string 競技会名 { get; set; } = "PDグランプリテスト大会";

        /// <summary>
        /// 区分名（外部から設定可能）
        /// </summary>
        public string 区分名 { get; set; } = "プロフェッショナル部門";

        /// <summary>
        /// ラウンド名（外部から設定可能）
        /// </summary>
        public string ラウンド名 { get; set; } = "準決勝ラウンド";
        #endregion

        #region コンストラクタ
        public DSP_TIT_001_区分ラウンド紹介()
        {
            InitializeComponent();
            this.Loaded += DSP_TIT_001_区分ラウンド紹介_Loaded;
            this.Unloaded += DSP_TIT_001_区分ラウンド紹介_Unloaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSP_TIT_001_区分ラウンド紹介_Loaded(object sender, RoutedEventArgs e)
        {
            // 画面読み込み時に自動実行を開始
            StartAutoExecution();
        }

        private void DSP_TIT_001_区分ラウンド紹介_Unloaded(object sender, RoutedEventArgs e)
        {
            // 画面アンロード時にリソースを解放
            Dispose();
        }
        #endregion

        #region パブリックメソッド
        /// <summary>
        /// 10秒間隔でStep1→Step2→Step3を自動実行
        /// </summary>
        public void StartAutoExecution()
        {
            _currentStep = 0;
            
            // 既存のタイマーがあれば停止
            StopTimer();
            
            // タイマーを設定
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(TIMER_INTERVAL_SECONDS);
            _timer.Tick += Timer_Tick;
            
            // 最初のStep1を即座に実行
            ExecuteCurrentStep();
            
            // タイマー開始
            _timer.Start();
        }
        #endregion

        #region プライベートメソッド
        private void Timer_Tick(object? sender, EventArgs e)
        {
            _currentStep++;
            
            if (_currentStep <= 2)
            {
                ExecuteCurrentStep();
            }
            else
            {
                // Step3まで完了したらタイマー停止
                StopTimer();
            }
        }

        private void ExecuteCurrentStep()
        {
            switch (_currentStep)
            {
                case 0:
                    Step1(競技会名);
                    break;
                case 1:
                    Step2(区分名, ラウンド名);
                    break;
                case 2:
                    Step3();
                    break;
            }
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
                _timer = null;
            }
        }

        private void EnsurePartsMainInitialized()
        {
            if (_partsMain == null)
            {
                _partsMain = new パーツ.COM000_PartsMain();
                // 初回のみ非表示を実行
                非表示();
            }
        }

        private void 非表示()
        {
            // 区分ラウンド名を非表示
            PartsCOM001.TB_左上2.Text = string.Empty;

            // TIT001 のタイトルと画像を非表示
            PartsTIT001.LB_Title1.Visibility = Visibility.Collapsed;
            PartsTIT001.LB_Title2.Visibility = Visibility.Collapsed;
            PartsTIT001.IM_1.Visibility = Visibility.Collapsed;
            PartsTIT001.IM_2.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Step1: 競技会名を表示
        /// </summary>
        /// <param name="競技会名">表示する競技会名</param>
        public void Step1(string 競技会名)
        {
            // COM000_PartsMainを初期化
            EnsurePartsMainInitialized();

            // COM001 の　TB_左上2(区分＋ラウンド名)をクリアする
            PartsCOM001.TB_左上2.Text = string.Empty;

            // COM001 のロゴを表示する
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
    
            // COM001 のTB_左上1 に競技会名をセットする
            PartsCOM001.TB_左上1.Text = 競技会名;
        }

        /// <summary>
        /// Step2: 区分名とラウンド名をアニメーション表示
        /// </summary>
        /// <param name="区分名">表示する区分名</param>
        /// <param name="ラウンド名">表示するラウンド名</param>
        public void Step2(string 区分名, string ラウンド名)
        {
            // PartsMainの初期化確認
            EnsurePartsMainInitialized();
            
            if (_partsMain == null) return;

            // 画像とタイトルを表示状態に設定
            PartsTIT001.IM_1.Visibility = Visibility.Visible;
            PartsTIT001.IM_2.Visibility = Visibility.Visible;
            PartsTIT001.LB_Title1.Visibility = Visibility.Visible;
            PartsTIT001.LB_Title2.Visibility = Visibility.Visible;

            // 画像の初期状態を設定（フェードイン用に透明にする）
            PartsTIT001.IM_1.Opacity = 0;
            PartsTIT001.IM_2.Opacity = 0;

            // 画像のフェードインアニメーション
            var imageStoryboard = new Storyboard();
            _partsMain.フェードイン(true, PartsTIT001.IM_1, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsTIT001.IM_2, imageStoryboard, 0);
            imageStoryboard.Begin();

            // 画像のスライドアニメーション
            CreateAndStartSlideAnimation(PartsTIT001.IM_2, SLIDE_FROM_LEFT, SLIDE_TO_LEFT_POSITION);
            CreateAndStartSlideAnimation(PartsTIT001.IM_1, SLIDE_FROM_RIGHT, SLIDE_TO_RIGHT_POSITION);

            // タイトルテキストの設定とフォントサイズの自動調整
            PartsTIT001.LB_Title1.Content = 区分名;
            PartsTIT001.LB_Title2.Content = ラウンド名;
            
            AdjustFontSize(PartsTIT001.LB_Title1, 区分名);
            AdjustFontSize(PartsTIT001.LB_Title2, ラウンド名);

            // タイトルのフェードインアニメーション
            var titleStoryboard = new Storyboard();
            PartsTIT001.LB_Title1.Opacity = 0;
            PartsTIT001.LB_Title2.Opacity = 0;
            _partsMain.フェードイン(true, PartsTIT001.LB_Title1, titleStoryboard, FADE_DELAY_MILLISECONDS);
            _partsMain.フェードイン(true, PartsTIT001.LB_Title2, titleStoryboard, FADE_DELAY_MILLISECONDS);
            titleStoryboard.Begin();
        }

        private void CreateAndStartSlideAnimation(UIElement target, double fromPosition, double toPosition)
        {
            var storyboard = new Storyboard();
            var slideAnimation = new DoubleAnimation
            {
                From = fromPosition,
                To = toPosition,
                Duration = TimeSpan.FromSeconds(ANIMATION_DURATION_SECONDS)
            };

            Storyboard.SetTarget(slideAnimation, target);
            Storyboard.SetTargetProperty(slideAnimation, new PropertyPath("(Canvas.Left)"));
            storyboard.Children.Add(slideAnimation);
            storyboard.Begin();
        }

        /// <summary>
        /// 文字列の長さに応じてフォントサイズを自動調整
        /// </summary>
        /// <param name="label">対象のLabel</param>
        /// <param name="text">表示するテキスト</param>
        private void AdjustFontSize(Label label, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                label.FontSize = DEFAULT_FONT_SIZE;
                return;
            }

            int textLength = text.Length;
            double fontSize;

            if (textLength <= SHORT_TEXT_LENGTH)
            {
                // 短い文字列: デフォルトサイズ (20)
                fontSize = DEFAULT_FONT_SIZE;
            }
            else if (textLength <= MEDIUM_TEXT_LENGTH)
            {
                // 中程度の文字列: 線形に縮小 (20 → 16)
                fontSize = DEFAULT_FONT_SIZE - ((textLength - SHORT_TEXT_LENGTH) * 0.4);
            }
            else
            {
                // 長い文字列: さらに縮小 (16 → 12)
                fontSize = 16 - ((textLength - MEDIUM_TEXT_LENGTH) * 0.2);
                fontSize = Math.Max(fontSize, MIN_FONT_SIZE); // 最小サイズを保証
            }

            label.FontSize = fontSize;
        }

        /// <summary>
        /// Step3: タイトルと画像をフェードアウト
        /// </summary>
        public void Step3()
        {
            // TIT001 の LB_Title1 と LB_Title2 をクリア
            // TIT001 の IM_1 と IM_2 をフェードアウト

            EnsurePartsMainInitialized();
            
            if (_partsMain == null) return;

            var fadeOutStoryboard = new Storyboard();

            _partsMain.フェードアウト(true, PartsTIT001.LB_Title1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT001.LB_Title2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT001.IM_1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT001.IM_2, fadeOutStoryboard, 0);

            fadeOutStoryboard.Begin();
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

        ~DSP_TIT_001_区分ラウンド紹介()
        {
            Dispose(false);
        }
        #endregion
    }
}
