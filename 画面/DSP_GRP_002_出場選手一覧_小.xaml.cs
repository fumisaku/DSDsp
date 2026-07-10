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
    /// DSP_GRP_002_出場選手一覧_小.xaml の相互作用ロジック
    /// </summary>
    public partial class DSP_GRP_002_出場選手一覧_小 : DSDspScreenBase
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
        #endregion

        #region プロパティ
        /// <summary>
        /// 総ステップ数：Step1(1) + Step2(1) + [Step3+Step4] × ページ数 + Step5(1)
        /// ヒート番号が 8 以下なら 5 ステップ、9～16 なら 7 ステップ、以降 2 ずつ増加。
        /// </summary>
        protected override int TotalSteps => 2 + ページ数 * 2 + 1;

        /// <summary>表示ページ数（8件/ページ）</summary>
        private int ページ数 => Math.Max(1, (int)Math.Ceiling(ヒート番号 / 8.0));
        #endregion

        #region コンストラクタ
        public DSP_GRP_002_出場選手一覧_小()
        {
            InitializeComponent();
            this.Loaded += DSP_GRP_002_出場選手一覧_小_Loaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSP_GRP_002_出場選手一覧_小_Loaded(object sender, RoutedEventArgs e)
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
            //   case 0       → Step1
            //   case 1       → Step2
            //   case 2+p*2   → Step3 (p=0,1,2... ページ目)
            //   case 3+p*2   → Step4
            //   最後          → Step5
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

            // Step3/Step4 の繰り返しブロック（case 2以降、2ステップずつ）
            int ブロック内 = _currentStep - 2;   // 0始まり
            int ページ = ブロック内 / 2;
            int ブロック位置 = ブロック内 % 2;

            if (ページ < ページ数)
            {
                if (ブロック位置 == 0)
                {
                    Step3(DV_Result, ページ * 8);
                }
                else
                {
                    Step4();
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
         
            // LST004 の文字と画像を非表示
            PartsLST004.IM_タイトル1.Visibility = Visibility.Collapsed;
            PartsLST004.IM_タイトル2.Visibility = Visibility.Collapsed;
            PartsLST004.IM_タイトル3.Visibility = Visibility.Collapsed;

            PartsLST004.LB_タイトル1.Visibility = Visibility.Collapsed;
            PartsLST004.LB_タイトル2.Visibility = Visibility.Collapsed;
            PartsLST004.LB_タイトル3.Visibility = Visibility.Collapsed;

            //PartsLST004.LB_タイトル_減点.Visibility = Visibility.Collapsed;
            //PartsLST004.LB_タイトル_Total.Visibility = Visibility.Collapsed;

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

            // 一旦非表示にする
            非表示();

            // COM003 の　LB_右上をクリアする
            PartsCOM003.LB_右上.Content = string.Empty;

            // COM001 のロゴを表示する
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));

            // COM001/COM002 の標準ヘッダを設定（競技会名・区分ラウンド名・種目情報）
            _採点方式ID = SetCommonHeader(PartsCOM001.TB_左上1, PartsCOM001.TB_左上2, PartsCOM002.LB_右上);
            if (DA_Master == null) return;

            // DS_Statusから選手情報を取得
            _背番号 = DSDspDataHelper.Get背番号FromHeat(DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);
            var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, _背番号);
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
            // PartsMainの初期化確認
            EnsurePartsMainInitialized();

            if (_partsMain == null) return;


            // 画像の初期状態を設定（フェードイン用に透明にする）
            PartsLST004.IM_タイトル1.Opacity = 0;
            PartsLST004.IM_タイトル2.Opacity = 0;
            PartsLST004.IM_タイトル3.Opacity = 0;

            // 画像のフェードインアニメーション
            // ① まず IM_種目1・IM_種目2 をフェードイン（beginTime=0: 即開始、800ms で完了）
            // ② その後（1000ms後）に IM_ソロ選手結果1～4 をフェードイン
            var imageStoryboard = new Storyboard();
            _partsMain.フェードイン(true, PartsLST004.IM_タイトル1, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsLST004.IM_タイトル2, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsLST004.IM_タイトル3, imageStoryboard, 0);


            // 画像フェードイン完了後に表示
            imageStoryboard.Completed += (s, e) =>
            {
                // 区分ラウンド名・種目名をヘルパーから取得して設定
                string 区分名 = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
                string ラウンド名 = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);
                PartsLST004.LB_タイトル1.Content = 区分名 + " " + ラウンド名;
                PartsLST004.LB_タイトル2.Content = DSDspDataHelper.Get種目名(DA_Master, 区分番号, ラウンド番号, 種目番号);

                PartsLST004.LB_タイトル3.Content = "出場選手一覧";



                // 一気に表示
                PartsLST004.LB_タイトル1.Visibility = Visibility.Visible;
                PartsLST004.LB_タイトル2.Visibility = Visibility.Visible;
                PartsLST004.LB_タイトル3.Visibility = Visibility.Visible;
                //PartsLST004.LB_タイトル_減点.Visibility = Visibility.Visible;
                //PartsLST004.LB_タイトル_Total.Visibility = Visibility.Visible;



              
                // フォントサイズ自動調整  区分ラウンド名
                _partsMain?.フォントサイズ自動調整(
                    label: PartsLST004.LB_タイトル1,
                    text: PartsLST004.LB_タイトル1.Content.ToString(),
                    maxWidth: 300,
                    maxFontSize: 14,
                    minFontSize: 8,
                    fontFamilyName: FONT_FAMILY_NAME);

                // フォントサイズ自動調整  種目名
                _partsMain?.フォントサイズ自動調整(
                    label: PartsLST004.LB_タイトル2,
                    text: PartsLST004.LB_タイトル2.Content.ToString(),
                    maxWidth: 450,
                    maxFontSize: 16,
                    minFontSize: 8,
                    fontFamilyName: FONT_FAMILY_NAME);



                // フェードイン
                var playerStoryboard = new Storyboard();
                PartsLST004.LB_タイトル1.Opacity = 0;
                PartsLST004.LB_タイトル2.Opacity = 0;
                PartsLST004.LB_タイトル3.Opacity = 0;
                //PartsLST004.LB_タイトル_減点.Opacity = 0;
                //PartsLST004.LB_タイトル_Total.Opacity = 0;

                _partsMain?.フェードイン(true, PartsLST004.LB_タイトル1, playerStoryboard, 100);
                _partsMain?.フェードイン(true, PartsLST004.LB_タイトル2, playerStoryboard, 100);
                _partsMain?.フェードイン(true, PartsLST004.LB_タイトル3, playerStoryboard, 100);
               // _partsMain?.フェードイン(true, PartsLST004.LB_タイトル_減点, playerStoryboard, 100);
               // _partsMain?.フェードイン(true, PartsLST004.LB_タイトル_Total, playerStoryboard, 100);
                playerStoryboard.Begin();
            };

            imageStoryboard.Begin();

            // 画像のスライドアニメーション
            CreateAndStartSlideAnimation(PartsLST004.IM_タイトル1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsLST004.IM_タイトル2, SLIDE_FROM_LEFT);
            CreateAndStartSlideAnimation(PartsLST004.IM_タイトル3, SLIDE_FROM_RIGHT);

        }



        /// <summary>
        /// Step3: 選手一覧の表示（番号・背番号・選手名・所属）
        /// DS_Statusから区分番号・ラウンド番号・種目番号・ヒート番号をキーに出場選手背番号リストを取得し、
        /// DA_Masterから選手情報を取得して開始インデックスから最大8件を表示。
        /// IM_明細1～N を順にフェードイン後、結果ラベルを一斉フェードイン。
        /// </summary>
        /// <param name="dvResult">DV_Result（未使用。シグネチャ統一のため保持）</param>
        /// <param name="開始インデックス">何件目（0始まり）から表示するか。ページング用。</param>
        public void Step3(JsonNode? dvResult, int 開始インデックス = 0)
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            // ---- DS_Statusから出場選手背番号リストを取得 ----
            var 全背番号リスト = DSDspDataHelper.Get背番号リストFromHeat(
                DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);

            // 開始インデックスから最大8件を取得
            var 表示対象 = 全背番号リスト.Skip(開始インデックス).Take(8).ToList();
            int 表示件数 = 表示対象.Count;
            if (表示件数 == 0) return;

            // ---- ラベルにデータをセット（非表示のまま）----
            for (int i = 0; i < 表示件数; i++)
            {
                string 背番号 = 表示対象[i];
                int 番号 = 開始インデックス + i + 1;   // 1始まりの通し番号

                // DA_Masterから選手情報を取得
                var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, 背番号);
                string 選手名L = DSDspDataHelper.Get選手名L(選手情報);
                string 選手名P = DSDspDataHelper.Get選手名P(選手情報);
                // パートナー名がブランクの場合はリーダー名のみ
                string 選手名表示 = string.IsNullOrEmpty(選手名P)
                    ? 選手名L
                    : 選手名L + "・" + 選手名P;
                string 所属 = DSDspDataHelper.Get所属(選手情報);

                var 前景色 = new SolidColorBrush(Colors.DarkBlue);

                // 順位列 → 通し番号
                _順位LB[i].Content = 番号.ToString();
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
                    maxWidth: 148,
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
        public void Step4()
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
            // PartsLST004のタイトル画像・ラベルをフェードアウト
            _partsMain.フェードアウト(true, PartsLST004.IM_タイトル1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST004.IM_タイトル2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST004.IM_タイトル3, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST004.LB_タイトル1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST004.LB_タイトル2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsLST004.LB_タイトル3, fadeOutStoryboard, 0);

            //_partsMain.フェードアウト(true, PartsLST004.LB_タイトル_減点, fadeOutStoryboard, 0);
            //_partsMain.フェードアウト(true, PartsLST004.LB_タイトル_Total, fadeOutStoryboard, 0);
            fadeOutStoryboard.Begin();
        }

        #endregion
    }
}

