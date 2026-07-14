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
    /// DSP_GRP_001_出場選手一覧_大.xaml の相互作用ロジック
    /// </summary>
    public partial class DSP_GRP_001_出場選手一覧_大 : DSDspScreenBase
    {
        #region 定数定義
        private const int ANIMATION_DURATION_SECONDS = 1;
        private const double SLIDE_FROM_LEFT = -1000;
        private const double SLIDE_FROM_RIGHT = 1000;

        // フォントサイズ調整用の定数
        private const string FONT_FAMILY_NAME = "Segoe UI Semibold";
        #endregion

        #region フィールド
        // Step1で取得したデータを保持
        private string _採点方式ID = string.Empty;
        private string _背番号 = string.Empty;
        private string _選手名L = string.Empty;
        private string _選手名P = string.Empty;

        // 表示対象行の Image / Label 配列（Step3で初期化、他メソッドからも参照可能）
        private Image[]? _明細IM;
        private Label[]? _順位LB;
        private Label[]? _背番号LB;
        private Label[]? _選手名LB;
        private Label[]? _所属LB;
        private Label[]? _減点LB;
        private Label[]? _得点LB;
        // 直前の Step3 で実際に表示した件数（Step4 のフェードアウト範囲を限定するために使用）
        private int _前回表示件数 = 0;
        // オーバービューモード時の総選手数（Step1でキャッシュ）
        private int _総表示件数 = 0;
        // Step1 で確定したページ数（TotalSteps を一定に保つために使用）
        private int _ページ数 = 1;
        #endregion

        #region プロパティ
        /// <summary>
        /// 総ステップ数：Step1+Step2+Step3(1) + Step4(1) + [Step3+Step4] × (ページ数-1) + Step5(1)
        /// Step1・Step2・1ページ目Step3は同時実行のため、通常より2ステップ少ない。
        /// </summary>
        protected override int TotalSteps => _ページ数 == 1 ? 2 : _ページ数 * 2 + 1;
        public override bool WaitsForLastStepFadeOut => true;
        #endregion

        #region コンストラクタ
        public DSP_GRP_001_出場選手一覧_大()
        {
            InitializeComponent();
            this.Loaded += DSP_GRP_001_出場選手一覧_大_Loaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSP_GRP_001_出場選手一覧_大_Loaded(object sender, RoutedEventArgs e)
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
            // ステップ割り当て:
            //   case 0       → Step1 + Step2 + Step3(p=0) 自動実行
            //   case 1       → Step4(p=0)
            //   case 1+p*2   → Step3(p=1,2...) ※p≥1 の場合
            //   case 2+p*2   → Step4(p=1,2...)
            //   最後          → Step5
            if (_currentStep == 0)
            {
                Step1();
                Step2();
                // Step1+Step2の直後に1ページ目のStep3を自動実行
                Step3(DV_Result, 0);
                return;
            }

            // _currentStep=1 以降: Step4(p=0), Step3(p=1), Step4(p=1), ...
            int ブロック内 = _currentStep;   // 1→Step4(p=0), 2→Step3(p=1), 3→Step4(p=1)...

            // p=0 のStep4 (ブロック内=1)
            if (ブロック内 == 1)
            {
                // 1ページのみの場合はStep4完了後にStep5を自動実行
                Step4(_ページ数 == 1 ? (Action)Step5 : null);
                return;
            }

            // p≥1 のStep3/Step4 (ブロック内≥2, 2ステップずつ)
            int p = (ブロック内 - 2) / 2 + 1;     // ページ番号（1始まり）
            int pos = (ブロック内 - 2) % 2;        // 0=Step3, 1=Step4

            if (p < _ページ数)
            {
                if (pos == 0)
                {
                    Step3(DV_Result, p * 8);
                }
                else
                {
                    // 最終ページのStep4完了後はStep5を自動実行
                    Step4(p == _ページ数 - 1 ? (Action)Step5 : null);
                }
                return;
            }

            // 全ページ終了後 → Step5
            Step5();
        }
        #endregion

        #region プライベートメソッド

        private void 非表示()
        {
         
            // LST001 の文字と画像を非表示
            PartsLST001.IM_タイトル1.Visibility = Visibility.Collapsed;
            PartsLST001.IM_タイトル2.Visibility = Visibility.Collapsed;
            PartsLST001.IM_タイトル3.Visibility = Visibility.Collapsed;

            PartsLST001.LB_タイトル1.Visibility = Visibility.Collapsed;
            PartsLST001.LB_タイトル2.Visibility = Visibility.Collapsed;
            PartsLST001.LB_タイトル3.Visibility = Visibility.Collapsed;

            PartsLST001.LB_タイトル_減点.Visibility = Visibility.Collapsed;
            PartsLST001.LB_タイトル_Total.Visibility = Visibility.Collapsed;

            for (int i = 0; i <8 ; i++)
            {
                _明細IM[i].Visibility = Visibility.Collapsed;

                _順位LB[i].Visibility = Visibility.Collapsed;
                _背番号LB[i].Visibility = Visibility.Collapsed;
                _選手名LB[i].Visibility = Visibility.Collapsed;
                _所属LB[i].Visibility = Visibility.Collapsed;
                _減点LB[i].Visibility = Visibility.Collapsed;
                _得点LB[i].Visibility = Visibility.Collapsed;
            }           

        }

        /// <summary>
        /// Step1: 競技会名、区分名、ラウンド名、種目情報、選手情報を表示
        /// DA_MasterとDS_Statusから情報を取得
        /// </summary>
        public void Step1()
        {
            // COM000_PartsMainを初期化
            EnsurePartsMainInitialized();


            // ---- 表示対象行の Image / Label を配列化（1始まり→0始まりインデックス）----
            _明細IM = new[]
            {
                PartsLST001.IM_明細1, PartsLST001.IM_明細2, PartsLST001.IM_明細3, PartsLST001.IM_明細4,
                PartsLST001.IM_明細5, PartsLST001.IM_明細6, PartsLST001.IM_明細7, PartsLST001.IM_明細8
            };
            _順位LB = new[]
            {
                PartsLST001.LB_結果1_順位, PartsLST001.LB_結果2_順位, PartsLST001.LB_結果3_順位, PartsLST001.LB_結果4_順位,
                PartsLST001.LB_結果5_順位, PartsLST001.LB_結果6_順位, PartsLST001.LB_結果7_順位, PartsLST001.LB_結果8_順位
            };
            _背番号LB = new[]
            {
                PartsLST001.LB_結果1_背番号, PartsLST001.LB_結果2_背番号, PartsLST001.LB_結果3_背番号, PartsLST001.LB_結果4_背番号,
                PartsLST001.LB_結果5_背番号, PartsLST001.LB_結果6_背番号, PartsLST001.LB_結果7_背番号, PartsLST001.LB_結果8_背番号
            };
            _選手名LB = new[]
            {
                PartsLST001.LB_結果1_選手名, PartsLST001.LB_結果2_選手名, PartsLST001.LB_結果3_選手名, PartsLST001.LB_結果4_選手名,
                PartsLST001.LB_結果5_選手名, PartsLST001.LB_結果6_選手名, PartsLST001.LB_結果7_選手名, PartsLST001.LB_結果8_選手名
            };
            // DSP_GRP_001 画面側で選手名ラベルの Width をパーツ定義値（153）の 2 倍に変更
            foreach (var lb in _選手名LB)
            {
                lb.Width = 306;
            }
            _所属LB = new[]
            {
                PartsLST001.LB_結果1_所属, PartsLST001.LB_結果2_所属, PartsLST001.LB_結果3_所属, PartsLST001.LB_結果4_所属,
                PartsLST001.LB_結果5_所属, PartsLST001.LB_結果6_所属, PartsLST001.LB_結果7_所属, PartsLST001.LB_結果8_所属
            };
            _減点LB = new[]
            {
                PartsLST001.LB_結果1_減点, PartsLST001.LB_結果2_減点, PartsLST001.LB_結果3_減点, PartsLST001.LB_結果4_減点,
                PartsLST001.LB_結果5_減点, PartsLST001.LB_結果6_減点, PartsLST001.LB_結果7_減点, PartsLST001.LB_結果8_減点
            };
            _得点LB = new[]
            {
                PartsLST001.LB_結果1_得点, PartsLST001.LB_結果2_得点, PartsLST001.LB_結果3_得点, PartsLST001.LB_結果4_得点,
                PartsLST001.LB_結果5_得点, PartsLST001.LB_結果6_得点, PartsLST001.LB_結果7_得点, PartsLST001.LB_結果8_得点
            };

            // 一旦非表示にする
            非表示();

            // COM003 の　LB_右上をクリアする
            PartsCOM003.LB_右上.Content = string.Empty;

            // COM001 のロゴを表示する
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));

            // COM001/COM002 の標準ヘッダを設定（競技会名・区分ラウンド名・種目情報）
            _採点方式ID = SetCommonHeader(PartsCOM001.TB_左上1, PartsCOM001.TB_左上2, PartsCOM002.LB_右上);
            if (DA_Master == null) return;

            // 表示件数を計算してキャッシュ（TotalSteps / ページ数 の算出に使用）
            if (IsOverviewMode)
            {
                // デュエルヒート表モード：全ヒートの選手総数
                _総表示件数 = DSDspDataHelper.Get全ヒート選手リスト(
                    DS_Status, 区分番号, ラウンド番号, 種目番号).Count;
            }
            else
            {
                // 通常モード：指定ヒートの出場選手数
                _総表示件数 = DSDspDataHelper.Get背番号リストFromHeat(
                    DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号).Count;
            }
            _ページ数 = Math.Max(1, (int)Math.Ceiling(_総表示件数 / 8.0));

            // DS_Statusから選手情報を取得（通常モードのみ：参考情報）
            _背番号 = DSDspDataHelper.Get背番号FromHeat(DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);
            var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, _背番号, 区分番号);
            _選手名L = DSDspDataHelper.Get選手名L(選手情報);
            _選手名P = DSDspDataHelper.Get選手名P(選手情報);

            // PartsCOM003に選手情報を表示  選手情報表示しない
            // PartsCOM003.LB_右上.Content = ヒート番号.ToString() + "組目　" + _背番号 + "　" + _選手名L + "・" + _選手名P;
        }

        /// <summary>
        /// Step2: ヘッダー部分を表示する
        /// </summary>
        public void Step2()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            // --- ラベルのテキストとフォントサイズを先にセット ---
            string 区分名 = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
            string ラウンド名 = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);
            PartsLST001.LB_タイトル1.Content = 区分名 + " " + ラウンド名;
            PartsLST001.LB_タイトル2.Content = DSDspDataHelper.Get種目名(DA_Master, 区分番号, ラウンド番号, 種目番号);
            PartsLST001.LB_タイトル3.Content = "出場選手一覧";

            _partsMain.フォントサイズ自動調整(
                label: PartsLST001.LB_タイトル1,
                text: PartsLST001.LB_タイトル1.Content.ToString() ?? "",
                maxWidth: 300, maxFontSize: 14, minFontSize: 8,
                fontFamilyName: FONT_FAMILY_NAME);
            _partsMain.フォントサイズ自動調整(
                label: PartsLST001.LB_タイトル2,
                text: PartsLST001.LB_タイトル2.Content.ToString() ?? "",
                maxWidth: 450, maxFontSize: 16, minFontSize: 8,
                fontFamilyName: FONT_FAMILY_NAME);

            // --- Visible にしてから Opacity=0 で隠す ---
            PartsLST001.LB_タイトル1.Visibility    = Visibility.Visible;
            PartsLST001.LB_タイトル2.Visibility    = Visibility.Visible;
            PartsLST001.LB_タイトル3.Visibility    = Visibility.Visible;

            PartsLST001.IM_タイトル1.Opacity        = 0;
            PartsLST001.IM_タイトル2.Opacity        = 0;
            PartsLST001.IM_タイトル3.Opacity        = 0;
            PartsLST001.LB_タイトル1.Opacity        = 0;
            PartsLST001.LB_タイトル2.Opacity        = 0;
            PartsLST001.LB_タイトル3.Opacity        = 0;
            PartsLST001.LB_タイトル_減点.Opacity    = 0;
            PartsLST001.LB_タイトル_Total.Opacity   = 0;

            // --- IM_タイトルとラベルを同時にフェードイン ---
            var sb = new Storyboard();
            _partsMain.フェードイン(true, PartsLST001.IM_タイトル1, sb, 0);
            _partsMain.フェードイン(true, PartsLST001.IM_タイトル2, sb, 0);
            _partsMain.フェードイン(true, PartsLST001.IM_タイトル3, sb, 0);
            _partsMain.フェードイン(true, PartsLST001.LB_タイトル1, sb, 0);
            _partsMain.フェードイン(true, PartsLST001.LB_タイトル2, sb, 0);
            _partsMain.フェードイン(true, PartsLST001.LB_タイトル3, sb, 0);
            // LB_タイトル_減点・LB_タイトル_Total はこの画面では非表示
            sb.Begin();

            // IM_タイトルのスライドアニメーション（フェードインと同時に開始）
            CreateAndStartSlideAnimation(PartsLST001.IM_タイトル1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsLST001.IM_タイトル2, SLIDE_FROM_LEFT);
            CreateAndStartSlideAnimation(PartsLST001.IM_タイトル3, SLIDE_FROM_RIGHT);
        }



        /// <summary>
        /// Step3: 選手一覧の表示（番号・背番号・選手名・所属）
        /// IsOverviewMode=true のデュエルヒート表モードでは、種目内の全ヒート選手をヒート番号昇順で一覧表示し、
        /// 順位列に "NH"（ヒート番号）を表示する。
        /// 通常モードでは DS_Status の指定ヒートの出場選手を表示する。
        /// IM_明細1～N を順にフェードイン後、結果ラベルを一斉フェードイン。
        /// </summary>
        /// <param name="dvResult">DV_Result（未使用。シグネチャ統一のため保持）</param>
        /// <param name="開始インデックス">何件目（0始まり）から表示するか。ページング用。</param>
        public void Step3(JsonNode? dvResult, int 開始インデックス = 0)
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            List<(string 順位表示, string 背番号)> 表示データリスト;

            if (IsOverviewMode)
            {
                // ---- デュエルヒート表モード：全ヒートの選手をヒート番号昇順で取得 ----
                var 全ヒート選手 = DSDspDataHelper.Get全ヒート選手リスト(
                    DS_Status, 区分番号, ラウンド番号, 種目番号);
                表示データリスト = 全ヒート選手
                    .Select(x => ($"{x.HeatNo}H", x.PlayerNo))
                    .ToList();
            }
            else
            {
                // ---- 通常モード：指定ヒートの出場選手を取得 ----
                var 全背番号リスト = DSDspDataHelper.Get背番号リストFromHeat(
                    DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);
                表示データリスト = 全背番号リスト
                    .Select((no, idx) => ((開始インデックス + idx + 1).ToString(), no))
                    .ToList();
            }

            // 開始インデックスから最大8件を取得
            var 表示対象 = 表示データリスト.Skip(開始インデックス).Take(8).ToList();
            int 表示件数 = 表示対象.Count;
            if (表示件数 == 0) return;

            // ---- ラベルにデータをセット（非表示のまま）----
            for (int i = 0; i < 表示件数; i++)
            {
                string 順位表示 = 表示対象[i].順位表示;
                string 背番号 = 表示対象[i].背番号;

                // DA_Masterから選手情報を取得
                var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, 背番号, 区分番号);
                string 選手名L = DSDspDataHelper.Get選手名L(選手情報);
                string 選手名P = DSDspDataHelper.Get選手名P(選手情報);
                // パートナー名がブランクの場合はリーダー名のみ
                string 選手名表示 = string.IsNullOrEmpty(選手名P)
                    ? 選手名L
                    : 選手名L + "・" + 選手名P;
                string 所属 = DSDspDataHelper.Get所属(選手情報);

                var 前景色 = new SolidColorBrush(Colors.DarkBlue);

                // 順位列 → ヒート番号（"NH"形式）または通し番号
                _順位LB[i].Content = 順位表示;
                _背番号LB[i].Content = 背番号;
                _選手名LB[i].Content = 選手名表示;
                _所属LB[i].Content = string.Empty;
                _減点LB[i].Content = string.Empty;
                _得点LB[i].Content = 所属;

                _順位LB[i].Foreground = 前景色;
                _背番号LB[i].Foreground = 前景色;
                _選手名LB[i].Foreground = 前景色;
                _所属LB[i].Foreground = 前景色;
                _減点LB[i].Foreground = 前景色;
                _得点LB[i].Foreground = 前景色;

                // Visibility は Collapsed のまま（フェードイン時に Visible にする）
                _順位LB[i].Opacity = 0;
                _背番号LB[i].Opacity = 0;
                _選手名LB[i].Opacity = 0;
                _所属LB[i].Opacity = 0;
                _減点LB[i].Opacity = 0;
                _得点LB[i].Opacity = 0;
            }

            // ---- フォントサイズ自動調整（選手名）----
            for (int i = 0; i < 表示件数; i++)
            {
                _partsMain.フォントサイズ自動調整(
                    label: _選手名LB[i],
                    text: _選手名LB[i].Content?.ToString() ?? "",
                    maxWidth: 300,
                    maxFontSize: 16,
                    minFontSize: 8,
                    fontFamilyName: FONT_FAMILY_NAME);
            }

            // ---- フォントサイズ自動調整（所属）----
            for (int i = 0; i < 表示件数; i++)
            {
                _partsMain.フォントサイズ自動調整(
                    label: _所属LB[i],
                    text: _所属LB[i].Content?.ToString() ?? "",
                    maxWidth: 140,
                    maxFontSize: 16,
                    minFontSize: 8,
                    fontFamilyName: FONT_FAMILY_NAME);
            }

            // ---- IM_明細を順にフェードイン（1秒で 表示件数 枚）----
            // 間隔: 1000ms ÷ 表示件数（最低100ms）
            int 間隔ms = 表示件数 > 1 ? Math.Max(1000 / 表示件数, 100) : 0;
            var 明細Storyboard = new Storyboard();
            for (int i = 0; i < 表示件数; i++)
            {
                _明細IM[i].Opacity = 0;
                _partsMain.フェードイン(true, _明細IM[i], 明細Storyboard, i * 間隔ms);
            }

            // 明細フェードイン完了後、結果ラベルを一斉フェードイン
            明細Storyboard.Completed += (s, e) =>
            {
                var 結果Storyboard = new Storyboard();
                for (int i = 0; i < 表示件数; i++)
                {
                    _partsMain?.フェードイン(true, _順位LB[i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _背番号LB[i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _選手名LB[i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _所属LB[i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _減点LB[i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, _得点LB[i], 結果Storyboard, 0);
                }
                結果Storyboard.Begin();
            };

            // Step3 で実際に表示した件数を保存（Step4 のフェードアウト範囲に使用）
            _前回表示件数 = 表示件数;

            明細Storyboard.Begin();
        }

        /// <summary>
        /// Step4: 直前の Step3 で表示した行だけフェードアウト。
        /// 表示していない行（Opacity=0）には触れず、一瞬見えてしまう現象を防ぐ。
        /// </summary>
        /// <param name="onCompleted">フェードアウト完了後に呼び出すコールバック（省略可）</param>
        public void Step4(Action? onCompleted = null)
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            var fadeOutStoryboard = new Storyboard();

            // _前回表示件数 件分だけフェードアウト（表示していない行はスキップ）
            for (int i = 0; i < _前回表示件数; i++)
            {
                _partsMain.フェードアウト(true, _明細IM[i], fadeOutStoryboard, 0);

                _partsMain.フェードアウト(true, _順位LB[i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _背番号LB[i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _選手名LB[i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _所属LB[i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _減点LB[i], fadeOutStoryboard, 0);
                _partsMain.フェードアウト(true, _得点LB[i], fadeOutStoryboard, 0);
            }

            if (onCompleted != null)
                fadeOutStoryboard.Completed += (s, e) => onCompleted();

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
            // PartsLST001のタイトル画像・ラベルをフェードアウト
            _partsMain.フェードアウト(true, PartsLST001.IM_タイトル1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST001.IM_タイトル2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST001.IM_タイトル3, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST001.LB_タイトル1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST001.LB_タイトル2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST001.LB_タイトル3, fadeOutStoryboard, 0);

            _partsMain.フェードアウト(true, PartsLST001.LB_タイトル_減点, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST001.LB_タイトル_Total, fadeOutStoryboard, 0);
            fadeOutStoryboard.Completed += (s, e) => RaiseLastStepFadeOutCompleted();
            fadeOutStoryboard.Begin();
        }

        #endregion
    }
}

