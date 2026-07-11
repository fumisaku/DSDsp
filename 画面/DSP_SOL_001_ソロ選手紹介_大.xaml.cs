using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_SOL_001_ソロ選手紹介_大.xaml の相互作用ロジック
    /// </summary>
    public partial class DSP_SOL_001_ソロ選手紹介_大 : DSDspScreenBase
    {
        #region 定数定義
        private const int ANIMATION_DURATION_SECONDS = 1;
        private const double SLIDE_FROM_LEFT = -1000;
        private const double SLIDE_FROM_RIGHT = 1000;
        private const int FADE_DELAY_MILLISECONDS = 800;

        // フォントサイズ調整用の定数
        private const double MAX_FONT_SIZE = 24;
        private const double MIN_FONT_SIZE = 6;
        private const double MAX_TEXT_WIDTH = 400;
        private const string FONT_FAMILY_NAME = "Segoe UI Semibold";
        #endregion

        #region オーバーライド
        /// <summary>
        /// ステップ数（Step0, Step1, Step2, Step3の4ステップ）
        /// </summary>
        protected override int TotalSteps => 4;
        #endregion

        #region コンストラクタ
        public DSP_SOL_001_ソロ選手紹介_大()
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
            // TIT004 の文字と画像を非表示
            PartsTIT004.LB_演技順.Visibility = Visibility.Collapsed;
            PartsTIT004.LB_背番号.Visibility = Visibility.Collapsed;
            PartsTIT004.LB_選手名L.Visibility = Visibility.Collapsed;
            PartsTIT004.LB_選手名P.Visibility = Visibility.Collapsed;
            PartsTIT004.LB_所属.Visibility = Visibility.Collapsed;

            PartsTIT004.IM_種目1.Visibility = Visibility.Collapsed;
            PartsTIT004.IM_種目2.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Step1: 競技会名、区分名、ラウンド名、種目情報を表示
        /// DA_MasterとDS_Statusから情報を取得
        /// </summary>
        public void Step1()
        {
            // COM000_PartsMainを初期化
            EnsurePartsMainInitialized();

            // 一旦非表示にする
            非表示();  

            // COM003 の　LB_右上をクリアする
            PartsCOM003.LB_右上.Content = string.Empty;

            // COM001 のロゴを表示する
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));

            // ヘルパーを使用してデータを取得
            string 競技会名 = DSDspDataHelper.Get競技会名(DA_Master);
            PartsCOM001.TB_左上1.Text = 競技会名;

            string 区分名 = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
            string ラウンド名 = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);
            PartsCOM001.TB_左上2.Text = 区分名 + " " + ラウンド名;

            // 種目情報を取得
            var dance = DSDspDataHelper.Get種目(DA_Master, 区分番号, ラウンド番号, 種目番号);
            if (dance != null)
            {
                int 種目順 = 種目番号;
                string 種目カテゴリ = DSDspDataHelper.Get種目カテゴリ(DA_Master, 区分番号, ラウンド番号, 種目番号);
                string 種目名 = DSDspDataHelper.Get種目名(DA_Master, 区分番号, ラウンド番号, 種目番号);
                
                PartsCOM002.LB_右上.Content = 種目順.ToString() + "種目目" + " " + 種目カテゴリ + " " + 種目名;
            }
            else
            {
                PartsCOM002.LB_右上.Content = "種目情報なし";
            }
        }

        /// <summary>
        /// Step2: 選手情報をアニメーション表示
        /// DS_Statusから選手情報を取得
        /// </summary>
        public void Step2()
        {
            // PartsMainの初期化確認
            EnsurePartsMainInitialized();

            if (_partsMain == null) return;

            // DS_Statusから背番号を取得
            string 背番号 = DSDspDataHelper.Get背番号FromHeat(DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);
            
            // DA_Masterから選手情報を取得
            var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, 背番号);
            string 選手名L = DSDspDataHelper.Get選手名L(選手情報);
            string 選手名P = DSDspDataHelper.Get選手名P(選手情報);
            string 所属 = DSDspDataHelper.Get所属(選手情報);

            // 画像とタイトルを表示状態に設定
            PartsTIT004.LB_演技順.Visibility = Visibility.Visible;
            PartsTIT004.LB_背番号.Visibility = Visibility.Visible;
            PartsTIT004.LB_選手名L.Visibility = Visibility.Visible;
            PartsTIT004.LB_選手名P.Visibility = Visibility.Visible;
            PartsTIT004.LB_所属.Visibility = Visibility.Visible;

            // 画像の初期状態を設定（フェードイン用に透明にする）
            PartsTIT004.IM_種目1.Opacity = 0;
            PartsTIT004.IM_種目2.Opacity = 0;


            // 画像のスライドアニメーション
            CreateAndStartSlideAnimation(PartsTIT004.IM_種目1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsTIT004.IM_種目2, SLIDE_FROM_LEFT);

            // 画像のフェードインアニメーション
            var imageStoryboard = new Storyboard();
            _partsMain.フェードイン(true, PartsTIT004.IM_種目1, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsTIT004.IM_種目2, imageStoryboard, 0);
            imageStoryboard.Begin();


            // タイトルテキストの設定とフォントサイズの自動調整
            PartsTIT004.LB_演技順.Content = ヒート番号.ToString() + "組目";
            PartsTIT004.LB_背番号.Content = 背番号;
            PartsTIT004.LB_選手名L.Content = 選手名L;
            PartsTIT004.LB_選手名P.Content = 選手名P;
            PartsTIT004.LB_所属.Content = 所属;

            // COM000_PartsMainの共通機能を使用してフォントサイズを自動調整
            // 全てのパラメータを明示的に指定
            _partsMain.フォントサイズ自動調整(
                label: PartsTIT004.LB_選手名L,
                text: 選手名L,
                maxWidth: 400,
                maxFontSize: 22,
                minFontSize: 20,
                fontFamilyName: FONT_FAMILY_NAME);

            _partsMain.フォントサイズ自動調整(
                label: PartsTIT004.LB_選手名P,
                text: 選手名P,
                maxWidth: 400,
                maxFontSize: 22,
                minFontSize: 20,
                fontFamilyName: FONT_FAMILY_NAME);

            _partsMain.フォントサイズ自動調整(
                label: PartsTIT004.LB_所属,
                text: 所属,
                maxWidth: 400,
                maxFontSize: 20,
                minFontSize: 20,
                fontFamilyName: FONT_FAMILY_NAME);

            // タイトルのフェードインアニメーション
            var titleStoryboard = new Storyboard();
            PartsTIT004.LB_演技順.Opacity = 0;
            PartsTIT004.LB_背番号.Opacity = 0;
            PartsTIT004.LB_選手名L.Opacity = 0;
            PartsTIT004.LB_選手名P.Opacity = 0;
            PartsTIT004.LB_所属.Opacity = 0;
            _partsMain.フェードイン(true, PartsTIT004.LB_演技順, titleStoryboard, FADE_DELAY_MILLISECONDS);
            _partsMain.フェードイン(true, PartsTIT004.LB_背番号, titleStoryboard, FADE_DELAY_MILLISECONDS);
            _partsMain.フェードイン(true, PartsTIT004.LB_選手名L, titleStoryboard, FADE_DELAY_MILLISECONDS);
            _partsMain.フェードイン(true, PartsTIT004.LB_選手名P, titleStoryboard, FADE_DELAY_MILLISECONDS);
            _partsMain.フェードイン(true, PartsTIT004.LB_所属, titleStoryboard, FADE_DELAY_MILLISECONDS);
            titleStoryboard.Begin();
        }

        /// <summary>
        /// Step3: ラベルと画像をフェードアウト
        /// </summary>
        public void Step3()
        {
            // TIT002 の LB_Title1 と LB_Title2 をクリア
            // TIT002 の IM_1 と IM_2 をフェードアウト

            EnsurePartsMainInitialized();

            if (_partsMain == null) return;

            var fadeOutStoryboard = new Storyboard();
            _partsMain.フェードアウト(true, PartsTIT004.LB_演技順, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT004.LB_背番号, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT004.LB_選手名L, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT004.LB_選手名P, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT004.LB_所属, fadeOutStoryboard, 0);

            _partsMain.フェードアウト(true, PartsTIT004.IM_種目1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT004.IM_種目2, fadeOutStoryboard, 0);
            fadeOutStoryboard.Begin();
        }
        #endregion
    }
}

