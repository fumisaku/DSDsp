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
    public partial class DSP_SOL_007_ソロ途中結果_大 : DSDspScreenBase
    {
        #region 定数定義
        private const int ANIMATION_DURATION_SECONDS = 1;
        private const double SLIDE_FROM_LEFT = -1000;
        private const double SLIDE_FROM_RIGHT = 1000;

        // フォントサイズ調整用の定数
        private const string FONT_FAMILY_NAME = "HGPSoeiKakugothicUB";
        #endregion

        #region フィールド
        // Step1で取得したデータを保持
        private string _採点方式ID = string.Empty;
        private string _背番号 = string.Empty;
        private string _選手名L = string.Empty;
        private string _選手名P = string.Empty;
        #endregion

        #region プロパティ
        /// <summary>
        /// 総ステップ数（Step0, Step1, Step2, Step3, Step4の5ステップ）
        /// </summary>
        protected override int TotalSteps => 5;
        #endregion

        #region コンストラクタ
        public DSP_SOL_007_ソロ途中結果_大()
        {
            InitializeComponent();
            this.Loaded += DSP_SOL_007_ソロ途中結果_大_Loaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSP_SOL_007_ソロ途中結果_大_Loaded(object sender, RoutedEventArgs e)
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
                    Step3(DV_Result);
                    break;
                case 3:
                    Step4();
                    break;

            }
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
            PartsLST001.LB_タイトル_減点.Visibility = Visibility.Collapsed;


            PartsLST001.IM_明細1.Visibility = Visibility.Collapsed;
            PartsLST001.IM_明細2.Visibility = Visibility.Collapsed;
            PartsLST001.IM_明細3.Visibility = Visibility.Collapsed;
            PartsLST001.IM_明細4.Visibility = Visibility.Collapsed;
            PartsLST001.IM_明細5.Visibility = Visibility.Collapsed;
            PartsLST001.IM_明細6.Visibility = Visibility.Collapsed;
            PartsLST001.IM_明細7.Visibility = Visibility.Collapsed;
            PartsLST001.IM_明細8.Visibility = Visibility.Collapsed;

            PartsLST001.LB_結果1_順位.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果1_背番号.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果1_選手名.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果1_所属.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果1_減点.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果1_得点.Visibility = Visibility.Collapsed;

            PartsLST001.LB_結果2_順位.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果2_背番号.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果2_選手名.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果2_所属.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果2_減点.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果2_得点.Visibility = Visibility.Collapsed;

            PartsLST001.LB_結果3_順位.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果3_背番号.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果3_選手名.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果3_所属.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果3_減点.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果3_得点.Visibility = Visibility.Collapsed;

            PartsLST001.LB_結果4_順位.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果4_背番号.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果4_選手名.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果4_所属.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果4_減点.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果4_得点.Visibility = Visibility.Collapsed;

            PartsLST001.LB_結果5_順位.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果5_背番号.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果5_選手名.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果5_所属.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果5_減点.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果5_得点.Visibility = Visibility.Collapsed;

            PartsLST001.LB_結果6_順位.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果6_背番号.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果6_選手名.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果6_所属.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果6_減点.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果6_得点.Visibility = Visibility.Collapsed;

            PartsLST001.LB_結果7_順位.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果7_背番号.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果7_選手名.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果7_所属.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果7_減点.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果7_得点.Visibility = Visibility.Collapsed;

            PartsLST001.LB_結果8_順位.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果8_背番号.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果8_選手名.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果8_所属.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果8_減点.Visibility = Visibility.Collapsed;
            PartsLST001.LB_結果8_得点.Visibility = Visibility.Collapsed;

        }

        /// <summary>
        /// Step1: 競技会名、区分名、ラウンド名、種目情報、選手情報を表示
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
        /// Step2: 画像とPCS名、選手情報をアニメーション表示
        /// </summary>
        public void Step2()
        {
            // PartsMainの初期化確認
            EnsurePartsMainInitialized();

            if (_partsMain == null) return;


            // 画像の初期状態を設定（フェードイン用に透明にする）
            PartsLST001.IM_タイトル1.Opacity = 0;
            PartsLST001.IM_タイトル2.Opacity = 0;
            PartsLST001.IM_タイトル3.Opacity = 0;

            // 画像のフェードインアニメーション
            // ① まず IM_種目1・IM_種目2 をフェードイン（beginTime=0: 即開始、800ms で完了）
            // ② その後（1000ms後）に IM_ソロ選手結果1～4 をフェードイン
            var imageStoryboard = new Storyboard();
            _partsMain.フェードイン(true, PartsLST001.IM_タイトル1, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsLST001.IM_タイトル2, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsLST001.IM_タイトル3, imageStoryboard, 0);


            // 画像フェードイン完了後に表示
            imageStoryboard.Completed += (s, e) =>
            {
                // 区分ラウンド名・種目名をヘルパーから取得して設定
                string 区分名 = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
                string ラウンド名 = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);
                PartsLST001.LB_タイトル1.Content = 区分名 + " " + ラウンド名;
                PartsLST001.LB_タイトル2.Content = DSDspDataHelper.Get種目名(DA_Master, 区分番号, ラウンド番号, 種目番号);

                PartsLST001.LB_タイトル3.Content = "途中経過";



                // 一気に表示
                PartsLST001.LB_タイトル1.Visibility = Visibility.Visible;
                PartsLST001.LB_タイトル2.Visibility = Visibility.Visible;
                PartsLST001.LB_タイトル3.Visibility = Visibility.Visible;
                PartsLST001.LB_タイトル_減点.Visibility = Visibility.Visible;
                PartsLST001.LB_タイトル_Total.Visibility = Visibility.Visible;



              
                // フォントサイズ自動調整  区分ラウンド名
                _partsMain?.フォントサイズ自動調整(
                    label: PartsLST001.LB_タイトル1,
                    text: PartsLST001.LB_タイトル1.Content.ToString(),
                    maxWidth: 300,
                    maxFontSize: 14,
                    minFontSize: 8,
                    fontFamilyName: FONT_FAMILY_NAME);

                // フォントサイズ自動調整  種目名
                _partsMain?.フォントサイズ自動調整(
                    label: PartsLST001.LB_タイトル2,
                    text: PartsLST001.LB_タイトル2.Content.ToString(),
                    maxWidth: 450,
                    maxFontSize: 16,
                    minFontSize: 8,
                    fontFamilyName: FONT_FAMILY_NAME);



                // フェードイン
                var playerStoryboard = new Storyboard();
                PartsLST001.LB_タイトル1.Opacity = 0;
                PartsLST001.LB_タイトル2.Opacity = 0;
                PartsLST001.LB_タイトル3.Opacity = 0;
                PartsLST001.LB_タイトル_減点.Opacity = 0;
                PartsLST001.LB_タイトル_Total.Opacity = 0;

                _partsMain?.フェードイン(true, PartsLST001.LB_タイトル1, playerStoryboard, 100);
                _partsMain?.フェードイン(true, PartsLST001.LB_タイトル2, playerStoryboard, 100);
                _partsMain?.フェードイン(true, PartsLST001.LB_タイトル3, playerStoryboard, 100);
                _partsMain?.フェードイン(true, PartsLST001.LB_タイトル_減点, playerStoryboard, 100);
                _partsMain?.フェードイン(true, PartsLST001.LB_タイトル_Total, playerStoryboard, 100);
                playerStoryboard.Begin();
            };

            imageStoryboard.Begin();

            // 画像のスライドアニメーション
            CreateAndStartSlideAnimation(PartsLST001.IM_タイトル1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsLST001.IM_タイトル2, SLIDE_FROM_LEFT);
            CreateAndStartSlideAnimation(PartsLST001.IM_タイトル3, SLIDE_FROM_RIGHT);

        }



        /// <summary>
        /// Step3: 途中結果一覧の表示
        /// DV_Resultから当該種目の結果を取得し、1位からヒート番号件数分を表示。
        /// IM_明細1～N を順にフェードイン後、結果ラベルを一斉フェードイン。
        /// 指定ヒート番号の出場選手は文字色を濃いオレンジにする。
        /// </summary>
        /// <param name="dvResult">DV_Result（種目結果データ）</param>
        public void Step3(JsonNode? dvResult)
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null || dvResult == null) return;

            // ---- 種目結果を取得 ----
            var 種目結果リスト = dvResult["種目結果"]?.AsArray();
            if (種目結果リスト == null) return;

            var 種目結果 = 種目結果リスト.FirstOrDefault(d => d?["種目順"]?.GetValue<int>() == 種目番号);
            if (種目結果 == null) return;

            var 選手結果リスト = 種目結果["選手結果"]?.AsArray();
            if (選手結果リスト == null) return;

            // 順位昇順で並べ、表示件数はヒート番号件（最大8）
            int 表示件数 = Math.Min(Math.Max(ヒート番号, 1), 8);
            var 表示対象 = 選手結果リスト
                .Where(p => p != null)
                .OrderBy(p => p!["種目順位番号"]?.GetValue<int>() ?? int.MaxValue)
                .Take(表示件数)
                .ToList();

            // ---- 指定ヒートの出場選手（背番号リスト）を取得 ----
            var 当該ヒート選手 = DSDspDataHelper.Get背番号リストFromHeat(
                DS_Status, 区分番号, ラウンド番号, 種目番号, ヒート番号);

            // ---- 表示対象行の Image / Label を配列化（1始まり→0始まりインデックス）----
            var 明細IM = new[]
            {
                PartsLST001.IM_明細1, PartsLST001.IM_明細2, PartsLST001.IM_明細3, PartsLST001.IM_明細4,
                PartsLST001.IM_明細5, PartsLST001.IM_明細6, PartsLST001.IM_明細7, PartsLST001.IM_明細8
            };
            var 順位LB = new[]
            {
                PartsLST001.LB_結果1_順位, PartsLST001.LB_結果2_順位, PartsLST001.LB_結果3_順位, PartsLST001.LB_結果4_順位,
                PartsLST001.LB_結果5_順位, PartsLST001.LB_結果6_順位, PartsLST001.LB_結果7_順位, PartsLST001.LB_結果8_順位
            };
            var 背番号LB = new[]
            {
                PartsLST001.LB_結果1_背番号, PartsLST001.LB_結果2_背番号, PartsLST001.LB_結果3_背番号, PartsLST001.LB_結果4_背番号,
                PartsLST001.LB_結果5_背番号, PartsLST001.LB_結果6_背番号, PartsLST001.LB_結果7_背番号, PartsLST001.LB_結果8_背番号
            };
            var 選手名LB = new[]
            {
                PartsLST001.LB_結果1_選手名, PartsLST001.LB_結果2_選手名, PartsLST001.LB_結果3_選手名, PartsLST001.LB_結果4_選手名,
                PartsLST001.LB_結果5_選手名, PartsLST001.LB_結果6_選手名, PartsLST001.LB_結果7_選手名, PartsLST001.LB_結果8_選手名
            };
            var 所属LB = new[]
            {
                PartsLST001.LB_結果1_所属, PartsLST001.LB_結果2_所属, PartsLST001.LB_結果3_所属, PartsLST001.LB_結果4_所属,
                PartsLST001.LB_結果5_所属, PartsLST001.LB_結果6_所属, PartsLST001.LB_結果7_所属, PartsLST001.LB_結果8_所属
            };
            var 減点LB = new[]
            {
                PartsLST001.LB_結果1_減点, PartsLST001.LB_結果2_減点, PartsLST001.LB_結果3_減点, PartsLST001.LB_結果4_減点,
                PartsLST001.LB_結果5_減点, PartsLST001.LB_結果6_減点, PartsLST001.LB_結果7_減点, PartsLST001.LB_結果8_減点
            };
            var 得点LB = new[]
            {
                PartsLST001.LB_結果1_得点, PartsLST001.LB_結果2_得点, PartsLST001.LB_結果3_得点, PartsLST001.LB_結果4_得点,
                PartsLST001.LB_結果5_得点, PartsLST001.LB_結果6_得点, PartsLST001.LB_結果7_得点, PartsLST001.LB_結果8_得点
            };

            // ---- ラベルにデータをセット（非表示のまま）----
            for (int i = 0; i < 表示件数; i++)
            {
                var p = 表示対象[i];
                string 背番号 = p?["背番号"]?.ToString() ?? "";
                string 順位表記 = p?["種目順位表記"]?.ToString() ?? "";
                double 得点 = p?["種目得点"]?.GetValue<double>() ?? 0;
                bool 失格 = p?["失格FLAG"]?.ToString() == "1";

                // 一般減点合計
                double 減点合計 = 0;
                if (!失格)
                {
                    var 一般減点Array = p?["一般減点"]?.AsArray();
                    if (一般減点Array != null)
                        foreach (var r in 一般減点Array)
                            減点合計 += r?["減点値"]?.GetValue<double>() ?? 0;
                }

                // 選手情報
                var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, 背番号);
                string 選手名L = DSDspDataHelper.Get選手名L(選手情報);
                string 選手名P = DSDspDataHelper.Get選手名P(選手情報);
                string 選手名表示 = string.IsNullOrEmpty(選手名P)
                    ? 選手名L
                    : 選手名L + "・" + 選手名P;
                string 所属 = DSDspDataHelper.Get所属(選手情報);

                // 当該ヒート出場選手なら濃いオレンジ、それ以外はDarkBlue
                var 前景色 = 当該ヒート選手.Contains(背番号)
                    ? new SolidColorBrush(Colors.DarkOrange)
                    : new SolidColorBrush(Colors.DarkBlue);

                // 順位ラベル（LB_結果1_xxx が 1位）
                順位LB[i].Content = 失格 ? "失格" : 順位表記;
                背番号LB[i].Content = 背番号;
                選手名LB[i].Content = 選手名表示;
                所属LB[i].Content = 所属;
                減点LB[i].Content = 失格 ? "" : (減点合計 == 0 ? "" : 減点合計.ToString("F1"));
                得点LB[i].Content = 失格 ? "失格" : 得点.ToString("F3");

                順位LB[i].Foreground = 前景色;
                背番号LB[i].Foreground = 前景色;
                選手名LB[i].Foreground = 前景色;
                所属LB[i].Foreground = 前景色;
                減点LB[i].Foreground = 前景色;
                得点LB[i].Foreground = 前景色;

                // Visibility は Collapsed のまま（フェードイン時に Visible にする）
                順位LB[i].Opacity = 0;
                背番号LB[i].Opacity = 0;
                選手名LB[i].Opacity = 0;
                所属LB[i].Opacity = 0;
                減点LB[i].Opacity = 0;
                得点LB[i].Opacity = 0;
            }

            // ---- フォントサイズ自動調整（選手名）----
            for (int i = 0; i < 表示件数; i++)
            {
                _partsMain.フォントサイズ自動調整(
                    label: 選手名LB[i],
                    text: 選手名LB[i].Content?.ToString() ?? "",
                    maxWidth: 148,
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
                明細IM[i].Opacity = 0;
                _partsMain.フェードイン(true, 明細IM[i], 明細Storyboard, i * 間隔ms);
            }

            // 明細フェードイン完了後、結果ラベルを一斉フェードイン
            明細Storyboard.Completed += (s, e) =>
            {
                var 結果Storyboard = new Storyboard();
                for (int i = 0; i < 表示件数; i++)
                {
                    _partsMain?.フェードイン(true, 順位LB[i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, 背番号LB[i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, 選手名LB[i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, 所属LB[i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, 減点LB[i], 結果Storyboard, 0);
                    _partsMain?.フェードイン(true, 得点LB[i], 結果Storyboard, 0);
                }
                結果Storyboard.Begin();
            };

            明細Storyboard.Begin();
        }

        /// <summary>
        /// Step4: ラベルと画像をフェードアウト
        /// TODO: Step3実装後にLST001の表示要素をフェードアウトするよう実装する
        /// </summary>
        public void Step4()
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

            fadeOutStoryboard.Begin();
        }


        #endregion
    }
}

