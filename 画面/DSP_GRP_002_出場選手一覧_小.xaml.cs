using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_GRP_002_出場選手一覧_小.xaml の相互作用ロジック
    /// </summary>
    public partial class DSP_GRP_002_出場選手一覧_小 : DSDspScreenBase
    {
        #region 定数定義
        private const double SLIDE_FROM_LEFT  = -1000;
        private const double SLIDE_FROM_RIGHT =  1000;
        private const string FONT_FAMILY_NAME = "Segoe UI Semibold";
        #endregion

        #region フィールド
        private string _採点方式ID = string.Empty;
        private string _背番号    = string.Empty;
        private string _選手名L   = string.Empty;
        private string _選手名P   = string.Empty;

        private Image[]? _明細IM;
        private Label[]? _順位LB;
        private Label[]? _背番号LB;
        private Label[]? _選手名LB;
        private Label[]? _所属LB;
        private int _前回表示件数 = 0;
        private int _総表示件数  = 0;
        private int _ページ数    = 1;
        private int _全ヒート数  = 0;

        // ページング自動進行のための内部ステップ管理
        // ページング部分（Step4完了後の自動遷移）は画面クラスが自律して進める
        private bool _ページング完了 = false;
        #endregion

        #region プロパティ
        /// <summary>
        /// 総ステップ数（Advance()呼び出し回数）:
        ///   1ページ: Step0(表示) + Step1(FO) = 2 ただし Step1 はページング完了後のLSTステップへ続く
        ///   複数ページ: Step0 + ページ数×2-1(ページング) + 1(LST) = ページ数×2+1
        ///   + クロマキー/全画面によるLSTステップ数
        /// </summary>
        protected override int TotalSteps
        {
            get
            {
                int 基本 = _ページ数 == 1 ? 2 : _ページ数 * 2 + 1;
                int 追加 = (_全ヒート数 >= 2 && ChromaKeyMode) ? 2 : 1;
                return 基本 + 追加;
            }
        }

        // フェードアウトアニメーション完了後に RaiseScreenCompleted() を呼ぶため true
        public override bool WaitsForLastStepFadeOut => true;
        #endregion

        #region コンストラクタ
        public DSP_GRP_002_出場選手一覧_小()
        {
            InitializeComponent();
            this.Loaded += (s, e) => EnsurePartsMainInitialized();
        }
        #endregion

        #region オーバーライド
        /// <summary>
        /// Advance() から呼ばれる。_currentStep の値でステップを振り分ける。
        /// 注意: このメソッド内で _currentStep を変更してはならない（Advance()が管理する）。
        /// </summary>
        protected override void ExecuteCurrentStep()
        {
            int 基本ステップ数  = _ページ数 == 1 ? 2 : _ページ数 * 2 + 1;
            int 最初のLSTステップ = 基本ステップ数;

            // ── LST ステップ ──
            if (_全ヒート数 >= 2)
            {
                if (_currentStep == 最初のLSTステップ)
                {
                    if (ChromaKeyMode)
                        Step5_LST005フェードイン();
                    else
                        Step6_フェードアウト();
                    return;
                }
                if (_currentStep == 最初のLSTステップ + 1)
                {
                    Step6_フェードアウト();
                    return;
                }
            }
            else
            {
                if (_currentStep == 最初のLSTステップ)
                {
                    Step5_タイトルフェードアウト();
                    return;
                }
            }

            // ── 初回ステップ（表示） ──
            if (_currentStep == 0)
            {
                Step1();
                Step2();
                Step3(DV_Result, 0);
                return;
            }

            // ── ページングステップ ──
            if (_currentStep == 1)
            {
                // p=0 の Step4
                // 1ページのみ → 完了後に LST ステップへ自動遷移
                Step4(_ページ数 == 1 ? (Action)OnページングComplete : null);
                return;
            }

            int ブロック内 = _currentStep;
            int p   = (ブロック内 - 2) / 2 + 1;
            int pos = (ブロック内 - 2) % 2;

            if (p < _ページ数)
            {
                if (pos == 0)
                    Step3(DV_Result, p * 8);
                else
                    Step4(p == _ページ数 - 1 ? (Action)OnページングComplete : null);
                return;
            }

            // フォールスルー（念のため）
            if (_currentStep >= TotalSteps) return;
            OnページングComplete();
        }
        #endregion

        #region ページング完了後の自動遷移
        /// <summary>
        /// Step4（最終ページ）完了後にアニメーションコールバックから呼ばれる。
        /// LST ステップへ自動遷移する。
        /// </summary>
        private void OnページングComplete()
        {
            // _currentStep を LST ステップに合わせてから実行
            // Advance() は後で ++ するが、ここでは画面側から直接 _currentStep を進める
            int 基本ステップ数 = _ページ数 == 1 ? 2 : _ページ数 * 2 + 1;
            _currentStep = 基本ステップ数;

            if (_全ヒート数 >= 2)
            {
                if (ChromaKeyMode)
                {
                    Step5_LST005フェードイン();
                    // Step6 は次の Advance() 待ち → _currentStep を Step6 番号にセット
                    _currentStep = 基本ステップ数 + 1;
                }
                else
                {
                    Step6_フェードアウト();
                }
            }
            else
            {
                Step5_タイトルフェードアウト();
            }
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
                _明細IM![i].Visibility  = Visibility.Collapsed;
                _順位LB![i].Visibility  = Visibility.Collapsed;
                _背番号LB![i].Visibility = Visibility.Collapsed;
                _選手名LB![i].Visibility = Visibility.Collapsed;
                _所属LB![i].Visibility  = Visibility.Collapsed;
            }
        }

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
            _所属LB = new[]
            {
                PartsLST004.LB_結果1_得点, PartsLST004.LB_結果2_得点, PartsLST004.LB_結果3_得点, PartsLST004.LB_結果4_得点,
                PartsLST004.LB_結果5_得点, PartsLST004.LB_結果6_得点, PartsLST004.LB_結果7_得点, PartsLST004.LB_結果8_得点
            };

            非表示();

            PartsCOM003.LB_右上.Content = string.Empty;
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
            _採点方式ID = SetCommonHeader(PartsCOM001.TB_左上1, PartsCOM001.TB_左上2, PartsCOM002.LB_右上);
            if (DA_Master == null) return;

            _全ヒート数 = DSDspDataHelper.Getヒート数(DS_Status, 区分番号, ラウンド番号, 種目番号);

            if (IsOverviewMode)
                _総表示件数 = DSDspDataHelper.Get全ヒート選手リスト(DS_Status, 区分番号, ラウンド番号, 種目番号).Count;
            else
                _総表示件数 = DSDspDataHelper.Get背番号リストFromHeat(DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号).Count;

            _ページ数 = Math.Max(1, (int)Math.Ceiling(_総表示件数 / 8.0));

            _背番号 = DSDspDataHelper.Get背番号FromHeat(DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);
            var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, _背番号, 区分番号);
            _選手名L = DSDspDataHelper.Get選手名L(選手情報);
            _選手名P = DSDspDataHelper.Get選手名P(選手情報);
        }

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
                string 区分名   = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
                string ラウンド名 = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);
                PartsLST004.LB_タイトル1.Content = 区分名 + " " + ラウンド名;
                PartsLST004.LB_タイトル2.Content = DSDspDataHelper.Get種目名(DA_Master, 区分番号, ラウンド番号, 種目番号);
                int 全ヒート数 = DSDspDataHelper.Getヒート数(DS_Status, 区分番号, ラウンド番号, 種目番号);
                PartsLST004.LB_タイトル3.Content = 全ヒート数 >= 2 ? $"{ヒート番号}H出場選手一覧" : "出場選手一覧";

                PartsLST004.LB_タイトル1.Visibility = Visibility.Visible;
                PartsLST004.LB_タイトル2.Visibility = Visibility.Visible;
                PartsLST004.LB_タイトル3.Visibility = Visibility.Visible;

                _partsMain?.フォントサイズ自動調整(PartsLST004.LB_タイトル1, PartsLST004.LB_タイトル1.Content.ToString(), 300, 9, 6, FONT_FAMILY_NAME);
                _partsMain?.フォントサイズ自動調整(PartsLST004.LB_タイトル2, PartsLST004.LB_タイトル2.Content.ToString(), 450, 10, 6, FONT_FAMILY_NAME);

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

            SetLST005();
            SetLST006();
        }

        private void SetLST005()
        {
            if (_全ヒート数 < 2)
            {
                PartsLST005.Visibility = Visibility.Collapsed;
                return;
            }

            string dncCd = string.Empty;
            if (DA_Master != null)
            {
                var dance = DSDspDataHelper.Get種目(DA_Master, 区分番号, ラウンド番号, 種目番号);
                dncCd = dance?["DE_DncCd"]?.ToString() ?? string.Empty;
            }

            PartsLST005.LB_タイトル1.Content = $"現在 {dncCd} {ヒート番号}H";
            var 背番号リスト = DSDspDataHelper.Get背番号リストFromHeat(DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);
            PartsLST005.LB_明細1.Content = string.Join("  ", 背番号リスト);

            PartsLST005.Visibility = Visibility.Collapsed;
            PartsLST005.Opacity    = 0;
        }

        private void SetLST006()
        {
            int 全ヒート数 = DSDspDataHelper.Getヒート数(DS_Status, 区分番号, ラウンド番号, 種目番号);
            if (全ヒート数 < 2)
            {
                PartsLST006.Visibility = Visibility.Collapsed;
                return;
            }

            var 次ヒート = DSDspDataHelper.Get次ヒート情報(DS_Status, DA_Master, 区分番号, ラウンド番号, 種目番号, ヒート番号);
            if (次ヒート == null)
            {
                PartsLST006.Visibility = Visibility.Collapsed;
                return;
            }

            PartsLST006.LB_タイトル1.Content = $"Next {次ヒート.Value.DncCd} {次ヒート.Value.HeatNo}H";
            var 背番号リスト = DSDspDataHelper.Get背番号リストFromHeat(DS_Status, 区分番号, ラウンド番号, 次ヒート.Value.DncNo, 次ヒート.Value.HeatNo);
            PartsLST006.LB_明細1.Content = string.Join("  ", 背番号リスト);

            PartsLST006.Visibility = Visibility.Visible;
            PartsLST006.Opacity    = 0;
            var sb = new Storyboard();
            _partsMain?.フェードイン(true, PartsLST006, sb, 0);
            sb.Begin();
        }

        public void Step3(JsonNode? dvResult, int 開始インデックス = 0)
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            List<(string 順位表示, string 背番号)> 表示データリスト;

            if (IsOverviewMode)
            {
                var 全ヒート選手 = DSDspDataHelper.Get全ヒート選手リスト(DS_Status, 区分番号, ラウンド番号, 種目番号);
                表示データリスト = 全ヒート選手.Select(x => ($"{x.HeatNo}H", x.PlayerNo)).ToList();
            }
            else
            {
                var 全背番号リスト = DSDspDataHelper.Get背番号リストFromHeat(DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);
                表示データリスト = 全背番号リスト.Select((no, idx) => (string.Empty, no)).ToList();
            }

            var 表示対象 = 表示データリスト.Skip(開始インデックス).Take(8).ToList();
            int 表示件数 = 表示対象.Count;
            if (表示件数 == 0) return;

            for (int i = 0; i < 表示件数; i++)
            {
                string 順位表示 = 表示対象[i].順位表示;
                string 背番号   = 表示対象[i].背番号;
                var 選手情報    = DSDspDataHelper.Get選手情報(DA_Master, 背番号, 区分番号);
                string 選手名L  = Get苗字(DSDspDataHelper.Get選手名L(選手情報));
                string 選手名P  = Get苗字(DSDspDataHelper.Get選手名P(選手情報));
                string 選手名表示 = string.IsNullOrEmpty(選手名P) ? 選手名L : 選手名L + "・" + 選手名P;
                string 所属     = DSDspDataHelper.Get所属(選手情報);
                var 前景色      = new SolidColorBrush(Colors.DarkBlue);

                _順位LB![i].Content    = 順位表示;
                _背番号LB![i].Content  = 背番号;
                _選手名LB![i].Content  = 選手名表示;
                _所属LB![i].Content    = 所属;

                _順位LB[i].Foreground  = 前景色;
                _背番号LB[i].Foreground = 前景色;
                _選手名LB[i].Foreground = 前景色;
                _所属LB[i].Foreground  = 前景色;

                _順位LB[i].Opacity  = 0;
                _背番号LB[i].Opacity = 0;
                _選手名LB[i].Opacity = 0;
                _所属LB[i].Opacity  = 0;
            }

            for (int i = 0; i < 表示件数; i++)
                _partsMain.フォントサイズ自動調整(_選手名LB![i], _選手名LB[i].Content?.ToString() ?? "", 148, 10, 6, FONT_FAMILY_NAME);
            for (int i = 0; i < 表示件数; i++)
                _partsMain.フォントサイズ自動調整(_所属LB![i], _所属LB[i].Content?.ToString() ?? "", 140, 10, 6, FONT_FAMILY_NAME);

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
                    _partsMain?.フェードイン(true, _順位LB![i],  結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _背番号LB![i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _選手名LB![i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _所属LB![i],  結果Storyboard, 0);
                }
                結果Storyboard.Begin();
            };

            _前回表示件数 = 表示件数;
            明細Storyboard.Begin();
        }

        public void Step4(Action? onCompleted = null)
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            var fadeOutStoryboard = new Storyboard();
            for (int i = 0; i < _前回表示件数; i++)
            {
                _partsMain.フェードアウト(true, _明細IM![i],  fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _順位LB![i],  fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _背番号LB![i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _選手名LB![i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _所属LB![i],  fadeOutStoryboard, 0);
            }

            if (onCompleted != null)
                fadeOutStoryboard.Completed += (s, e) => onCompleted();

            fadeOutStoryboard.Begin();
        }

        /// <summary>
        /// Step5（全ヒート数≥2・クロマキーモード）:
        /// LST005フェードイン と タイトルフェードアウト を同時実行して停止する。
        /// 次の Advance() で Step6 が実行される。
        /// </summary>
        private void Step5_LST005フェードイン()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            PartsLST005.Visibility = Visibility.Visible;
            PartsLST005.Opacity    = 0;
            var sbIn = new Storyboard();
            _partsMain.フェードイン(true, PartsLST005, sbIn, 0);
            sbIn.Begin();

            var sbOut = new Storyboard();
            _partsMain.フェードアウト(true, PartsLST004.IM_タイトル1, sbOut, 0);
            _partsMain.フェードアウト(true, PartsLST004.IM_タイトル2, sbOut, 0);
            _partsMain.フェードアウト(true, PartsLST004.IM_タイトル3, sbOut, 0);
            _partsMain.フェードアウト(true, PartsLST004.LB_タイトル1, sbOut, 0);
            _partsMain.フェードアウト(true, PartsLST004.LB_タイトル2, sbOut, 0);
            _partsMain.フェードアウト(true, PartsLST004.LB_タイトル3, sbOut, 0);
            sbOut.Begin();
        }

        /// <summary>
        /// Step6（全ヒート数≥2）: LST005/LST006 またはタイトル/LST006 をフェードアウト。
        /// 完了後に RaiseScreenCompleted() を発火する。
        /// </summary>
        private void Step6_フェードアウト()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            var fadeOutStoryboard = new Storyboard();

            if (ChromaKeyMode)
            {
                if (PartsLST005.Visibility == Visibility.Visible)
                    _partsMain.フェードアウト(true, PartsLST005, fadeOutStoryboard, 0);
                if (PartsLST006.Visibility == Visibility.Visible)
                    _partsMain.フェードアウト(true, PartsLST006, fadeOutStoryboard, 0);
            }
            else
            {
                if (PartsLST006.Visibility == Visibility.Visible)
                    _partsMain.フェードアウト(true, PartsLST006, fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, PartsLST004.IM_タイトル1, fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, PartsLST004.IM_タイトル2, fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, PartsLST004.IM_タイトル3, fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, PartsLST004.LB_タイトル1, fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, PartsLST004.LB_タイトル2, fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, PartsLST004.LB_タイトル3, fadeOutStoryboard, 0);
            }

            fadeOutStoryboard.Completed += (s, e) => RaiseScreenCompleted();
            fadeOutStoryboard.Begin();
        }

        /// <summary>
        /// Step5（全ヒート数＜2）: タイトルをフェードアウト。
        /// 完了後に RaiseScreenCompleted() を発火する。
        /// </summary>
        private void Step5_タイトルフェードアウト()
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
            fadeOutStoryboard.Completed += (s, e) => RaiseScreenCompleted();
            fadeOutStoryboard.Begin();
        }

        private static string Get苗字(string 氏名)
        {
            if (string.IsNullOrEmpty(氏名)) return 氏名;
            for (int i = 0; i < 氏名.Length; i++)
            {
                if (氏名[i] == ' ' || 氏名[i] == '\u3000')
                    return 氏名.Substring(0, i);
            }
            return 氏名;
        }

        #endregion
    }
}
