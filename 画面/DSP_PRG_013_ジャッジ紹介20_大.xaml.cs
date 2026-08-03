using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_PRG_013_ジャッジ紹介20_大.xaml の相互作用ロジック
    ///
    /// ジャッジ紹介（全画面・大）11人以上用。1ページ20人表示。
    /// 使用パーツ: COM001, COM002, COM003, LST007
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): COM001/COM002/COM003 を設定、ジャッジリストを構築。ラベルをクリア。
    ///   STEP2 (case 1): IM_タイトル1-3、LB_タイトル1-3 を表示
    ///   STEP3 (case 2, 4, ...): LB_タイトル4 と IM_明細N/LB_結果N_* をフェードイン（ページ毎）
    ///   STEP4 (case 3, 5, ...): STEP3 で表示したものをフェードアウト
    ///   STEP5: STEP2 で表示したものを非表示 → RaiseScreenCompleted()
    ///
    /// タイトル4: 20人以下の場合はブランク。21人以上の場合は「ページ/全ページ」を表示。
    /// LST007 は所属カラムがないため、背番号・選手名のみ表示。
    /// </summary>
    public partial class DSP_PRG_013_ジャッジ紹介20_大 : DSDspScreenBase
    {
        #region 定数
        private const int MaxRows = 20;
        private const string FontFamilyName = "Segoe UI Semibold";
        #endregion

        #region フィールド
        private bool _step2Visible = false;
        private bool _closeRequested = false;
        private List<(string JdgCd, string JdgDispName)> _judgeList = new();
        private int _pageCount = 1;
        #endregion

        #region プロパティ
        protected override int TotalSteps => 100;

        /// <summary>
        /// 表示対象のジャッジグループID。null または空の場合は全員表示。
        /// </summary>
        public string? JudgeGroupId { get; set; }
        #endregion

        #region コンストラクタ
        public DSP_PRG_013_ジャッジ紹介20_大()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }
        #endregion

        #region イベントハンドラ
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsurePartsMainInitialized();
            HideAllParts();
        }
        #endregion

        #region オーバーライドメソッド
        protected override void ExecuteCurrentStep()
        {
            int s = _currentStep;

            if (s == 0) { Step1(); return; }
            if (s == 1) { Step2(); return; }

            int rel = s - 2;
            if (rel % 2 == 0)
            {
                int pageIdx = rel / 2;
                if (_closeRequested || pageIdx >= _pageCount)
                    Step5();
                else
                    Step3(pageIdx);
            }
            else
            {
                Step4();
            }
        }
        #endregion

        #region ステップ実装

        /// <summary>STEP1: COM001/COM002/COM003 を設定し、ジャッジリストを構築。ラベルをクリア。</summary>
        private void Step1()
        {
            EnsurePartsMainInitialized();

            // Loaded より先に Advance() が呼ばれた場合に備えて全パーツを非表示にする
            HideAllParts();

            // COM001: JDSFマーク + 競技会名 + 左上02はブランク
            if (PartsCOM001.FindName("IM_JDSFマーク") is System.Windows.Controls.Image im)
                im.Source = new BitmapImage(
                    new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
            if (PartsCOM001.FindName("TB_左上1") is System.Windows.Controls.TextBlock tb1)
                tb1.Text = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;
            if (PartsCOM001.FindName("TB_左上2") is System.Windows.Controls.TextBlock tb2)
                tb2.Text = string.Empty;

            // COM002: ジャッジ紹介
            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
                lbRight.Content = "ジャッジ紹介";
            StartClock();

            // COM003: ブランク
            if (PartsCOM003.FindName("LB_右上") is Label lb003)
                lb003.Content = string.Empty;

            BuildJudgeList();
        }

        /// <summary>STEP2: タイトル1-3 を表示</summary>
        private void Step2()
        {
            EnsurePartsMainInitialized();
            var p = PartsLST007;

            // タイトル1: 競技会名
            string title1 = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;
            SetLabelContent(p, "LB_タイトル1", title1);
            if (p.FindName("LB_タイトル1") is Label lbT1)
                _partsMain?.フォントサイズ自動調整(lbT1, title1, 310, 12, 7, FontFamilyName);
            SetVisible(p, "IM_タイトル1", true);
            SetVisible(p, "LB_タイトル1", true);

            // タイトル2: ジャッジ紹介
            SetLabelContent(p, "LB_タイトル2", "ジャッジ紹介");
            SetVisible(p, "IM_タイトル2", true);
            SetVisible(p, "LB_タイトル2", true);

            // タイトル3: ジャッジ
            SetLabelContent(p, "LB_タイトル3", "ジャッジ");
            SetVisible(p, "IM_タイトル3", true);
            SetVisible(p, "LB_タイトル3", true);

            _step2Visible = true;
        }

        /// <summary>STEP3: 指定ページのジャッジ明細をフェードインで表示</summary>
        private void Step3(int pageIdx)
        {
            EnsurePartsMainInitialized();
            var p = PartsLST007;
            int startIdx = pageIdx * MaxRows;
            int endIdx   = Math.Min(startIdx + MaxRows, _judgeList.Count);

            // タイトル4: 20人以下はブランク、21人以上はページ/全ページ
            if (_pageCount > 1)
            {
                SetLabelContent(p, "LB_タイトル4", $"{pageIdx + 1}/{_pageCount}");
                SetVisible(p, "LB_タイトル4", true);
            }
            else
            {
                SetLabelContent(p, "LB_タイトル4", string.Empty);
                SetVisible(p, "LB_タイトル4", false);
            }

            for (int row = 1; row <= MaxRows; row++)
            {
                int idx = startIdx + row - 1;
                if (idx < _judgeList.Count)
                {
                    var j = _judgeList[idx];
                    SetLabelContent(p, $"LB_結果{row}_背番号", j.JdgCd);
                    SetLabelContent(p, $"LB_結果{row}_選手名", j.JdgDispName);
                    if (p.FindName($"LB_結果{row}_選手名") is Label lb選手名)
                        _partsMain?.フォントサイズ自動調整(lb選手名, j.JdgDispName, 170, 14, 7, FontFamilyName);
                }
                else
                {
                    SetLabelContent(p, $"LB_結果{row}_背番号", string.Empty);
                    SetLabelContent(p, $"LB_結果{row}_選手名", string.Empty);
                }
            }

            var imNames = new List<string>();
            var lbNames = new List<string>();
            for (int row = 1; row <= MaxRows; row++)
            {
                int idx = startIdx + row - 1;
                if (idx < _judgeList.Count)
                {
                    imNames.Add($"IM_明細{row}");
                    lbNames.Add($"LB_結果{row}_背番号");
                    lbNames.Add($"LB_結果{row}_選手名");
                }
                else
                {
                    SetVisible(p, $"IM_明細{row}",         false);
                    SetVisible(p, $"LB_結果{row}_背番号",  false);
                    SetVisible(p, $"LB_結果{row}_選手名",  false);
                }
            }

            foreach (var n in imNames) { SetOpacity(p, n, 0); SetVisible(p, n, true); }
            foreach (var n in lbNames) { SetOpacity(p, n, 0); SetVisible(p, n, true); }

            if (_partsMain == null)
            {
                foreach (var n in imNames.Concat(lbNames)) SetOpacity(p, n, 1);
                return;
            }

            var imSb = new Storyboard();
            for (int i = 0; i < imNames.Count; i++)
                if (p.FindName(imNames[i]) is UIElement el)
                    _partsMain.フェードイン(true, el, imSb, i * 30);

            imSb.Completed += (s2, e) =>
            {
                var lbSb = new Storyboard();
                foreach (var n in lbNames)
                    if (p.FindName(n) is UIElement el2)
                        _partsMain?.フェードイン(true, el2, lbSb, 0);
                lbSb.Begin();
            };
            imSb.Begin();
        }

        /// <summary>STEP4: STEP3 で表示したものをフェードアウト</summary>
        private void Step4()
        {
            EnsurePartsMainInitialized();
            var p = PartsLST007;

            var targets = new List<string> { "LB_タイトル4" };
            for (int row = 1; row <= MaxRows; row++)
            {
                targets.Add($"IM_明細{row}");
                targets.Add($"LB_結果{row}_背番号");
                targets.Add($"LB_結果{row}_選手名");
            }

            if (_partsMain == null)
            {
                foreach (var n in targets) SetVisible(p, n, false);
                return;
            }

            var sb = new Storyboard();
            bool any = false;
            foreach (var n in targets)
                if (p.FindName(n) is UIElement el && el.Visibility == Visibility.Visible && el.Opacity > 0)
                { _partsMain.フェードアウト(true, el, sb, 0); any = true; }

            if (!any) { foreach (var n in targets) SetVisible(p, n, false); return; }
            sb.Completed += (s2, e) => { foreach (var n in targets) SetVisible(p, n, false); };
            sb.Begin();
        }

        /// <summary>STEP5: STEP2 で表示したものを非表示 → ScreenCompleted</summary>
        private void Step5()
        {
            if (!_step2Visible) return;
            var p = PartsLST007;
            SetVisible(p, "IM_タイトル1", false); SetVisible(p, "LB_タイトル1", false);
            SetVisible(p, "IM_タイトル2", false); SetVisible(p, "LB_タイトル2", false);
            SetVisible(p, "IM_タイトル3", false); SetVisible(p, "LB_タイトル3", false);
            _step2Visible = false;
            RaiseScreenCompleted();
        }

        #endregion

        #region 公開メソッド
        public void RequestClose() => _closeRequested = true;
        #endregion

        #region プライベートヘルパー

        private void BuildJudgeList()
        {
            var all = DSDspDataHelper.Getジャッジリスト_ByGroup(DA_Master, JudgeGroupId);
            _judgeList = all.Select(j => (j.JdgCd, j.JdgDispName)).ToList();
            _pageCount = Math.Max(1, (int)Math.Ceiling(_judgeList.Count / (double)MaxRows));
        }

        private void HideAllParts()
        {
            var p = PartsLST007;
            SetLabelContent(p, "LB_タイトル1", string.Empty);
            SetLabelContent(p, "LB_タイトル2", string.Empty);
            SetLabelContent(p, "LB_タイトル3", string.Empty);
            SetLabelContent(p, "LB_タイトル4", string.Empty);
            for (int row = 1; row <= MaxRows; row++)
            {
                SetLabelContent(p, $"LB_結果{row}_背番号", string.Empty);
                SetLabelContent(p, $"LB_結果{row}_選手名", string.Empty);
            }
            foreach (var name in new[] { "IM_タイトル1","LB_タイトル1","IM_タイトル2","LB_タイトル2","IM_タイトル3","LB_タイトル3","LB_タイトル4" })
                SetVisible(p, name, false);
            for (int row = 1; row <= MaxRows; row++)
            {
                SetVisible(p, $"IM_明細{row}",        false);
                SetVisible(p, $"LB_結果{row}_背番号", false);
                SetVisible(p, $"LB_結果{row}_選手名", false);
            }
        }

        private static void SetVisible(FrameworkElement p, string name, bool visible)
        {
            if (p.FindName(name) is UIElement el)
                el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
        private static void SetOpacity(FrameworkElement p, string name, double opacity)
        {
            if (p.FindName(name) is UIElement el) el.Opacity = opacity;
        }
        private static void SetLabelContent(FrameworkElement p, string name, string text)
        {
            if (p.FindName(name) is Label lb) lb.Content = text;
        }

        #endregion
    }
}

// Made with Bob
