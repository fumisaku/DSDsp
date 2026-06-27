using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_TIT_001_区分ラウンド紹介.xaml の相互作用ロジック
    /// </summary>
    public partial class DSP_TIT_001_区分ラウンド紹介 : DSDspScreenBase
    {
        #region 定数定義
        private const int ANIMATION_DURATION_SECONDS = 1;
        private const double SLIDE_FROM_LEFT = -1000;
        private const double SLIDE_FROM_RIGHT = 1000;
        private const int FADE_DELAY_MILLISECONDS = 1000;
        
        // フォントサイズ調整用の定数
        private const double MAX_FONT_SIZE = 24;
        private const double MIN_FONT_SIZE = 6;
        private const double MAX_TEXT_WIDTH = 500;
        private const string FONT_FAMILY_NAME = "HGPSoeiKakugothicUB";
        #endregion

        #region オーバーライド
        /// <summary>
        /// ステップ数（Step0, Step1, Step2, Step3の4ステップ）
        /// </summary>
        protected override int TotalSteps => 4;
        #endregion




        #region コンストラクタ
        public DSP_TIT_001_区分ラウンド紹介()
        {
            InitializeComponent();
        }
        #endregion

        #region オーバーライドメソッド
        protected override void ExecuteCurrentStep()
        {
            switch (_currentStep)
            {
                case 0:
                    Step1();
                    break;
                case 1:
                    Step2();
                    break;
                case 2:
                    Step3();
                    break;
            }
        }

        protected override void EnsurePartsMainInitialized()
        {
            base.EnsurePartsMainInitialized();
            if (_partsMain != null)
            {
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
        public void Step1()
        {
            // COM000_PartsMainを初期化
            EnsurePartsMainInitialized();

            // COM001 の　TB_左上2(区分＋ラウンド名)をクリアする
            PartsCOM001.TB_左上2.Text = string.Empty;

            // COM001 のロゴを表示する
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
    
            // ヘルパーを使用して競技会名を取得（基底クラスのプロパティを使用）
            string 競技会名 = DSDspDataHelper.Get競技会名(DA_Master);
            PartsCOM001.TB_左上1.Text = 競技会名;
        }

        /// <summary>
        /// Step2: 区分名とラウンド名をアニメーション表示
        /// </summary>
        public void Step2()
        {
            // PartsMainの初期化確認
            EnsurePartsMainInitialized();
            
            if (_partsMain == null) return;

            // ヘルパーを使用して区分名とラウンド名を取得（基底クラスのプロパティを使用）
            string 区分名 = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
            string ラウンド名 = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);

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

            // 画像のスライドアニメーション（RenderTransform.X を使用し IM_* 要素のみ移動）
            CreateAndStartSlideAnimation(PartsTIT001.IM_2, SLIDE_FROM_LEFT);
            CreateAndStartSlideAnimation(PartsTIT001.IM_1, SLIDE_FROM_RIGHT);

            // タイトルテキストの設定とフォントサイズの自動調整
            PartsTIT001.LB_Title1.Content = 区分名;
            PartsTIT001.LB_Title2.Content = ラウンド名;
            
            // COM000_PartsMainの共通機能を使用してフォントサイズを自動調整
            // 全てのパラメータを明示的に指定
            _partsMain.フォントサイズ自動調整(
                label: PartsTIT001.LB_Title1,
                text: 区分名,
                maxWidth: MAX_TEXT_WIDTH,
                maxFontSize: MAX_FONT_SIZE,
                minFontSize: MIN_FONT_SIZE,
                fontFamilyName: FONT_FAMILY_NAME);
            
            _partsMain.フォントサイズ自動調整(
                label: PartsTIT001.LB_Title2,
                text: ラウンド名,
                maxWidth: MAX_TEXT_WIDTH,
                maxFontSize: MAX_FONT_SIZE,
                minFontSize: MIN_FONT_SIZE,
                fontFamilyName: FONT_FAMILY_NAME);

            // タイトルのフェードインアニメーション
            var titleStoryboard = new Storyboard();
            PartsTIT001.LB_Title1.Opacity = 0;
            PartsTIT001.LB_Title2.Opacity = 0;
            _partsMain.フェードイン(true, PartsTIT001.LB_Title1, titleStoryboard, FADE_DELAY_MILLISECONDS);
            _partsMain.フェードイン(true, PartsTIT001.LB_Title2, titleStoryboard, FADE_DELAY_MILLISECONDS);
            titleStoryboard.Begin();
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
    }
}
