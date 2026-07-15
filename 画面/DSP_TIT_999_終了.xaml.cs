using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_TIT_999_終了.xaml の相互作用ロジック
    /// 画面進行一覧の最後に固定で追加される終了画面。
    /// ステップ構成: Step0（ヘッダ設定 + 「終了」フェードイン）の1ステップのみ。
    /// </summary>
    public partial class DSP_TIT_999_終了 : DSDspScreenBase
    {
        #region プロパティ
        /// <summary>総ステップ数（Step0 の1ステップのみ）</summary>
        protected override int TotalSteps => 1;
        #endregion

        #region コンストラクタ
        public DSP_TIT_999_終了()
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
            }
        }
        #endregion

        #region ステップ実装

        /// <summary>
        /// Step1: ヘッダ設定 + 「終了」テキストをフェードインで表示する。
        /// </summary>
        public void Step1()
        {
            EnsurePartsMainInitialized();

            // ヘッダ設定
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(
                new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
            PartsCOM001.TB_左上1.Text = DSDspDataHelper.Get競技会名(DA_Master);
            PartsCOM001.TB_左上2.Text = DSDspDataHelper.Get区分名(DA_Master, 区分番号)
                                      + "　" + DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);

            // 「終了」テキストをフェードインで表示
            var sb = new Storyboard();
            var fadeIn = new DoubleAnimation
            {
                From     = 0,
                To       = 1,
                Duration = System.TimeSpan.FromSeconds(1),
            };
            Storyboard.SetTarget(fadeIn, LB_終了);
            Storyboard.SetTargetProperty(fadeIn, new System.Windows.PropertyPath("Opacity"));
            sb.Children.Add(fadeIn);
            sb.Begin();
        }

        #endregion
    }
}

// Made with Bob
