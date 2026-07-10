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
    public partial class DSP_SOL_003_ソロ選手結果GD_大 : DSDspScreenBase
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
        #endregion

        #region プロパティ
        /// <summary>
        /// 総ステップ数（Step0, Step1, Step2, Step3, Step4の5ステップ）
        /// </summary>
        protected override int TotalSteps => 5;
        #endregion

        #region コンストラクタ
        public DSP_SOL_003_ソロ選手結果GD_大()
        {
            InitializeComponent();
            this.Loaded += DSP_SOL_003_ソロ選手結果GD_大_Loaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSP_SOL_003_ソロ選手結果GD_大_Loaded(object sender, RoutedEventArgs e)
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
         
            // TIT004 の文字と画像を非表示
            PartsSOL001.LB_背番号.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_選手名L.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_選手名P.Visibility = Visibility.Collapsed;
            PartsSOL001.IM_種目1.Visibility = Visibility.Collapsed;
            PartsSOL001.IM_種目2.Visibility = Visibility.Collapsed;

            //結果部分
            PartsSOL001.IM_ソロ選手結果1.Visibility = Visibility.Collapsed;
            PartsSOL001.IM_ソロ選手結果2.Visibility = Visibility.Collapsed;
            PartsSOL001.IM_ソロ選手結果3.Visibility = Visibility.Collapsed;

            //結果 得点部分
            PartsSOL001.LB_PCS名1.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_PCS名2.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_PCS名3.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_PCS名4.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_Red名.Visibility = Visibility.Collapsed;

            PartsSOL001.LB_PCS1.Content = "";
            PartsSOL001.LB_PCS2.Content = "";
            PartsSOL001.LB_PCS3.Content = "";
            PartsSOL001.LB_PCS4.Content = "";
            PartsSOL001.LB_Red.Content = "";

            PartsSOL001.LB_PCS1.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_PCS2.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_PCS3.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_PCS4.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_Red.Visibility = Visibility.Collapsed;

            PartsSOL001.LB_Total名.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_Total.Content = "";
            PartsSOL001.LB_Total.Visibility = Visibility.Collapsed;

            PartsSOL001.LB_Rank名.Visibility = Visibility.Collapsed;
            PartsSOL001.LB_Rank.Content = "";
            PartsSOL001.LB_Rank.Visibility = Visibility.Collapsed;




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

            // PartsCOM003に選手情報を表示
            PartsCOM003.LB_右上.Content = ヒート番号.ToString() + "組目　" + _背番号 + "　" + _選手名L + "・" + _選手名P;
        }

        /// <summary>
        /// Step2: 画像とPCS名、選手情報をアニメーション表示
        /// </summary>
        public void Step2()
        {
            // PartsMainの初期化確認
            EnsurePartsMainInitialized();

            if (_partsMain == null) return;

            // 画像とタイトルを表示状態に設定
            PartsSOL001.LB_背番号.Content = "";
            PartsSOL001.LB_選手名L.Content = "";
            PartsSOL001.LB_選手名P.Content = "";

            PartsSOL001.LB_背番号.Visibility = Visibility.Visible;
            PartsSOL001.LB_選手名L.Visibility = Visibility.Visible;
            PartsSOL001.LB_選手名P.Visibility = Visibility.Visible;

            // 画像の初期状態を設定（フェードイン用に透明にする）
            PartsSOL001.IM_種目1.Opacity = 0;
            PartsSOL001.IM_種目2.Opacity = 0;
            PartsSOL001.IM_ソロ選手結果1.Opacity = 0;
            PartsSOL001.IM_ソロ選手結果2.Opacity = 0;
            PartsSOL001.IM_ソロ選手結果3.Opacity = 0;

            // 画像のフェードインアニメーション
            // ① まず IM_種目1・IM_種目2 をフェードイン（beginTime=0: 即開始、800ms で完了）
            // ② その後（1000ms後）に IM_ソロ選手結果1～3 をフェードイン
            var imageStoryboard = new Storyboard();
            _partsMain.フェードイン(true, PartsSOL001.IM_種目1, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsSOL001.IM_種目2, imageStoryboard, 0);
            _partsMain.フェードイン(true, PartsSOL001.IM_ソロ選手結果1, imageStoryboard, 1000);
            _partsMain.フェードイン(true, PartsSOL001.IM_ソロ選手結果2, imageStoryboard, 1000);
            _partsMain.フェードイン(true, PartsSOL001.IM_ソロ選手結果3, imageStoryboard, 1000);

            // 画像フェードイン完了後にPCS名を表示
            imageStoryboard.Completed += (s, e) =>
            {
                // PCS名を取得して表示
                SetPCSNames();

                // PCS名、Red、Total名、Rank名を一気に表示
                PartsSOL001.LB_PCS名1.Visibility = Visibility.Visible;
                PartsSOL001.LB_PCS名2.Visibility = Visibility.Visible;
                PartsSOL001.LB_PCS名3.Visibility = Visibility.Visible;
                PartsSOL001.LB_PCS名4.Visibility = Visibility.Visible;
                PartsSOL001.LB_Red名.Content = "Red";
                PartsSOL001.LB_Red名.Visibility = Visibility.Visible;
                PartsSOL001.LB_Total名.Content = "Total";
                PartsSOL001.LB_Total名.Visibility = Visibility.Visible;
                PartsSOL001.LB_Rank名.Content = "Rank";
                PartsSOL001.LB_Rank名.Visibility = Visibility.Visible;

                // 選手情報をフェードイン
                PartsSOL001.LB_背番号.Content = _背番号;
                PartsSOL001.LB_選手名L.Content = _選手名L;
                PartsSOL001.LB_選手名P.Content = _選手名P;

                // フォントサイズ自動調整
                _partsMain?.フォントサイズ自動調整(
                    label: PartsSOL001.LB_選手名L,
                    text: _選手名L,
                    maxWidth: 400,
                    maxFontSize: 22,
                    minFontSize: 20,
                    fontFamilyName: FONT_FAMILY_NAME);

                _partsMain?.フォントサイズ自動調整(
                    label: PartsSOL001.LB_選手名P,
                    text: _選手名P,
                    maxWidth: 400,
                    maxFontSize: 22,
                    minFontSize: 20,
                    fontFamilyName: FONT_FAMILY_NAME);

                // 選手情報のフェードイン
                var playerStoryboard = new Storyboard();
                PartsSOL001.LB_背番号.Opacity = 0;
                PartsSOL001.LB_選手名L.Opacity = 0;
                PartsSOL001.LB_選手名P.Opacity = 0;
                _partsMain?.フェードイン(true, PartsSOL001.LB_背番号, playerStoryboard, 100);
                _partsMain?.フェードイン(true, PartsSOL001.LB_選手名L, playerStoryboard, 100);
                _partsMain?.フェードイン(true, PartsSOL001.LB_選手名P, playerStoryboard, 100);
                playerStoryboard.Begin();
            };

            imageStoryboard.Begin();

            // 画像のスライドアニメーション
            CreateAndStartSlideAnimation(PartsSOL001.IM_種目1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsSOL001.IM_種目2, SLIDE_FROM_LEFT);
        }

        /// <summary>
        /// PCS名を取得して設定
        /// </summary>
        private void SetPCSNames()
        {
            if (DA_Master == null || string.IsNullOrEmpty(_採点方式ID))
            {
                PartsSOL001.LB_PCS名1.Content = "PCS1";
                PartsSOL001.LB_PCS名2.Content = "PCS2";
                PartsSOL001.LB_PCS名3.Content = "PCS3";
                PartsSOL001.LB_PCS名4.Content = "PCS4";
                return;
            }

            // DJ_採点方式リストから該当の採点方式を取得
            var 採点方式リスト = DA_Master["DJ_採点方式リスト"]?.AsArray();
            if (採点方式リスト != null)
            {
                var 採点方式 = 採点方式リスト.FirstOrDefault(s => s?["採点方式ID"]?.ToString() == _採点方式ID);
                if (採点方式 != null)
                {
                    var pcs設定 = 採点方式["PCS設定"]?.AsArray();
                    if (pcs設定 != null)
                    {
                        // PCS1-4の名前を設定
                        for (int i = 0; i < 4 && i < pcs設定.Count; i++)
                        {
                            var pcs = pcs設定[i];
                            string pcs名 = pcs?["PCS名"]?.ToString() ?? $"PCS{i + 1}";
                            
                            switch (i)
                            {
                                case 0:
                                    PartsSOL001.LB_PCS名1.Content = pcs名;
                                    break;
                                case 1:
                                    PartsSOL001.LB_PCS名2.Content = pcs名;
                                    break;
                                case 2:
                                    PartsSOL001.LB_PCS名3.Content = pcs名;
                                    break;
                                case 3:
                                    PartsSOL001.LB_PCS名4.Content = pcs名;
                                    break;
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Step3: 結果の表示
        /// DV_Resultから結果を取得して表示
        /// </summary>
        /// <param name="dvResult">DV_Result（種目結果データ）</param>
        public void Step3(JsonNode? dvResult)
        {
            EnsurePartsMainInitialized();

            if (_partsMain == null || dvResult == null) return;

            // 種目結果から該当の種目を取得
            var 種目結果リスト = dvResult["種目結果"]?.AsArray();
            if (種目結果リスト == null) return;

            var 種目結果 = 種目結果リスト.FirstOrDefault(d => d?["種目順"]?.GetValue<int>() == 種目番号);
            if (種目結果 == null) return;

            // 選手結果から該当の背番号の選手を取得
            var 選手結果リスト = 種目結果["選手結果"]?.AsArray();
            if (選手結果リスト == null) return;

            var 選手結果 = 選手結果リスト.FirstOrDefault(p => p?["背番号"]?.ToString() == _背番号);
            if (選手結果 == null) return;

            // PCS得点を取得
            var pcsArray = 選手結果["PCS"]?.AsArray();
            double pcs1 = 0, pcs2 = 0, pcs3 = 0, pcs4 = 0;
            if (pcsArray != null && pcsArray.Count >= 4)
            {
                pcs1 = pcsArray[0]?["PCS得点"]?.GetValue<double>() ?? 0;
                pcs2 = pcsArray[1]?["PCS得点"]?.GetValue<double>() ?? 0;
                pcs3 = pcsArray[2]?["PCS得点"]?.GetValue<double>() ?? 0;
                pcs4 = pcsArray[3]?["PCS得点"]?.GetValue<double>() ?? 0;
            }

            // 一般減点を取得
            double 減点合計 = 0;
            bool 失格 = 選手結果["失格FLAG"]?.ToString() == "1";
            
            if (!失格)
            {
                var 一般減点Array = 選手結果["一般減点"]?.AsArray();
                if (一般減点Array != null)
                {
                    foreach (var 減点 in 一般減点Array)
                    {
                        減点合計 += 減点?["減点値"]?.GetValue<double>() ?? 0;
                    }
                }
            }

            // 種目得点と順位を取得
            double 種目得点 = 選手結果["種目得点"]?.GetValue<double>() ?? 0;
            string 種目順位表記 = 選手結果["種目順位表記"]?.ToString() ?? "";

            // PCS得点をフェードイン表示
            PartsSOL001.LB_PCS1.Content = pcs1.ToString("F2");
            PartsSOL001.LB_PCS2.Content = pcs2.ToString("F2");
            PartsSOL001.LB_PCS3.Content = pcs3.ToString("F2");
            PartsSOL001.LB_PCS4.Content = pcs4.ToString("F2");

            // 減点表示
            if (失格)
            {
                PartsSOL001.LB_Red.Content = "失格";
            }
            else
            {
                PartsSOL001.LB_Red.Content = 減点合計.ToString("F1");
            }

            // PCS得点と減点をフェードイン
            var pcsStoryboard = new Storyboard();
            PartsSOL001.LB_PCS1.Opacity = 0;
            PartsSOL001.LB_PCS2.Opacity = 0;
            PartsSOL001.LB_PCS3.Opacity = 0;
            PartsSOL001.LB_PCS4.Opacity = 0;
            PartsSOL001.LB_Red.Opacity = 0;
            _partsMain.フェードイン(true, PartsSOL001.LB_PCS1, pcsStoryboard, 0);
            _partsMain.フェードイン(true, PartsSOL001.LB_PCS2, pcsStoryboard, 0);
            _partsMain.フェードイン(true, PartsSOL001.LB_PCS3, pcsStoryboard, 0);
            _partsMain.フェードイン(true, PartsSOL001.LB_PCS4, pcsStoryboard, 0);
            _partsMain.フェードイン(true, PartsSOL001.LB_Red, pcsStoryboard, 0);

            // PCS表示完了後、1秒後にTotal表示
            pcsStoryboard.Completed += (s, e) =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.1) };
                timer.Tick += (sender, args) =>
                {
                    timer.Stop();
                    
                    // Total得点を表示
                    PartsSOL001.LB_Total.Content = 種目得点.ToString("F3");
                    
                    var totalStoryboard = new Storyboard();
                    PartsSOL001.LB_Total.Opacity = 0;
                    _partsMain?.フェードイン(true, PartsSOL001.LB_Total, totalStoryboard, 0);
                    
                    // Total表示完了後、さらに1秒後にRank表示
                    totalStoryboard.Completed += (s2, e2) =>
                    {
                        var rankTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.1) };
                        rankTimer.Tick += (sender2, args2) =>
                        {
                            rankTimer.Stop();
                            
                            // Rank順位を表示
                            PartsSOL001.LB_Rank.Content = 種目順位表記;
                            
                            var rankStoryboard = new Storyboard();
                            PartsSOL001.LB_Rank.Opacity = 0;
                            _partsMain?.フェードイン(true, PartsSOL001.LB_Rank, rankStoryboard, 0);
                            rankStoryboard.Begin();
                        };
                        rankTimer.Start();
                    };
                    
                    totalStoryboard.Begin();
                };
                timer.Start();
            };

            pcsStoryboard.Begin();
        }

        /// <summary>
        /// Step4: ラベルと画像をフェードアウト
        /// </summary>
        public void Step4()
        {
            EnsurePartsMainInitialized();

            if (_partsMain == null) return;

            var fadeOutStoryboard = new Storyboard();
            
            // Step2とStep3で表示したものをフェードアウト
            _partsMain.フェードアウト(true, PartsSOL001.LB_背番号, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_選手名L, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_選手名P, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.IM_種目1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.IM_種目2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.IM_ソロ選手結果1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.IM_ソロ選手結果2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.IM_ソロ選手結果3, fadeOutStoryboard, 0);
            
            // PCS名
            _partsMain.フェードアウト(true, PartsSOL001.LB_PCS名1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_PCS名2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_PCS名3, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_PCS名4, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_Red名, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_Total名, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_Rank名, fadeOutStoryboard, 0);
            
            // PCS得点
            _partsMain.フェードアウト(true, PartsSOL001.LB_PCS1, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_PCS2, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_PCS3, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_PCS4, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_Red, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_Total, fadeOutStoryboard, 0);
            _partsMain.フェードアウト(true, PartsSOL001.LB_Rank, fadeOutStoryboard, 0);
            
            fadeOutStoryboard.Begin();
        }


        #endregion
    }
}

