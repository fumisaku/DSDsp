using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DSDsp.画面
{
    /// <summary>
    /// HET003_出場選手紹介.xaml の相互作用ロジック
    /// </summary>
    public partial class HET003_出場選手紹介 : UserControl
    {
        private DispatcherTimer _timer;

        public HET003_出場選手紹介()
        {
            InitializeComponent();
            
            // 現在時刻を更新するタイマーを設定
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // 右上02パーツのLabelを更新
            if (PartsCOM003.FindName("LB_右上") is Label lb)
            {
                lb.Content = $"現在時刻　{DateTime.Now:HH:mm}";
            }
        }

        /// <summary>
        /// 画面データを設定する
        /// </summary>
        /// <param name="competitionName">競技会名</param>
        /// <param name="divisionRound">区分名・ラウンド名</param>
        /// <param name="eventName">種目名</param>
        /// <param name="heatInfo">ヒート情報（例：ワルツ　1H）</param>
        public void SetData(string competitionName, string divisionRound, string eventName, string heatInfo)
        {
            // 左上パーツのTextBlockにアクセスして設定
            if (PartsCOM001.FindName("TB_左上1") is TextBlock tb1)
            {
                tb1.Text = competitionName;
            }
            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
            {
                tb2.Text = divisionRound;
            }

            // 右上01パーツのLabelにアクセスして設定
            if (PartsCOM002.FindName("LB_右上") is Label lb1)
            {
                lb1.Content = eventName;
            }

            // 一覧リスト大パーツのタイトルを設定
            if (PartsLST001.FindName("LB_タイトル1") is Label lbTitle1)
            {
                lbTitle1.Content = divisionRound;
            }
            if (PartsLST001.FindName("LB_タイトル2") is Label lbTitle2)
            {
                lbTitle2.Content = heatInfo;
            }
            if (PartsLST001.FindName("LB_タイトル3") is Label lbTitle3)
            {
                lbTitle3.Content = "出場選手";
            }
        }

        /// <summary>
        /// 出場選手リストを設定する
        /// </summary>
        /// <param name="players">選手情報のリスト</param>
        public void SetPlayerList(List<PlayerInfo> players)
        {
            // 減点・得点列を非表示
            if (PartsLST001.FindName("LB_タイトル_減点") is Label lbTitleDeduction)
            {
                lbTitleDeduction.Visibility = System.Windows.Visibility.Collapsed;
            }
            if (PartsLST001.FindName("LB_タイトル_Total") is Label lbTitleTotal)
            {
                lbTitleTotal.Visibility = System.Windows.Visibility.Collapsed;
            }

            // 選手情報を設定（最大8組まで）
            for (int i = 0; i < players.Count && i < 8; i++)
            {
                int index = i + 1;
                var player = players[i];

                // 順位は非表示（出場選手紹介では順位は不要）
                if (PartsLST001.FindName($"LB_結果{index}_順位") is Label lbRank)
                {
                    lbRank.Visibility = System.Windows.Visibility.Collapsed;
                }

                // 背番号
                if (PartsLST001.FindName($"LB_結果{index}_背番号") is Label lbNumber)
                {
                    lbNumber.Content = player.Number;
                }

                // 選手名
                if (PartsLST001.FindName($"LB_結果{index}_選手名") is Label lbName)
                {
                    lbName.Content = player.Name;
                }

                // 所属
                if (PartsLST001.FindName($"LB_結果{index}_所属") is Label lbAffiliation)
                {
                    lbAffiliation.Content = player.Affiliation;
                }

                // 減点・得点を非表示
                if (PartsLST001.FindName($"LB_結果{index}_減点") is Label lbRowDeduction)
                {
                    lbRowDeduction.Visibility = System.Windows.Visibility.Collapsed;
                }
                if (PartsLST001.FindName($"LB_結果{index}_得点") is Label lbRowScore)
                {
                    lbRowScore.Visibility = System.Windows.Visibility.Collapsed;
                }
            }

            // 使用しない行を非表示
            for (int i = players.Count; i < 8; i++)
            {
                int index = i + 1;
                if (PartsLST001.FindName($"IM_明細{index}") is System.Windows.Controls.Image img)
                {
                    img.Visibility = System.Windows.Visibility.Collapsed;
                }
                if (PartsLST001.FindName($"LB_結果{index}_順位") is Label lbRank)
                {
                    lbRank.Visibility = System.Windows.Visibility.Collapsed;
                }
                if (PartsLST001.FindName($"LB_結果{index}_背番号") is Label lbNumber)
                {
                    lbNumber.Visibility = System.Windows.Visibility.Collapsed;
                }
                if (PartsLST001.FindName($"LB_結果{index}_選手名") is Label lbName)
                {
                    lbName.Visibility = System.Windows.Visibility.Collapsed;
                }
                if (PartsLST001.FindName($"LB_結果{index}_所属") is Label lbAffiliation)
                {
                    lbAffiliation.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }
    }

    /// <summary>
    /// 選手情報クラス
    /// </summary>
    public class PlayerInfo
    {
        public string Number { get; set; } = "";
        public string Name { get; set; } = "";
        public string Affiliation { get; set; } = "";
    }
}

// Made with Bob
