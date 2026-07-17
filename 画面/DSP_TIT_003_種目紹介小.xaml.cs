using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_TIT_003_種目紹介小.xaml の相互作用ロジック
    /// TIT003（下部バー）パーツを使用した種目紹介画面（小）。
    /// TIT003 は LB_種目順・LB_種目紹介・IM_種目1・IM_種目2 を持つ。
    /// ステップ構成: Step1(ヘッダ設定) → Step2(種目情報アニメーション表示) → Step3(フェードアウト)
    /// </summary>
    public partial class DSP_TIT_003_種目紹介小 : DSDspScreenBase
    {
        #region 定数定義
        private const double SLIDE_FROM_LEFT = -1000;
        private const double SLIDE_FROM_RIGHT = 1000;
        private const int FADE_DELAY_MILLISECONDS = 1000;

        private const double MAX_FONT_SIZE = 16;
        private const double MIN_FONT_SIZE = 8;
        private const double MAX_TEXT_WIDTH = 400;
        private const string FONT_FAMILY_NAME = "Segoe UI Semibold";
        #endregion

        #region プロパティ
        /// <summary>総ステップ数（Step1(1) + Step2(1) + Step3(1) の3ステップ）</summary>
        protected override int TotalSteps => 3;
        public override bool WaitsForLastStepFadeOut => true;
        public override bool HoldsAfterFadeOut => true;
        #endregion

        #region コンストラクタ
        public DSP_TIT_003_種目紹介小()
        {
            InitializeComponent();
            this.Loaded += DSP_TIT_003_種目紹介小_Loaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSP_TIT_003_種目紹介小_Loaded(object sender, RoutedEventArgs e)
        {
            EnsurePartsMainInitialized();
        }
        #endregion

        #region オーバーライドメソッド
        /// <summary>現在のステップを実行</summary>
        protected override void ExecuteCurrentStep()
        {
            switch (_currentStep)
            {
                case 0: Step1(); break;
                case 1: Step2(); break;
                case 2: Step3(); break;
            }
        }
        #endregion

        #region プライベートメソッド

        private void 非表示()
        {
            PartsCOM001.TB_左上2.Text = string.Empty;

            PartsTIT003.LB_種目順.Visibility   = Visibility.Collapsed;
            PartsTIT003.LB_種目紹介.Visibility  = Visibility.Collapsed;
            PartsTIT003.IM_種目1.Visibility     = Visibility.Collapsed;
            PartsTIT003.IM_種目2.Visibility     = Visibility.Collapsed;
        }

        /// <summary>
        /// Step1: 競技会名・区分名・ラウンド名をヘッダに設定し、TIT003 を非表示にリセットする。
        /// </summary>
        public void Step1()
        {
            EnsurePartsMainInitialized();

            非表示();

            PartsCOM002.LB_右上.Content = string.Empty;

            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(
                new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));

            PartsCOM001.TB_左上1.Text = DSDspDataHelper.Get競技会名(DA_Master);
            string 区分名 = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
            string ラウンド名 = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);
            PartsCOM001.TB_左上2.Text = 区分名 + "　" + ラウンド名;
        }

        /// <summary>
        /// Step2: 種目情報をアニメーション付きで表示する。
        /// TIT003 は LB_種目カテゴリ を持たないため、LB_種目順・LB_種目紹介のみ表示する。
        /// </summary>
        public void Step2()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            string 種目カテゴリ = DSDspDataHelper.Get種目カテゴリ(DA_Master, 区分番号, ラウンド番号, 種目番号);
            string 種目名 = DSDspDataHelper.Get種目名(DA_Master, 区分番号, ラウンド番号, 種目番号);

            // 画像を表示状態に設定して Opacity=0 からフェードイン
            PartsTIT003.IM_種目1.Visibility = Visibility.Visible;
            PartsTIT003.IM_種目2.Visibility = Visibility.Visible;
            PartsTIT003.IM_種目1.Opacity = 0;
            PartsTIT003.IM_種目2.Opacity = 0;

            var imageStoryboard = new Storyboard();
            _partsMain.フェードイン(true, PartsTIT003.IM_種目1, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsTIT003.IM_種目2, imageStoryboard, 0);
            imageStoryboard.Begin();

            CreateAndStartSlideAnimation(PartsTIT003.IM_種目1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsTIT003.IM_種目2, SLIDE_FROM_LEFT);

            // ラベルを表示状態にしてテキストをセット
            PartsTIT003.LB_種目順.Visibility  = Visibility.Visible;
            PartsTIT003.LB_種目紹介.Visibility = Visibility.Visible;

            PartsTIT003.LB_種目順.Content  = 種目番号.ToString() + "種目目";
            PartsTIT003.LB_種目紹介.Content = 種目カテゴリ + "　" +    種目名;

            _partsMain.フォントサイズ自動調整(
                label: PartsTIT003.LB_種目紹介,
                text: 種目名,
                maxWidth: MAX_TEXT_WIDTH,
                maxFontSize: MAX_FONT_SIZE,
                minFontSize: MIN_FONT_SIZE,
                fontFamilyName: FONT_FAMILY_NAME);

            // ラベルをフェードイン（画像より遅延）
            var titleStoryboard = new Storyboard();
            PartsTIT003.LB_種目順.Opacity  = 0;
            PartsTIT003.LB_種目紹介.Opacity = 0;
            _partsMain.フェードイン(true, PartsTIT003.LB_種目順,  titleStoryboard, FADE_DELAY_MILLISECONDS);
            _partsMain.フェードイン(true, PartsTIT003.LB_種目紹介, titleStoryboard, FADE_DELAY_MILLISECONDS);
            titleStoryboard.Begin();
        }

        /// <summary>
        /// Step3: TIT003 上の全要素をフェードアウトする。
        /// </summary>
        public void Step3()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            var sb = new Storyboard();
            _partsMain.フェードアウト(true, PartsTIT003.LB_種目順,  sb, 0);
            _partsMain.フェードアウト(true, PartsTIT003.LB_種目紹介, sb, 0);
            _partsMain.フェードアウト(true, PartsTIT003.IM_種目1,    sb, 0);
            _partsMain.フェードアウト(true, PartsTIT003.IM_種目2,    sb, 0);
            sb.Completed += (s, e) => RaiseScreenCompleted();
            sb.Begin();

            // 右上01に種目情報を表示する
            // 種目情報を取得
            var dance = DSDspDataHelper.Get種目(DA_Master, 区分番号, ラウンド番号, 種目番号);
            if (dance != null)
            {
                int 種目順 = 種目番号;
                string 種目カテゴリ = DSDspDataHelper.Get種目カテゴリ(DA_Master, 区分番号, ラウンド番号, 種目番号);
                string 種目名 = DSDspDataHelper.Get種目名(DA_Master, 区分番号, ラウンド番号, 種目番号);

                PartsCOM002.LB_右上.Content = 種目順.ToString() + "種目目" + "　" + 種目カテゴリ + "　" + 種目名;
            }
            else
            {
                PartsCOM002.LB_右上.Content = "種目情報なし";
            }


        }

        #endregion
    }
}

// Made with Bob
