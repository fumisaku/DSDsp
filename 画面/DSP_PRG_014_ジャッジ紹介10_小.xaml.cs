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
    /// DSP_PRG_014_ジャッジ紹介10_小.xaml の相互作用ロジック
    ///
    /// ジャッジ紹介（クロマキリスト）。1ページ10人表示。
    /// 使用パーツ: COM001, COM002, COM003, LST004
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): COM001/COM002/COM003 を設定、ジャッジリストを構築。ラベルをクリア。
    ///   STEP2 (case 1): IM_タイトル1-3、LB_タイトル1-3 を表示
    ///   STEP3 (case 2, 4, ...): LB_タイトル4 と IM_明細N/LB_結果N_* を表示（ページ毎）
    ///   STEP4 (case 3, 5, ...): STEP3 で表示したものをフェードアウト
    ///   STEP5: STEP2 で表示したものを非表示 → RaiseScreenCompleted()
    ///
    /// LST004 の列マッピング:
    ///   LB_結果N_順位   → 非表示
    ///   LB_結果N_背番号 → ジャッジ記号
    ///   LB_結果N_選手名 → ジャッジ表記名
    ///   LB_結果N_得点   → ジャッジ所属
    ///
    /// タイトル4: 10人以下はブランク、11人以上は「ページ/全ページ」を表示。
    /// </summary>
    public partial class DSP_PRG_014_ジャッジ紹介10_小 : DSDspScreenBase
    {
        #region 定数
        private const int MaxRows = 10;
        private const string FontFamilyName = "Segoe UI Semibold";
        #endregion

        #region フィールド
        private bool _step2Visible = false;
        private bool _closeRequested = false;
        private List<(string JdgCd, string JdgDispName, string JdgCtry)> _judgeList = new();
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
        public DSP_PRG_014_ジャッジ紹介10_小()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }
        #endregion

        #region イベントハンドラ
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsurePartsMainInitialized();

            // Advance() が先に呼ばれて Step1 以降が実行済みの場合は非表示化をスキップする。
            // ShowScreen → Advance() の順に呼んだとき、WPF の Loaded イベントが
            // Dispatcher キュー経由で後から発火し、Step1 の設定を上書きしてしまうのを防ぐ。
            if (_currentStep == 0)
                HideAllParts();

            // クロマキ背景色を AppSettings から設定
            try
            {
                var colorStr = AppSettings.Instance.ChromaKeySettings.BackgroundColor;
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                if (RootGrid != null)
                    RootGrid.Background = new System.Windows.Media.SolidColorBrush(color);
            }
            catch { /* デフォルトのまま */ }
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
            var p = PartsLST004;

            // タイトル1: 競技会名（Width=181, FontSize=9）
            string title1 = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;
            SetLabelContent(p, "LB_タイトル1", title1);
            if (p.FindName("LB_タイトル1") is Label lbT1)
                _partsMain?.フォントサイズ自動調整(lbT1, title1, 181, 9, 6, FontFamilyName);
            SetVisible(p, "IM_タイトル1", true);
            SetVisible(p, "LB_タイトル1", true);

            // タイトル2: ジャッジ紹介
            SetLabelContent(p, "LB_タイトル2", "ジャッジ紹介");
            if (p.FindName("LB_タイトル2") is Label lbT2)
                _partsMain?.フォントサイズ自動調整(lbT2, "ジャッジ紹介", 168, 10, 4, FontFamilyName);
            SetVisible(p, "IM_タイトル2", true);
            SetVisible(p, "LB_タイトル2", true);

            // タイトル3: ジャッジ
            SetLabelContent(p, "LB_タイトル3", "ジャッジ");
            if (p.FindName("LB_タイトル3") is Label lbT3)
                _partsMain?.フォントサイズ自動調整(lbT3, "ジャッジ", 140, 9, 6, FontFamilyName);
            SetVisible(p, "IM_タイトル3", true);
            SetVisible(p, "LB_タイトル3", true);

            _step2Visible = true;
        }

        /// <summary>STEP3: 指定ページのジャッジ明細を表示</summary>
        private void Step3(int pageIdx)
        {
            EnsurePartsMainInitialized();
            var p = PartsLST004;
            int startIdx = pageIdx * MaxRows;
            int endIdx   = Math.Min(startIdx + MaxRows, _judgeList.Count);

            // タイトル4: 10人以下はブランク、11人以上はページ/全ページ
            SetVisible(p, "LB_タイトル3", true);
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

                    // 順位欄は非表示
                    SetVisible(p, $"LB_結果{row}_順位", false);

                    // 背番号: ジャッジ記号
                    if (p.FindName($"LB_結果{row}_背番号") is Label lb背番号)
                    {
                        Canvas.SetLeft(lb背番号, 1);
                        lb背番号.Width = 26;
                    }
                    SetLabelContent(p, $"LB_結果{row}_背番号", j.JdgCd);

                    // 選手名: ジャッジ表記名
                    if (p.FindName($"LB_結果{row}_選手名") is Label lb選手名)
                    {
                        Canvas.SetLeft(lb選手名, 23);
                        _partsMain?.フォントサイズ自動調整(lb選手名, j.JdgDispName, 72, 10, 6, FontFamilyName);
                    }
                    SetLabelContent(p, $"LB_結果{row}_選手名", j.JdgDispName);

                    // 得点欄: ジャッジ所属
                    SetLabelContent(p, $"LB_結果{row}_得点", j.JdgCtry);
                    if (p.FindName($"LB_結果{row}_得点") is Label lb得点)
                    {
                        Canvas.SetLeft(lb得点, 98);
                        _partsMain?.フォントサイズ自動調整(lb得点, j.JdgCtry, 68, 10, 5, FontFamilyName);
                        lb得点.Width = 79;
                    }

                    SetVisible(p, $"IM_明細{row}",         true);
                    SetVisible(p, $"LB_結果{row}_背番号",  true);
                    SetVisible(p, $"LB_結果{row}_選手名",  true);
                    SetVisible(p, $"LB_結果{row}_得点",    true);
                }
                else
                {
                    SetVisible(p, $"LB_結果{row}_順位",   false);
                    SetVisible(p, $"IM_明細{row}",         false);
                    SetVisible(p, $"LB_結果{row}_背番号",  false);
                    SetVisible(p, $"LB_結果{row}_選手名",  false);
                    SetVisible(p, $"LB_結果{row}_得点",    false);
                }
            }

            // フェードイン
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
                    lbNames.Add($"LB_結果{row}_得点");
                }
            }

            foreach (var n in imNames) SetOpacity(p, n, 0);
            foreach (var n in lbNames) SetOpacity(p, n, 0);

            if (_partsMain == null)
            {
                foreach (var n in imNames.Concat(lbNames)) SetOpacity(p, n, 1);
                return;
            }

            var imSb = new Storyboard();
            for (int i = 0; i < imNames.Count; i++)
                if (p.FindName(imNames[i]) is UIElement el)
                    _partsMain.フェードイン(true, el, imSb, i * 50);

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
            var p = PartsLST004;

            SetVisible(p, "LB_タイトル3", true);
            SetVisible(p, "LB_タイトル4", false);

            var targets = new List<string>();
            for (int row = 1; row <= MaxRows; row++)
            {
                targets.Add($"IM_明細{row}");
                targets.Add($"LB_結果{row}_背番号");
                targets.Add($"LB_結果{row}_選手名");
                targets.Add($"LB_結果{row}_得点");
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
            var p = PartsLST004;
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
            _judgeList = all.Select(j => (j.JdgCd, j.JdgDispName, j.JdgCtry)).ToList();
            _pageCount = Math.Max(1, (int)Math.Ceiling(_judgeList.Count / (double)MaxRows));
        }

        private void HideAllParts()
        {
            var p = PartsLST004;
            SetLabelContent(p, "LB_タイトル1", string.Empty);
            SetLabelContent(p, "LB_タイトル2", string.Empty);
            SetLabelContent(p, "LB_タイトル3", string.Empty);
            SetLabelContent(p, "LB_タイトル4", string.Empty);
            for (int row = 1; row <= MaxRows; row++)
            {
                SetLabelContent(p, $"LB_結果{row}_背番号", string.Empty);
                SetLabelContent(p, $"LB_結果{row}_選手名", string.Empty);
                SetLabelContent(p, $"LB_結果{row}_得点",   string.Empty);
            }
            foreach (var name in new[] { "IM_タイトル1","LB_タイトル1","IM_タイトル2","LB_タイトル2","IM_タイトル3","LB_タイトル3","LB_タイトル4" })
                SetVisible(p, name, false);
            for (int row = 1; row <= MaxRows; row++)
            {
                SetVisible(p, $"LB_結果{row}_順位",   false);
                SetVisible(p, $"IM_明細{row}",         false);
                SetVisible(p, $"LB_結果{row}_背番号",  false);
                SetVisible(p, $"LB_結果{row}_選手名",  false);
                SetVisible(p, $"LB_結果{row}_得点",    false);
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
