using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_DUE_002_DUE選手紹介_小.xaml の相互作用ロジック
    /// デュエル競技の1ヒートに対戦する2選手を下部バナーに並べて表示する（小画面）。
    /// ステップ構成: Step1(ヘッダ設定) → Step2(2選手フェードイン) → Step3(フェードアウト)
    /// </summary>
    public partial class DSP_DUE_002_DUE選手紹介_小 : DSDspScreenBase
    {
        #region 定数定義
        private const double SLIDE_FROM_LEFT  = -1000;
        private const double SLIDE_FROM_RIGHT =  1000;
        private const int    FADE_DELAY_MS    =  1000;
        private const string FONT_FAMILY_NAME = "Segoe UI Semibold";
        #endregion

        #region オーバーライド
        /// <summary>
        /// ステップ数: Step1(0), Step2(1), Step3(2) の 3 ステップ
        /// </summary>
        protected override int TotalSteps => 2;
        public override bool WaitsForLastStepFadeOut => true;
        public override bool HoldsAfterFadeOut => true;
        #endregion

        #region コンストラクタ
        public DSP_DUE_002_DUE選手紹介_小()
        {
            InitializeComponent();
        }
        #endregion

        #region オーバーライドメソッド
        protected override void ExecuteCurrentStep()
        {
            switch (_currentStep)
            {
                case 0: Step1(); Step2(); break;
                case 1: Step3(); break;
            }
        }

        protected override void EnsurePartsMainInitialized()
        {
            bool wasNull = (_partsMain == null);
            base.EnsurePartsMainInitialized();
            if (wasNull && _partsMain != null)
            {
                // 初回のみ非表示を実行
                非表示();
            }
        }
        #endregion

        #region プライベートメソッド

        /// <summary>TIT007 上の全ラベル・画像を非表示にする</summary>
        private void 非表示()
        {
            // 画像
            PartsTIT007.IM_種目1_1.Visibility = Visibility.Collapsed;
            PartsTIT007.IM_種目1_2.Visibility = Visibility.Collapsed;
            PartsTIT007.IM_種目2_1.Visibility = Visibility.Collapsed;
            PartsTIT007.IM_種目2_2.Visibility = Visibility.Collapsed;
            PartsTIT007.IM_VS.Visibility = Visibility.Collapsed;

            // テキスト共通
            PartsTIT007.LB_演技順.Visibility      = Visibility.Collapsed;

            // 1選手目
            PartsTIT007.LB_選手紹介_1.Visibility  = Visibility.Collapsed;
            PartsTIT007.LB_所属_1.Visibility      = Visibility.Collapsed;

            // 2選手目
            PartsTIT007.LB_選手紹介_2.Visibility  = Visibility.Collapsed;
            PartsTIT007.LB_所属_2.Visibility      = Visibility.Collapsed;
        }

        /// <summary>
        /// Step1: 競技会名・区分名・ラウンド名・種目情報をヘッダに設定し、
        ///        TIT007 パーツを非表示状態にリセットする。
        /// </summary>
        public void Step1()
        {
            EnsurePartsMainInitialized();

            非表示();

            // COM003 の右上をクリア
            PartsCOM003.LB_右上.Content = string.Empty;

            // JDSFロゴを設定
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(
                new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));

            // 競技会名・区分ラウンド名・種目情報を共通ヘルパーで設定
            SetCommonHeader(PartsCOM001.TB_左上1, PartsCOM001.TB_左上2, PartsCOM002.LB_右上);
        }

        /// <summary>
        /// Step2: デュエル対戦の2選手をアニメーション付きで表示する（下部バナー）。
        ///        DS_Status の該当ヒートから選手を最大2名取得して左右に並べる。
        ///        TIT007 は小サイズのため選手名L+Pを連結した1行で表示する。
        /// </summary>
        public void Step2()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            // ---- 2選手の背番号を取得 ----
            var 背番号リスト = DSDspDataHelper.Get背番号リストFromHeat(
                DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);

            string 背番号1 = 背番号リスト.Count > 0 ? 背番号リスト[0] : "???";
            string 背番号2 = 背番号リスト.Count > 1 ? 背番号リスト[1] : "???";

            // ---- DA_Masterから選手情報を取得 ----
            var 選手1 = DSDspDataHelper.Get選手情報(DA_Master, 背番号1, 区分番号);
            var 選手2 = DSDspDataHelper.Get選手情報(DA_Master, 背番号2, 区分番号);

            // TIT007 は1行ラベルのため「背番号 選手名L・選手名P」を1つの文字列にまとめる
            string 選手名L1 = DSDspDataHelper.Get選手名L(選手1);
            string 選手名P1 = DSDspDataHelper.Get選手名P(選手1);
            string 所属1   = DSDspDataHelper.Get所属(選手1);


            string 選手紹介1 = string.IsNullOrEmpty(選手名P1)
                ? $"{背番号1} {選手名L1}"
                : $"{背番号1} {選手名L1}・{選手名P1}";

            string 選手名L2 = DSDspDataHelper.Get選手名L(選手2);
            string 選手名P2 = DSDspDataHelper.Get選手名P(選手2);
            string 所属2   = DSDspDataHelper.Get所属(選手2);
            string 選手紹介2 = string.IsNullOrEmpty(選手名P2)
                ? $"{背番号2} {選手名L2}"
                : $"{背番号2} {選手名L2}・{選手名P2}";

            // ---- 全ラベル・画像を Visible に戻し、Opacity=0 からフェードイン ----
            PartsTIT007.IM_種目1_1.Visibility = Visibility.Visible;
            PartsTIT007.IM_種目1_2.Visibility = Visibility.Visible;
            PartsTIT007.IM_種目2_1.Visibility = Visibility.Visible;
            PartsTIT007.IM_種目2_2.Visibility = Visibility.Visible;
            PartsTIT007.IM_種目1_1.Opacity = 0;
            PartsTIT007.IM_種目1_2.Opacity = 0;
            PartsTIT007.IM_種目2_1.Opacity = 0;
            PartsTIT007.IM_種目2_2.Opacity = 0;
            PartsTIT007.IM_VS.Visibility = Visibility.Visible;

            PartsTIT007.LB_演技順.Visibility     = Visibility.Visible;
            PartsTIT007.LB_選手紹介_1.Visibility = Visibility.Visible;
            PartsTIT007.LB_所属_1.Visibility     = Visibility.Visible;
            PartsTIT007.LB_選手紹介_2.Visibility = Visibility.Visible;
            PartsTIT007.LB_所属_2.Visibility     = Visibility.Visible;

            // ---- テキスト内容を設定 ----
            PartsTIT007.LB_演技順.Content     = ヒート番号.ToString() + "組目";
            PartsTIT007.LB_選手紹介_1.Content = 選手紹介1;
            PartsTIT007.LB_所属_1.Content     = 所属1;
            PartsTIT007.LB_選手紹介_2.Content = 選手紹介2;
            PartsTIT007.LB_所属_2.Content     = 所属2;

            // ---- フォントサイズ自動調整（選手紹介: 14～8）----
            foreach (var (lb, txt) in new[]
            {
                (PartsTIT007.LB_選手紹介_1, 選手紹介1),
                (PartsTIT007.LB_選手紹介_2, 選手紹介2),
            })
            {
                _partsMain.フォントサイズ自動調整(
                    label: lb, text: txt,
                    maxWidth: 280, maxFontSize: 14, minFontSize: 8,
                    fontFamilyName: FONT_FAMILY_NAME);
            }

            // ---- フォントサイズ自動調整（所属: 12～6）----
            foreach (var (lb, txt) in new[]
            {
                (PartsTIT007.LB_所属_1, 所属1),
                (PartsTIT007.LB_所属_2, 所属2),
            })
            {
                _partsMain.フォントサイズ自動調整(
                    label: lb, text: txt,
                    maxWidth: 280, maxFontSize: 12, minFontSize: 6,
                    fontFamilyName: FONT_FAMILY_NAME);
            }

            // ---- 画像フェードイン + スライドアニメーション ----
            var imgSb = new Storyboard();
            _partsMain.フェードイン(true, PartsTIT007.IM_種目1_1, imgSb, 0);
            _partsMain.フェードイン(true, PartsTIT007.IM_種目1_2, imgSb, 0);
            _partsMain.フェードイン(true, PartsTIT007.IM_種目2_1, imgSb, 0);
            _partsMain.フェードイン(true, PartsTIT007.IM_種目2_2, imgSb, 0);

            CreateAndStartSlideAnimation(PartsTIT007.IM_種目1_1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsTIT007.IM_種目1_2, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsTIT007.IM_種目2_1, SLIDE_FROM_LEFT);
            CreateAndStartSlideAnimation(PartsTIT007.IM_種目2_2, SLIDE_FROM_LEFT);
            imgSb.Begin();

            // ---- テキストフェードイン（画像より遅延） ----
            var txtSb = new Storyboard();
            foreach (var lb in new System.Windows.Controls.Label[]
            {
                PartsTIT007.LB_演技順,
                PartsTIT007.LB_選手紹介_1, PartsTIT007.LB_所属_1,
                PartsTIT007.LB_選手紹介_2, PartsTIT007.LB_所属_2,                
            })
            {
                lb.Opacity = 0;
                _partsMain.フェードイン(true, lb, txtSb, FADE_DELAY_MS);
            }
            txtSb.Begin();
        }

        /// <summary>
        /// Step3: TIT007 上の全要素をフェードアウトする。
        /// </summary>
        public void Step3()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            var sb = new Storyboard();

            _partsMain.フェードアウト(true, PartsTIT007.IM_種目1_1, sb, 0);
            _partsMain.フェードアウト(true, PartsTIT007.IM_種目1_2, sb, 0);
            _partsMain.フェードアウト(true, PartsTIT007.IM_種目2_1, sb, 0);
            _partsMain.フェードアウト(true, PartsTIT007.IM_種目2_2, sb, 0);
            _partsMain.フェードアウト(true, PartsTIT007.IM_VS, sb, 0);

            foreach (var lb in new System.Windows.Controls.Label[]
            {
                PartsTIT007.LB_演技順,
                PartsTIT007.LB_選手紹介_1, PartsTIT007.LB_所属_1,
                PartsTIT007.LB_選手紹介_2, PartsTIT007.LB_所属_2,
            })
            {
                _partsMain.フェードアウト(true, lb, sb, 0);
            }

            sb.Completed += (s, e) => RaiseLastStepFadeOutCompleted();
            sb.Begin();

            // 右上02に選手名を表示する。
            if (DA_Master == null) return;

            // ヒートの2選手を取得
            var 背番号リスト = DSDspDataHelper.Get背番号リストFromHeat(
                DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);

            var _背番号1 = 背番号リスト.Count > 0 ? 背番号リスト[0] : "???";
            var _背番号2 = 背番号リスト.Count > 1 ? 背番号リスト[1] : "???";

            var 選手1 = DSDspDataHelper.Get選手情報(DA_Master, _背番号1, 区分番号);
            string 選手名L1 = Get苗字(DSDspDataHelper.Get選手名L(選手1));
            string 選手名P1 = Get苗字(DSDspDataHelper.Get選手名P(選手1));


            var _選手紹介1 = string.IsNullOrEmpty(選手名P1)
                ? $"{_背番号1} {選手名L1}"
                : $"{_背番号1} {選手名L1}・{選手名P1} 組";

            var 選手2 = DSDspDataHelper.Get選手情報(DA_Master, _背番号2, 区分番号);
            string 選手名L2 = Get苗字(DSDspDataHelper.Get選手名L(選手2));
            string 選手名P2 = Get苗字(DSDspDataHelper.Get選手名P(選手2));
            var _選手紹介2 = string.IsNullOrEmpty(選手名P2)
                ? $"{_背番号2} {選手名L2}"
                : $"{_背番号2} {選手名L2}・{選手名P2} 組";

            // COM003 右上にヒート情報を表示
            PartsCOM003.LB_右上.Content =
                $"{ヒート番号}H  {_選手紹介1} vs {_選手紹介2}";


        }



        /// <summary>
        /// 氏名文字列から苗字部分を抽出する。
        /// 半角スペース・全角スペースの最初の出現位置より前を苗字とする。
        /// スペースが含まれない場合は文字列全体を返す。
        /// </summary>
        private static string Get苗字(string 氏名)
        {
            if (string.IsNullOrEmpty(氏名)) return 氏名;
            int idx = -1;
            for (int i = 0; i < 氏名.Length; i++)
            {
                if (氏名[i] == ' ' || 氏名[i] == '\u3000')   // 半角スペース or 全角スペース
                {
                    idx = i;
                    break;
                }
            }
            return idx >= 0 ? 氏名.Substring(0, idx) : 氏名;
        }
        #endregion
    }
}
