using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_DUE_004_DUE選手結果_小.xaml の相互作用ロジック
    /// DUE_003 の小画面版。DUE002 パーツを使用する。
    /// DUE002 の選手名列は LB_選手紹介_1/2 の1行形式（背番号+選手名を連結）。
    /// ステップ構成:
    ///   Step1 (0) … ヘッダ設定・非表示リセット
    ///   Step2 (1) … 背景画像 + PCS列名 + 選手情報フェードイン
    ///   Step3 (2) … PCS得点 → Total → Rank の順に表示
    ///   Step4 (3) … 全要素フェードアウト
    /// </summary>
    public partial class DSP_DUE_004_DUE選手結果_小 : DSDspScreenBase
    {
        #region 定数定義
        private const double SLIDE_FROM_LEFT  = -1000;
        private const double SLIDE_FROM_RIGHT =  1000;
        private const string FONT_FAMILY_NAME = "Segoe UI Semibold";
        #endregion

        #region フィールド
        private string _採点方式ID  = string.Empty;
        private string _背番号1     = string.Empty;
        private string _背番号2     = string.Empty;
        private string _選手紹介1   = string.Empty;  // "背番号 選手名L・選手名P"
        private string _選手紹介2   = string.Empty;
        #endregion

        #region オーバーライド
        protected override int TotalSteps => 3;
        public override bool WaitsForLastStepFadeOut => true;
        #endregion

        #region コンストラクタ
        public DSP_DUE_004_DUE選手結果_小()
        {
            InitializeComponent();
            this.Loaded += (s, e) => EnsurePartsMainInitialized();
        }
        #endregion

        #region オーバーライドメソッド
        protected override void ExecuteCurrentStep()
        {
            switch (_currentStep)
            {
                case 0: Step1(); Step2(); break;
                case 1: Step3(DV_Result); break;
                case 2: Step4(); break;
            }
        }
        #endregion

        #region プライベートメソッド

        /// <summary>DUE002 上の全ラベル・画像を非表示にする</summary>
        private void 非表示()
        {
            // ---- 背景画像 ----
            PartsDUE002.IM_種目1_1.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_種目1_2.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_種目2_1.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_種目2_2.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_ソロ選手結果1_1.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_ソロ選手結果1_2.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_ソロ選手結果1_3.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_ソロ選手結果1_4.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_ソロ選手結果2_1.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_ソロ選手結果2_2.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_ソロ選手結果2_3.Visibility = Visibility.Collapsed;
            PartsDUE002.IM_ソロ選手結果2_4.Visibility = Visibility.Collapsed;

            // ---- 選手紹介（1行形式）----
            PartsDUE002.LB_選手紹介_1.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_選手紹介_2.Visibility = Visibility.Collapsed;

            PartsDUE002.LB_選手紹介_1.Content = string.Empty;
            PartsDUE002.LB_選手紹介_2.Content = string.Empty;

            // ---- PCS名・値・減点・Total・Rank（1選手目）----
            PartsDUE002.LB_PCS名1_1.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_PCS名1_2.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_PCS名1_3.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_PCS名1_4.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_Red名_1.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_PCS1_1.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_PCS1_2.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_PCS1_3.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_PCS1_4.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_Red_1.Visibility     = Visibility.Collapsed;
            PartsDUE002.LB_Total名_1.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_Total_1.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_Rank名_1.Visibility  = Visibility.Collapsed;
            PartsDUE002.LB_Rank_1.Visibility    = Visibility.Collapsed;

            // ---- PCS名・値・減点・Total・Rank（2選手目）----
            PartsDUE002.LB_PCS名2_1.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_PCS名2_2.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_PCS名2_3.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_PCS名2_4.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_Red名_2.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_PCS2_1.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_PCS2_2.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_PCS2_3.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_PCS2_4.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_Red_2.Visibility     = Visibility.Collapsed;
            PartsDUE002.LB_Total名_2.Visibility = Visibility.Collapsed;
            PartsDUE002.LB_Total_2.Visibility   = Visibility.Collapsed;
            PartsDUE002.LB_Rank名_2.Visibility  = Visibility.Collapsed;
            PartsDUE002.LB_Rank_2.Visibility    = Visibility.Collapsed;
        }

        /// <summary>
        /// Step1: ヘッダを設定し DUE002 を非表示状態にリセットする。
        ///        2選手の背番号・紹介テキストをフィールドに保持する。
        /// </summary>
        public void Step1()
        {
            EnsurePartsMainInitialized();
            非表示();

            PartsCOM003.LB_右上.Content = string.Empty;
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(
                new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));

            _採点方式ID = SetCommonHeader(
                PartsCOM001.TB_左上1, PartsCOM001.TB_左上2, PartsCOM002.LB_右上);

            if (DA_Master == null) return;

            // ヒートの2選手を取得
            var 背番号リスト = DSDspDataHelper.Get背番号リストFromHeat(
                DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);

            _背番号1 = 背番号リスト.Count > 0 ? 背番号リスト[0] : "???";
            _背番号2 = 背番号リスト.Count > 1 ? 背番号リスト[1] : "???";

            var 選手1 = DSDspDataHelper.Get選手情報(DA_Master, _背番号1, 区分番号);
            string 選手名L1 = Get苗字(DSDspDataHelper.Get選手名L(選手1));
            string 選手名P1 = Get苗字(DSDspDataHelper.Get選手名P(選手1));
            

            _選手紹介1 = string.IsNullOrEmpty(選手名P1)
                ? $"{_背番号1} {選手名L1}"
                : $"{_背番号1} {選手名L1}・{選手名P1} 組";

            var 選手2 = DSDspDataHelper.Get選手情報(DA_Master, _背番号2, 区分番号);
            string 選手名L2 = Get苗字(DSDspDataHelper.Get選手名L(選手2));
            string 選手名P2 = Get苗字(DSDspDataHelper.Get選手名P(選手2));
            _選手紹介2 = string.IsNullOrEmpty(選手名P2)
                ? $"{_背番号2} {選手名L2}"
                : $"{_背番号2} {選手名L2}・{選手名P2} 組";

            // COM003 右上にヒート情報を表示
           // PartsCOM003.LB_右上.Content =
           //     $"{ヒート番号}H  {_選手紹介1} vs {_選手紹介2}";

            PartsCOM003.LB_右上.Content = string.Empty;


        }

        /// <summary>
        /// Step2: 背景画像 + PCS列名 + 選手紹介をフェードイン表示する。
        /// </summary>
        public void Step2()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            // ---- 選手紹介ラベルを Visible に ----
            PartsDUE002.LB_選手紹介_1.Visibility = Visibility.Visible;
            PartsDUE002.LB_選手紹介_2.Visibility = Visibility.Visible;

            // ---- 背景画像の Opacity=0 → フェードイン ----
            foreach (var img in new System.Windows.Controls.Image[]
            {
                PartsDUE002.IM_種目1_1, PartsDUE002.IM_種目1_2,
                PartsDUE002.IM_種目2_1, PartsDUE002.IM_種目2_2,
                PartsDUE002.IM_ソロ選手結果1_1, PartsDUE002.IM_ソロ選手結果1_2,
                PartsDUE002.IM_ソロ選手結果1_3, PartsDUE002.IM_ソロ選手結果1_4,
                PartsDUE002.IM_ソロ選手結果2_1, PartsDUE002.IM_ソロ選手結果2_2,
                PartsDUE002.IM_ソロ選手結果2_3, PartsDUE002.IM_ソロ選手結果2_4,
            })
            {
                img.Visibility = Visibility.Visible;
                img.Opacity = 0;
            }

            var imgSb = new Storyboard();
            // タイトル画像を即フェードイン
            _partsMain.フェードイン(true, PartsDUE002.IM_種目1_1, imgSb, 0);
            _partsMain.フェードイン(true, PartsDUE002.IM_種目1_2, imgSb, 0);
            _partsMain.フェードイン(true, PartsDUE002.IM_種目2_1, imgSb, 0);
            _partsMain.フェードイン(true, PartsDUE002.IM_種目2_2, imgSb, 0);
            // 結果欄背景画像を 1000ms 後にフェードイン
            _partsMain.フェードイン(true, PartsDUE002.IM_ソロ選手結果1_1, imgSb, 1000);
            _partsMain.フェードイン(true, PartsDUE002.IM_ソロ選手結果1_2, imgSb, 1000);
            _partsMain.フェードイン(true, PartsDUE002.IM_ソロ選手結果1_3, imgSb, 1000);
            _partsMain.フェードイン(true, PartsDUE002.IM_ソロ選手結果1_4, imgSb, 1000);
            _partsMain.フェードイン(true, PartsDUE002.IM_ソロ選手結果2_1, imgSb, 1000);
            _partsMain.フェードイン(true, PartsDUE002.IM_ソロ選手結果2_2, imgSb, 1000);
            _partsMain.フェードイン(true, PartsDUE002.IM_ソロ選手結果2_3, imgSb, 1000);
            _partsMain.フェードイン(true, PartsDUE002.IM_ソロ選手結果2_4, imgSb, 1000);

            // 画像スライド
            CreateAndStartSlideAnimation(PartsDUE002.IM_種目1_1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsDUE002.IM_種目1_2, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsDUE002.IM_種目2_1, SLIDE_FROM_LEFT);
            CreateAndStartSlideAnimation(PartsDUE002.IM_種目2_2, SLIDE_FROM_LEFT);

            // 全画像フェードイン完了後に PCS列名 + 選手紹介を表示
            imgSb.Completed += (s, e) =>
            {
                // PCS列名を設定・表示
                SetPCSNames();
                foreach (var lb in new System.Windows.Controls.Label[]
                {
                    PartsDUE002.LB_PCS名1_1, PartsDUE002.LB_PCS名1_2,
                    PartsDUE002.LB_PCS名1_3, PartsDUE002.LB_PCS名1_4,
                    PartsDUE002.LB_Red名_1,  PartsDUE002.LB_Total名_1, PartsDUE002.LB_Rank名_1,
                    PartsDUE002.LB_PCS名2_1, PartsDUE002.LB_PCS名2_2,
                    PartsDUE002.LB_PCS名2_3, PartsDUE002.LB_PCS名2_4,
                    PartsDUE002.LB_Red名_2,  PartsDUE002.LB_Total名_2, PartsDUE002.LB_Rank名_2,
                })
                {
                    lb.Visibility = Visibility.Visible;
                }

                // 選手紹介テキストをセット
                PartsDUE002.LB_選手紹介_1.Content = _選手紹介1;
                PartsDUE002.LB_選手紹介_2.Content = _選手紹介2;

                // フォントサイズ自動調整
                _partsMain?.フォントサイズ自動調整(
                    label: PartsDUE002.LB_選手紹介_1, text: _選手紹介1,
                    maxWidth: 280, maxFontSize: 16, minFontSize: 8,
                    fontFamilyName: FONT_FAMILY_NAME);
                _partsMain?.フォントサイズ自動調整(
                    label: PartsDUE002.LB_選手紹介_2, text: _選手紹介2,
                    maxWidth: 280, maxFontSize: 16, minFontSize: 8,
                    fontFamilyName: FONT_FAMILY_NAME);

                // 選手紹介フェードイン
                var playerSb = new Storyboard();
                PartsDUE002.LB_選手紹介_1.Opacity = 0;
                PartsDUE002.LB_選手紹介_2.Opacity = 0;
                _partsMain?.フェードイン(true, PartsDUE002.LB_選手紹介_1, playerSb, 100);
                _partsMain?.フェードイン(true, PartsDUE002.LB_選手紹介_2, playerSb, 100);
                playerSb.Begin();
            };

            imgSb.Begin();
        }

        /// <summary>
        /// Step3: DV_Result から2選手の PCS得点・減点・Total・Rank を順に表示する。
        /// </summary>
        public void Step3(JsonNode? dvResult)
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null || dvResult == null) return;

            // ---- 種目結果を取得 ----
            var 種目結果リスト = dvResult["種目結果"]?.AsArray();
            if (種目結果リスト == null) return;

            var 種目結果 = 種目結果リスト.FirstOrDefault(
                d => d?["種目順"]?.GetValue<int>() == 種目番号);
            if (種目結果 == null) return;

            var 選手結果リスト = 種目結果["選手結果"]?.AsArray();
            if (選手結果リスト == null) return;

            // ---- 2選手分のデータを取得する内部ヘルパー ----
            (double[] pcs, double 減点, double total, string rank) GetPlayerData(string 背番号)
            {
                var 結果 = 選手結果リスト.FirstOrDefault(p => p?["背番号"]?.ToString() == 背番号);
                if (結果 == null) return (new double[4], 0, 0, "");

                bool 失格 = 結果["失格FLAG"]?.ToString() == "1";

                var pcsArray = 結果["PCS"]?.AsArray();
                double[] pcsVals = new double[4];
                if (pcsArray != null)
                    for (int i = 0; i < 4 && i < pcsArray.Count; i++)
                        pcsVals[i] = pcsArray[i]?["PCS得点"]?.GetValue<double>() ?? 0;

                double 減点合計 = 0;
                if (!失格)
                {
                    var 一般減点Array = 結果["一般減点"]?.AsArray();
                    if (一般減点Array != null)
                        foreach (var r in 一般減点Array)
                            減点合計 += r?["一般減点"]?.GetValue<double>() ?? 0;
                }

                double 種目得点    = 失格 ? 0 : (結果["種目得点"]?.GetValue<double>() ?? 0);
                string 種目順位表記 = 失格 ? "失格" : (結果["種目順位表記"]?.ToString() ?? "");

                return (pcsVals, 失格 ? double.NaN : 減点合計, 種目得点, 種目順位表記);
            }

            var (pcs1, 減点1, total1, rank1) = GetPlayerData(_背番号1);
            var (pcs2, 減点2, total2, rank2) = GetPlayerData(_背番号2);

            // ---- PCS得点・減点ラベルにセット ----
            PartsDUE002.LB_PCS1_1.Content = pcs1[0].ToString("F2");
            PartsDUE002.LB_PCS1_2.Content = pcs1[1].ToString("F2");
            PartsDUE002.LB_PCS1_3.Content = pcs1[2].ToString("F2");
            PartsDUE002.LB_PCS1_4.Content = pcs1[3].ToString("F2");
            PartsDUE002.LB_Red_1.Content  = double.IsNaN(減点1) ? "失格" : 減点1.ToString("F1");

            PartsDUE002.LB_PCS2_1.Content = pcs2[0].ToString("F2");
            PartsDUE002.LB_PCS2_2.Content = pcs2[1].ToString("F2");
            PartsDUE002.LB_PCS2_3.Content = pcs2[2].ToString("F2");
            PartsDUE002.LB_PCS2_4.Content = pcs2[3].ToString("F2");
            PartsDUE002.LB_Red_2.Content  = double.IsNaN(減点2) ? "失格" : 減点2.ToString("F1");

            // ---- PCS + 減点をフェードイン ----
            var pcsSb = new Storyboard();
            foreach (var lb in new System.Windows.Controls.Label[]
            {
                PartsDUE002.LB_PCS1_1, PartsDUE002.LB_PCS1_2,
                PartsDUE002.LB_PCS1_3, PartsDUE002.LB_PCS1_4, PartsDUE002.LB_Red_1,
                PartsDUE002.LB_PCS2_1, PartsDUE002.LB_PCS2_2,
                PartsDUE002.LB_PCS2_3, PartsDUE002.LB_PCS2_4, PartsDUE002.LB_Red_2,
            })
            {
                lb.Visibility = Visibility.Visible;
                lb.Opacity = 0;
                _partsMain.フェードイン(true, lb, pcsSb, 0);
            }

            // PCS完了後 → Total フェードイン
            pcsSb.Completed += (s, e) =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                timer.Tick += (sender, args) =>
                {
                    timer.Stop();

                    PartsDUE002.LB_Total_1.Content = total1.ToString("F3");
                    PartsDUE002.LB_Total_2.Content = total2.ToString("F3");

                    var totalSb = new Storyboard();
                    foreach (var lb in new System.Windows.Controls.Label[]
                        { PartsDUE002.LB_Total_1, PartsDUE002.LB_Total_2 })
                    {
                        lb.Visibility = Visibility.Visible;
                        lb.Opacity = 0;
                        _partsMain?.フェードイン(true, lb, totalSb, 0);
                    }

                    // Total完了後 → Rank フェードイン
                    totalSb.Completed += (s2, e2) =>
                    {
                        var rankTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                        rankTimer.Tick += (sender2, args2) =>
                        {
                            rankTimer.Stop();

                            PartsDUE002.LB_Rank_1.Content = rank1;
                            PartsDUE002.LB_Rank_2.Content = rank2;

                            var rankSb = new Storyboard();
                            foreach (var lb in new System.Windows.Controls.Label[]
                                { PartsDUE002.LB_Rank_1, PartsDUE002.LB_Rank_2 })
                            {
                                lb.Visibility = Visibility.Visible;
                                lb.Opacity = 0;
                                _partsMain?.フェードイン(true, lb, rankSb, 0);
                            }
                            rankSb.Begin();
                        };
                        rankTimer.Start();
                    };

                    totalSb.Begin();
                };
                timer.Start();
            };

            pcsSb.Begin();
        }

        /// <summary>
        /// Step4: DUE002 上の全要素をフェードアウトする。
        /// </summary>
        public void Step4()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            var sb = new Storyboard();

            foreach (var img in new System.Windows.Controls.Image[]
            {
                PartsDUE002.IM_種目1_1, PartsDUE002.IM_種目1_2,
                PartsDUE002.IM_種目2_1, PartsDUE002.IM_種目2_2,
                PartsDUE002.IM_ソロ選手結果1_1, PartsDUE002.IM_ソロ選手結果1_2,
                PartsDUE002.IM_ソロ選手結果1_3, PartsDUE002.IM_ソロ選手結果1_4,
                PartsDUE002.IM_ソロ選手結果2_1, PartsDUE002.IM_ソロ選手結果2_2,
                PartsDUE002.IM_ソロ選手結果2_3, PartsDUE002.IM_ソロ選手結果2_4,
            })
            {
                _partsMain.フェードアウト(true, img, sb, 0);
            }

            foreach (var lb in new System.Windows.Controls.Label[]
            {
                PartsDUE002.LB_選手紹介_1,
                PartsDUE002.LB_PCS名1_1,  PartsDUE002.LB_PCS名1_2,
                PartsDUE002.LB_PCS名1_3,  PartsDUE002.LB_PCS名1_4,
                PartsDUE002.LB_Red名_1,   PartsDUE002.LB_Total名_1, PartsDUE002.LB_Rank名_1,
                PartsDUE002.LB_PCS1_1,    PartsDUE002.LB_PCS1_2,
                PartsDUE002.LB_PCS1_3,    PartsDUE002.LB_PCS1_4,
                PartsDUE002.LB_Red_1,     PartsDUE002.LB_Total_1,   PartsDUE002.LB_Rank_1,
                PartsDUE002.LB_選手紹介_2,
                PartsDUE002.LB_PCS名2_1,  PartsDUE002.LB_PCS名2_2,
                PartsDUE002.LB_PCS名2_3,  PartsDUE002.LB_PCS名2_4,
                PartsDUE002.LB_Red名_2,   PartsDUE002.LB_Total名_2, PartsDUE002.LB_Rank名_2,
                PartsDUE002.LB_PCS2_1,    PartsDUE002.LB_PCS2_2,
                PartsDUE002.LB_PCS2_3,    PartsDUE002.LB_PCS2_4,
                PartsDUE002.LB_Red_2,     PartsDUE002.LB_Total_2,   PartsDUE002.LB_Rank_2,
            })
            {
                _partsMain.フェードアウト(true, lb, sb, 0);
            }

            sb.Completed += (s, e) => RaiseLastStepFadeOutCompleted();
            sb.Begin();
        }

        /// <summary>
        /// DA_Master の採点方式 PCS設定から PCS列名を DUE002 の両選手列に設定する。
        /// </summary>
        private void SetPCSNames()
        {
            string[] 名前 = { "PCS1", "PCS2", "PCS3", "PCS4" };

            if (DA_Master != null && !string.IsNullOrEmpty(_採点方式ID))
            {
                var 採点方式リスト = DA_Master["DJ_採点方式リスト"]?.AsArray();
                var 採点方式 = 採点方式リスト?.FirstOrDefault(
                    s => s?["採点方式ID"]?.ToString() == _採点方式ID);
                var pcs設定 = 採点方式?["PCS設定"]?.AsArray();
                if (pcs設定 != null)
                    for (int i = 0; i < 4 && i < pcs設定.Count; i++)
                        名前[i] = pcs設定[i]?["PCS名"]?.ToString() ?? 名前[i];
            }

            // 1選手目列
            PartsDUE002.LB_PCS名1_1.Content  = 名前[0];
            PartsDUE002.LB_PCS名1_2.Content  = 名前[1];
            PartsDUE002.LB_PCS名1_3.Content  = 名前[2];
            PartsDUE002.LB_PCS名1_4.Content  = 名前[3];
            PartsDUE002.LB_Red名_1.Content   = "Red";
            PartsDUE002.LB_Total名_1.Content = "Total";
            PartsDUE002.LB_Rank名_1.Content  = "Rank";

            // 2選手目列
            PartsDUE002.LB_PCS名2_1.Content  = 名前[0];
            PartsDUE002.LB_PCS名2_2.Content  = 名前[1];
            PartsDUE002.LB_PCS名2_3.Content  = 名前[2];
            PartsDUE002.LB_PCS名2_4.Content  = 名前[3];
            PartsDUE002.LB_Red名_2.Content   = "Red";
            PartsDUE002.LB_Total名_2.Content = "Total";
            PartsDUE002.LB_Rank名_2.Content  = "Rank";
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
