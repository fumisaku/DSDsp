using System;
using System.Windows;
using System.Windows.Controls;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_MSG_001_メッセージ.xaml の相互作用ロジック
    ///
    /// 進行タブの割り込みメッセージ表示専用の軽量画面。
    ///
    /// プリセット:
    ///   MessageType = Interval  → 「インターバル」
    ///   MessageType = Adjusting → 「調整中　お待ちください」
    ///   MessageType = Custom    → Message プロパティの文字列
    ///
    /// ステップ:
    ///   STEP1 (case 0): メッセージパネルを表示。
    ///   STEP2 (case 1): パネルを非表示 → RaiseScreenCompleted()
    /// </summary>
    public partial class DSP_MSG_001_メッセージ : DSDspScreenBase
    {
        #region 定数
        public const string TYPE_INTERVAL  = "Interval";
        public const string TYPE_ADJUSTING = "Adjusting";
        public const string TYPE_CUSTOM    = "Custom";
        #endregion

        #region プロパティ
        /// <summary>メッセージタイプ。"Interval" / "Adjusting" / "Custom"</summary>
        public string MessageType { get; set; } = TYPE_INTERVAL;

        /// <summary>MessageType = "Custom" のときに表示するメッセージ本文</summary>
        public string Message { get; set; } = string.Empty;

        protected override int TotalSteps => 2;
        #endregion

        #region コンストラクタ
        public DSP_MSG_001_メッセージ()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }
        #endregion

        #region イベントハンドラ
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsurePartsMainInitialized();
            // ShowMessage() が既に呼ばれている場合は Collapsed に戻さない
            if (_currentStep == 0)
            {
                MessagePanel.Visibility = Visibility.Collapsed;
                TxtMessageLine2.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region オーバーライドメソッド
        protected override void ExecuteCurrentStep()
        {
            switch (_currentStep)
            {
                case 0:
                    ShowMessage();
                    break;
                case 1:
                    HideMessage();
                    break;
            }
        }
        #endregion

        #region ステップ実装

        private void ShowMessage()
        {
            // COM001: 競技会名
            if (PartsCOM001.FindName("TB_左上1") is TextBlock tb1)
                tb1.Text = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;
            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
                tb2.Text = string.Empty;

            // COM002: 現在時刻
            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
                lbRight.Content = $"現在時刻　{DateTime.Now:HH:mm}";
            StartClock();

            // メッセージ内容を設定
            var (line1, line2) = ResolveMessage();
            TxtMessageLine1.Text = line1;

            if (!string.IsNullOrEmpty(line2))
            {
                TxtMessageLine2.Text = line2;
                TxtMessageLine2.Visibility = Visibility.Visible;
            }
            else
            {
                TxtMessageLine2.Visibility = Visibility.Collapsed;
            }

            MessagePanel.Visibility = Visibility.Visible;
        }

        private void HideMessage()
        {
            MessagePanel.Visibility = Visibility.Collapsed;
            RaiseScreenCompleted();
        }

        #endregion

        #region ヘルパー

        private (string line1, string line2) ResolveMessage()
        {
            return MessageType switch
            {
                TYPE_INTERVAL  => ("インターバル", string.Empty),
                TYPE_ADJUSTING => ("調整中", "お待ちください"),
                TYPE_CUSTOM    => ParseCustomMessage(Message),
                _ => (Message, string.Empty)
            };
        }

        /// <summary>
        /// カスタムメッセージを2行に分割する。
        /// 「\n」または「\\n」で区切りが指定されている場合はそこで分割する。
        /// 指定がない場合は1行目にすべて表示する。
        /// </summary>
        private static (string, string) ParseCustomMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return (string.Empty, string.Empty);

            // \n リテラルまたは実際の改行で分割
            var idx = msg.IndexOf('\n');
            if (idx < 0) idx = msg.IndexOf("\\n", System.StringComparison.Ordinal);

            if (idx > 0)
                return (msg[..idx].Trim(), msg[(idx + 1)..].Trim().TrimStart('\\').TrimStart('n'));

            return (msg, string.Empty);
        }

        #endregion
    }
}
