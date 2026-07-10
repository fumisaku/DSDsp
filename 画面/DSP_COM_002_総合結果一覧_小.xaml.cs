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
    /// DSP_COM_002_総合結果一覧_小.xaml の相互作用ロジック
    /// LST004（右_小リスト8）を使用。LST001 と異なり減点列・所属列がない。
    /// </summary>
    public partial class DSP_COM_002_総合結果一覧_小 : DSDspScreenBase
    {
        #region 定数定義
        private const int ANIMATION_DURATION_SECONDS = 1;
        private const double SLIDE_FROM_LEFT = -1000;
        private const double SLIDE_FROM_RIGHT = 1000;

        private const string FONT_FAMILY_NAME = "Segoe UI Semibold";
        #endregion

        #region フィールド
        private string _採点方式ID = string.Empty;
        private string _背番号 = string.Empty;
        private string _選手名L = string.Empty;
        private string _選手名P = string.Empty;

        // 表示対象行の Image / Label 配列（Step3で初期化）
        private Image[]? _明細IM;
        private Label[]? _順位LB;
        private Label[]? _背番号LB;
        private Label[]? _選手名LB;
        private Label[]? _得点LB;
        private int _前回表示件数 = 0;
        #endregion

        #region プロパティ
        /// <summary>
        /// 総ステップ数：Step1(1) + Step2(1) + [Step3+Step4] × ページ数 + Step5(1)
        /// </summary>
        protected override int TotalSteps => 2 + ページ数 * 2 + 1;

        /// <summary>表示ページ数（8件/ページ）</summary>
        private int ページ数 => Math.Max(1, (int)Math.Ceiling(ヒート番号 / 8.0));
        #endregion

        #region コンストラクタ
        public DSP_COM_002_総合結果一覧_小()
        {
            InitializeComponent();
            this.Loaded += DSP_COM_002_総合結果一覧_小_Loaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSP_COM_002_総合結果一覧_小_Loaded(object sender, RoutedEventArgs e)
        {
            EnsurePartsMainInitialized();
        }
        #endregion

        #region オーバーライドメソッド
        protected override void ExecuteCurrentStep()
        {
            if (_currentStep == 0)
            {
                Step1();
                return;
            }
            if (_currentStep == 1)
            {
                Step2();
                return;
            }

            int ブロック内 = _currentStep - 2;
            int ページ = ブロック内 / 2;
            int ブロック位置 = ブロック内 % 2;

            if (ページ < ページ数)
            {
                if (ブロック位置 == 0)
                    Step3(DV_Result, ページ * 8);
                else
                    Step4();
                return;
            }

            Step5();
        }
        #endregion

        #region プライベートメソッド

        private void 非表示()
        {
            PartsLST004.IM_タイトル1.Visibility = Visibility.Collapsed;
            PartsLST004.IM_タイトル2.Visibility = Visibility.Collapsed;
            PartsLST004.IM_タイトル3.Visibility = Visibility.Collapsed;

            PartsLST004.LB_タイトル1.Visibility = Visibility.Collapsed;
            PartsLST004.LB_タイトル2.Visibility = Visibility.Collapsed;
            PartsLST004.LB_タイトル3.Visibility = Visibility.Collapsed;

            for (int i = 0; i < 8; i++)
            {
                _明細IM![i].Visibility = Visibility.Collapsed;
                _順位LB![i].Visibility = Visibility.Collapsed;
                _背番号LB![i].Visibility = Visibility.Collapsed;
                _選手名LB![i].Visibility = Visibility.Collapsed;
                _得点LB![i].Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Step1: 共通ヘッダ設定と配列初期化
        /// </summary>
        public void Step1()
        {
            EnsurePartsMainInitialized();

            _明細IM = new[]
            {
                PartsLST004.IM_明細1, PartsLST004.IM_明細2, PartsLST004.IM_明細3, PartsLST004.IM_明細4,
                PartsLST004.IM_明細5, PartsLST004.IM_明細6, PartsLST004.IM_明細7, PartsLST004.IM_明細8
            };
            _順位LB = new[]
            {
                PartsLST004.LB_結果1_順位, PartsLST004.LB_結果2_順位, PartsLST004.LB_結果3_順位, PartsLST004.LB_結果4_順位,
                PartsLST004.LB_結果5_順位, PartsLST004.LB_結果6_順位, PartsLST004.LB_結果7_順位, PartsLST004.LB_結果8_順位
            };
            _背番号LB = new[]
            {
                PartsLST004.LB_結果1_背番号, PartsLST004.LB_結果2_背番号, PartsLST004.LB_結果3_背番号, PartsLST004.LB_結果4_背番号,
                PartsLST004.LB_結果5_背番号, PartsLST004.LB_結果6_背番号, PartsLST004.LB_結果7_背番号, PartsLST004.LB_結果8_背番号
            };
            _選手名LB = new[]
            {
                PartsLST004.LB_結果1_選手名, PartsLST004.LB_結果2_選手名, PartsLST004.LB_結果3_選手名, PartsLST004.LB_結果4_選手名,
                PartsLST004.LB_結果5_選手名, PartsLST004.LB_結果6_選手名, PartsLST004.LB_結果7_選手名, PartsLST004.LB_結果8_選手名
            };
            _得点LB = new[]
            {
                PartsLST004.LB_結果1_得点, PartsLST004.LB_結果2_得点, PartsLST004.LB_結果3_得点, PartsLST004.LB_結果4_得点,
                PartsLST004.LB_結果5_得点, PartsLST004.LB_結果6_得点, PartsLST004.LB_結果7_得点, PartsLST004.LB_結果8_得点
            };

            非表示();

            PartsCOM003.LB_右上.Content = string.Empty;

            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));

            _採点方式ID = SetCommonHeader(PartsCOM001.TB_左上1, PartsCOM001.TB_左上2, PartsCOM002.LB_右上);
            if (DA_Master == null) return;

            _背番号 = DSDspDataHelper.Get背番号FromHeat(DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);
            var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, _背番号);
            _選手名L = DSDspDataHelper.Get選手名L(選手情報);
            _選手名P = DSDspDataHelper.Get選手名P(選手情報);
        }

        /// <summary>
        /// Step2: タイトル画像スライド＋ラベルフェードイン
        /// </summary>
        public void Step2()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            PartsLST004.IM_タイトル1.Opacity = 0;
            PartsLST004.IM_タイトル2.Opacity = 0;
            PartsLST004.IM_タイトル3.Opacity = 0;

            var imageStoryboard = new Storyboard();
            _partsMain.フェードイン(true, PartsLST004.IM_タイトル1, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsLST004.IM_タイトル2, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsLST004.IM_タイトル3, imageStoryboard, 0);

            imageStoryboard.Completed += (s, e) =>
            {
                string 区分名 = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
                string ラウンド名 = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);
                PartsLST004.LB_タイトル1.Content = 区分名 + " " + ラウンド名;
                PartsLST004.LB_タイトル2.Content = DSDspDataHelper.Get種目名(DA_Master, 区分番号, ラウンド番号, 種目番号);
                PartsLST004.LB_タイトル3.Content = "総合結果";

                PartsLST004.LB_タイトル1.Visibility = Visibility.Visible;
                PartsLST004.LB_タイトル2.Visibility = Visibility.Visible;
                PartsLST004.LB_タイトル3.Visibility = Visibility.Visible;

                _partsMain?.フォントサイズ自動調整(
                    label: PartsLST004.LB_タイトル1,
                    text: PartsLST004.LB_タイトル1.Content.ToString(),
                    maxWidth: 170,
                    maxFontSize: 10,
                    minFontSize: 6,
                    fontFamilyName: FONT_FAMILY_NAME);

                _partsMain?.フォントサイズ自動調整(
                    label: PartsLST004.LB_タイトル2,
                    text: PartsLST004.LB_タイトル2.Content.ToString(),
                    maxWidth: 170,
                    maxFontSize: 12,
                    minFontSize: 6,
                    fontFamilyName: FONT_FAMILY_NAME);

                var playerStoryboard = new Storyboard();
                PartsLST004.LB_タイトル1.Opacity = 0;
                PartsLST004.LB_タイトル2.Opacity = 0;
                PartsLST004.LB_タイトル3.Opacity = 0;

                _partsMain?.フェードイン(true, PartsLST004.LB_タイトル1, playerStoryboard, 100);
                _partsMain?.フェードイン(true, PartsLST004.LB_タイトル2, playerStoryboard, 100);
                _partsMain?.フェードイン(true, PartsLST004.LB_タイトル3, playerStoryboard, 100);
                playerStoryboard.Begin();
            };

            imageStoryboard.Begin();

            CreateAndStartSlideAnimation(PartsLST004.IM_タイトル1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsLST004.IM_タイトル2, SLIDE_FROM_LEFT);
            CreateAndStartSlideAnimation(PartsLST004.IM_タイトル3, SLIDE_FROM_RIGHT);
        }

        /// <summary>
        /// Step3: 途中結果一覧の表示（開始インデックスから最大8件）
        /// </summary>
        public void Step3(JsonNode? dvResult, int 開始インデックス = 0)
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null || dvResult == null) return;

            var 種目結果リスト = dvResult["種目結果"]?.AsArray();
            if (種目結果リスト == null) return;

            var 種目結果 = 種目結果リスト.FirstOrDefault(d => d?["種目順"]?.GetValue<int>() == 種目番号);
            if (種目結果 == null) return;

            var 選手結果リスト = 種目結果["選手結果"]?.AsArray();
            if (選手結果リスト == null) return;

            var 全順位リスト = 選手結果リスト
                .Where(p => p != null)
                .OrderBy(p => p!["種目順位番号"]?.GetValue<int>() ?? int.MaxValue)
                .ToList();
            var 表示対象 = 全順位リスト
                .Skip(開始インデックス)
                .Take(8)
                .ToList();
            int 表示件数 = 表示対象.Count;
            if (表示件数 == 0) return;

            for (int i = 0; i < 表示件数; i++)
            {
                var p = 表示対象[i];
                string 背番号 = p?["背番号"]?.ToString() ?? "";
                string 順位表記 = p?["種目順位表記"]?.ToString() ?? "";
                double 得点 = p?["種目得点"]?.GetValue<double>() ?? 0;
                bool 失格 = p?["失格FLAG"]?.ToString() == "1";

                var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, 背番号);
                string 選手名L = DSDspDataHelper.Get選手名L(選手情報);
                string 選手名P = DSDspDataHelper.Get選手名P(選手情報);
                string 選手名表示 = string.IsNullOrEmpty(選手名P)
                    ? 選手名L
                    : 選手名L + "・" + 選手名P;

                var 前景色 = new SolidColorBrush(Colors.DarkBlue);

                _順位LB![i].Content = 失格 ? "失格" : 順位表記;
                _背番号LB![i].Content = 背番号;
                _選手名LB![i].Content = 選手名表示;
                _得点LB![i].Content = 失格 ? "失格" : 得点.ToString("F3");

                _順位LB[i].Foreground = 前景色;
                _背番号LB[i].Foreground = 前景色;
                _選手名LB[i].Foreground = 前景色;
                _得点LB[i].Foreground = 前景色;

                _順位LB[i].Opacity = 0;
                _背番号LB[i].Opacity = 0;
                _選手名LB[i].Opacity = 0;
                _得点LB[i].Opacity = 0;
            }

            for (int i = 0; i < 表示件数; i++)
            {
                _partsMain.フォントサイズ自動調整(
                    label: _選手名LB![i],
                    text: _選手名LB[i].Content?.ToString() ?? "",
                    maxWidth: 77,
                    maxFontSize: 10,
                    minFontSize: 6,
                    fontFamilyName: FONT_FAMILY_NAME);
            }

            int 間隔ms = 表示件数 > 1 ? Math.Max(1000 / 表示件数, 100) : 0;
            var 明細Storyboard = new Storyboard();
            for (int i = 0; i < 表示件数; i++)
            {
                _明細IM![i].Opacity = 0;
                _partsMain.フェードイン(true, _明細IM[i], 明細Storyboard, i * 間隔ms);
            }

            明細Storyboard.Completed += (s, e) =>
            {
                var 結果Storyboard = new Storyboard();
                for (int i = 0; i < 表示件数; i++)
                {
                    _partsMain?.フェードイン(true, _順位LB![i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _背番号LB![i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _選手名LB![i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _得点LB![i], 結果Storyboard, 0);
                }
                結果Storyboard.Begin();
            };

            _前回表示件数 = 表示件数;
            明細Storyboard.Begin();
        }

        /// <summary>
        /// Step4: 直前の Step3 で表示した行をフェードアウト
        /// </summary>
        public void Step4()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            var fadeOutStoryboard = new Storyboard();
            for (int i = 0; i < _前回表示件数; i++)
            {
                _partsMain.フェードアウト(true, _明細IM![i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _順位LB![i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _背番号LB![i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _選手名LB![i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _得点LB![i], fadeOutStoryboard, 0);
            }
            fadeOutStoryboard.Begin();
        }

        /// <summary>
        /// Step5: タイトルをフェードアウト
        /// </summary>
        public void Step5()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            var fadeOutStoryboard = new Storyboard();
            _partsMain.フェードアウト(true, PartsLST004.IM_タイトル1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST004.IM_タイトル2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST004.IM_タイトル3, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST004.LB_タイトル1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST004.LB_タイトル2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST004.LB_タイトル3, fadeOutStoryboard, 0);
            fadeOutStoryboard.Begin();
        }

        #endregion
    }
}

// Made with Bob
