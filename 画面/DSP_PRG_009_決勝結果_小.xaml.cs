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
    /// DSP_PRG_009_決勝結果_小.xaml の相互作用ロジック
    /// </summary>
    public partial class DSP_PRG_009_決勝結果_小 : DSDspScreenBase
    {
        #region プロパティ
        protected override int TotalSteps => 1;
        #endregion

        #region コンストラクタ
        public DSP_PRG_009_決勝結果_小()
        {
            InitializeComponent();
            this.Loaded += DSP_PRG_009_決勝結果_小_Loaded;
        }
        #endregion

        #region イベントハンドラ
        private void DSP_PRG_009_決勝結果_小_Loaded(object sender, RoutedEventArgs e)
        {
            EnsurePartsMainInitialized();
        }
        #endregion

        #region オーバーライドメソッド
        protected override void ExecuteCurrentStep()
        {
            // TODO: 表示ロジックは別途実装
        }
        #endregion
    }
}
