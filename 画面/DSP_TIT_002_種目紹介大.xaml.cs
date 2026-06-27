using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_TIT_002_種目紹介.xaml の相互作用ロジック
    /// </summary>
    public partial class DSP_TIT_002_種目紹介大 : DSDspScreenBase
    {
        #region 定数定義
        private const int ANIMATION_DURATION_SECONDS = 1;
        private const double SLIDE_FROM_LEFT = -1000;
        private const double SLIDE_FROM_RIGHT = 1000;
        private const int FADE_DELAY_MILLISECONDS = 1000;

        // フォントサイズ調整用の定数
        private const double MAX_FONT_SIZE = 24;
        private const double MIN_FONT_SIZE = 6;
        private const double MAX_TEXT_WIDTH = 400;
        private const string FONT_FAMILY_NAME = "HGPSoeiKakugothicUB";
        #endregion

        #region プロパティ
        /// <summary>
        /// 総ステップ数（Step0, Step1, Step2, Step3の4ステップ）
        /// </summary>
        protected override int TotalSteps => 4;
        #endregion

        #region コンストラクタ
        public DSP_TIT_002_種目紹介大()
        {
            InitializeComponent();
            this.Loaded += DSP_TIT_002_種目紹介大_Loaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSP_TIT_002_種目紹介大_Loaded(object sender, RoutedEventArgs e)
        {
            // 画面読み込み時は自動実行しない（外部から制御）
            // 初期化のみ実行
            EnsurePartsMainInitialized();
        }
        #endregion

        #region オーバーライドメソッド
        /// <summary>
        /// 現在のステップを実行
        /// </summary>
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
        #endregion

        #region プライベートメソッド

        private void 非表示()
        {
            // 区分ラウンド名を非表示
            PartsCOM001.TB_左上2.Text = string.Empty;

            // TIT001 のタイトルと画像を非表示
            PartsTIT002.LB_種目順.Visibility = Visibility.Collapsed;
            PartsTIT002.LB_種目カテゴリ.Visibility = Visibility.Collapsed;
            PartsTIT002.LB_種目紹介.Visibility = Visibility.Collapsed;
            
            PartsTIT002.IM_種目1.Visibility = Visibility.Collapsed;
            PartsTIT002.IM_種目2.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Step1: 競技会名、区分名、ラウンド名を表示
        /// DA_Masterから情報を取得
        /// </summary>
        public void Step1()
        {
            // COM000_PartsMainを初期化
            EnsurePartsMainInitialized();

            //一旦非表示にする
            非表示();

            // COM002 の　LB_右上をクリアする
            PartsCOM002.LB_右上.Content = string.Empty;

            // COM001 のロゴを表示する
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));

            // 競技会名・区分ラウンド名を設定（COM002には種目情報は表示しないため第3引数は使い捨て）
            PartsCOM001.TB_左上1.Text = DSDspDataHelper.Get競技会名(DA_Master);
            string 区分名H = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
            string ラウンド名H = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);
            PartsCOM001.TB_左上2.Text = 区分名H + " " + ラウンド名H;
        }

        /// <summary>
        /// Step2: 種目情報をアニメーション表示
        /// DA_Masterから種目情報を取得
        /// </summary>
        public void Step2()
        {
            // PartsMainの初期化確認
            EnsurePartsMainInitialized();

            if (_partsMain == null) return;

            // ヘルパーから種目情報を取得
            int 種目順 = 種目番号;
            string 種目カテゴリ = DSDspDataHelper.Get種目カテゴリ(DA_Master, 区分番号, ラウンド番号, 種目番号);
            string 種目名 = DSDspDataHelper.Get種目名(DA_Master, 区分番号, ラウンド番号, 種目番号);

            // 画像とタイトルを表示状態に設定
            PartsTIT002.IM_種目1.Visibility = Visibility.Visible;
            PartsTIT002.IM_種目2.Visibility = Visibility.Visible;
            PartsTIT002.LB_種目順.Visibility = Visibility.Visible;
            PartsTIT002.LB_種目カテゴリ.Visibility = Visibility.Visible;
            PartsTIT002.LB_種目紹介.Visibility = Visibility.Visible;
          
            // 画像の初期状態を設定（フェードイン用に透明にする）
            PartsTIT002.IM_種目1.Opacity = 0;
            PartsTIT002.IM_種目2.Opacity = 0;

            // 画像のフェードインアニメーション
            var imageStoryboard = new Storyboard();
            _partsMain.フェードイン(true, PartsTIT002.IM_種目1, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsTIT002.IM_種目2, imageStoryboard, 0);
            imageStoryboard.Begin();

            // 画像のスライドアニメーション（RenderTransform.X を使用し IM_種目* 要素のみ移動）
            CreateAndStartSlideAnimation(PartsTIT002.IM_種目1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsTIT002.IM_種目2, SLIDE_FROM_LEFT);
 
            // タイトルテキストの設定とフォントサイズの自動調整
            PartsTIT002.LB_種目順.Content = 種目順.ToString() + "種目目";
            PartsTIT002.LB_種目カテゴリ.Content = 種目カテゴリ;
            PartsTIT002.LB_種目紹介.Content = 種目名;

            // COM000_PartsMainの共通機能を使用してフォントサイズを自動調整
            // 全てのパラメータを明示的に指定
            _partsMain.フォントサイズ自動調整(
                label: PartsTIT002.LB_種目紹介,
                text: 種目名,
                maxWidth: MAX_TEXT_WIDTH,
                maxFontSize: MAX_FONT_SIZE,
                minFontSize: MIN_FONT_SIZE,
                fontFamilyName: FONT_FAMILY_NAME);

            // タイトルのフェードインアニメーション
            var titleStoryboard = new Storyboard();
            PartsTIT002.LB_種目順.Opacity = 0;
            PartsTIT002.LB_種目カテゴリ.Opacity = 0;
            PartsTIT002.LB_種目紹介.Opacity = 0;
            _partsMain.フェードイン(true, PartsTIT002.LB_種目順, titleStoryboard, FADE_DELAY_MILLISECONDS);
            _partsMain.フェードイン(true, PartsTIT002.LB_種目カテゴリ, titleStoryboard, FADE_DELAY_MILLISECONDS);
            _partsMain.フェードイン(true, PartsTIT002.LB_種目紹介, titleStoryboard, FADE_DELAY_MILLISECONDS);
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
            _partsMain.フェードアウト(true, PartsTIT002.LB_種目順, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT002.LB_種目カテゴリ, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT002.LB_種目紹介, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT002.IM_種目1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsTIT002.IM_種目2, fadeOutStoryboard, 0);

            fadeOutStoryboard.Begin();
        }
        #endregion
    }
}

