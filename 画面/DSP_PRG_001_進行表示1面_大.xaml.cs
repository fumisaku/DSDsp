using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_PRG_001_進行表示1面_大.xaml の相互作用ロジック
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): COM001（競技会名）、COM002（現在時刻）を表示
    ///   STEP2 (case 1): IM_タイトル_現/次、LB_タイトル_現/次 を表示
    ///   STEP3 (case 2): 現在競技・次の競技（最大3件）の明細・種目・時刻 を表示
    ///   STEP4 (case 3): STEP3 で表示したものを非表示
    ///                   → 次の進行がある場合は次の Advance() で STEP3 を再実行
    ///                   → 終了時は次の Advance() で STEP5 を実行
    ///   STEP5: STEP2 で表示したものを非表示 → RaiseScreenCompleted()
    /// </summary>
    public partial class DSP_PRG_001_進行表示1面_大 : DSDspScreenBase
    {
        #region 定数
        private const string FontFamilyName = "Segoe UI Semibold";
        #endregion

        #region フィールド
        private bool _step2Visible = false;
        private bool _closeRequested = false;
        #endregion

        #region プロパティ
        protected override int TotalSteps => 100;
        #endregion

        #region コンストラクタ
        public DSP_PRG_001_進行表示1面_大()
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
            switch (s)
            {
                case 0: Step1(); break;
                case 1: Step2(); break;
                default:
                    int rel = s - 2;
                    if (rel % 2 == 0)
                    {
                        if (_closeRequested) Step5();
                        else Step3();
                    }
                    else Step4();
                    break;
            }
        }
        #endregion

        #region ステップ実装

        /// <summary>STEP1: COM001, COM002 を表示。ラベルをクリア。</summary>
        private void Step1()
        {
            EnsurePartsMainInitialized();

            // 全ラベルをクリア（前回ゴミを消す）
            var p = PartsPRG001;
            foreach (var name in new[] {
                "LB_明細_現", "LB_種目_現",
                "LB_明細_次1", "LB_時刻_次1",
                "LB_明細_次2", "LB_時刻_次2",
                "LB_明細_次3", "LB_時刻_次3" })
                SetLabelContent(p, name, string.Empty);

            // COM003 の種目ラベルをクリア
            if (PartsCOM003.FindName("LB_右上") is Label lb種目)
                lb種目.Content = string.Empty;

            if (PartsCOM001.FindName("TB_左上1") is TextBlock tb1)
                tb1.Text = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;
            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
                tb2.Text = string.Empty;
            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
                lbRight.Content = $"現在時刻　{DateTime.Now:HH:mm}";
            StartClock();
        }

        /// <summary>STEP2: IM_タイトル_現/次, LB_タイトル_現/次 を表示</summary>
        private void Step2()
        {
            var p = PartsPRG001;
            SetLabelContent(p, "LB_タイトル_現", "現在の競技");
            SetVisible(p, "IM_タイトル_現", true);
            SetVisible(p, "LB_タイトル_現", true);
            SetLabelContent(p, "LB_タイトル_次", "次の競技");
            SetVisible(p, "IM_タイトル_次", true);
            SetVisible(p, "LB_タイトル_次", true);
            _step2Visible = true;
        }

        /// <summary>STEP3: 現在・次の競技 明細をフェードインで表示</summary>
        private void Step3()
        {
            EnsurePartsMainInitialized();
            var p = PartsPRG001;

            // 現在の競技
            string prgNo   = DSDspDataHelper.Get現在進行番号(DS_Status, 区分番号, ラウンド番号);
            string kbnName = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : "";
            string rndName = DA_Master != null ? DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号) : "";
            string 現テキスト = $"{prgNo}　{kbnName}　{rndName}";
            SetLabelContent(p, "LB_明細_現", 現テキスト);
            // LB_明細_現: Canvas.Left=23, Width=478。種目LBがCanvas.Left=429から始まるため実効幅=406
            if (p.FindName("LB_明細_現") is Label lb現)
                _partsMain?.フォントサイズ自動調整(lb現, 現テキスト, 406, 16, 8, FontFamilyName);

            string 種目テキスト = DA_Master != null
                ? string.Join("  ", DSDspDataHelper.Get全種目リスト(DA_Master, 区分番号, ラウンド番号).Select(d => d.DncCd))
                : string.Empty;
            SetLabelContent(p, "LB_種目_現", 種目テキスト);
            // LB_種目_現: Canvas.Left=429, 右端まで約90px, FontSize=12
            if (p.FindName("LB_種目_現") is Label lb種目現)
                _partsMain?.フォントサイズ自動調整(lb種目現, 種目テキスト, 90, 12, 7, FontFamilyName);

            // 次の競技（最大3件）
            var nextList = DSDspDataHelper.Get次進行情報リスト(DS_Status, 区分番号, ラウンド番号, 3);
            for (int i = 1; i <= 3; i++)
            {
                if (i <= nextList.Count && DA_Master != null)
                {
                    var next = nextList[i - 1];
                    string nk = DSDspDataHelper.Get区分名(DA_Master, next.KbnNo);
                    string nr = DSDspDataHelper.Getラウンド名(DA_Master, next.KbnNo, next.RndNo);
                    string 次テキスト = $"{next.PrgNo}　{nk}　{nr}";
                    SetLabelContent(p, $"LB_明細_次{i}", 次テキスト);
                    // LB_明細_次1/2/3: 時刻LBが右端にあるため実効幅394
                    if (p.FindName($"LB_明細_次{i}") is Label lb次)
                        _partsMain?.フォントサイズ自動調整(lb次, 次テキスト, 394, 16, 8, FontFamilyName);
                    if (!string.IsNullOrEmpty(next.PStaTM))
                        SetLabelContent(p, $"LB_時刻_次{i}", $"開始予定　{DSDspDataHelper.ExtractTimeOnly(next.PStaTM)}");
                    else
                        SetLabelContent(p, $"LB_時刻_次{i}", string.Empty);
                }
                else
                {
                    SetLabelContent(p, $"LB_明細_次{i}", string.Empty);
                    SetLabelContent(p, $"LB_時刻_次{i}", string.Empty);
                }
            }

            // 表示要素を収集（テキストがある行のみ IM をフェードイン対象にする）
            bool has現 = !string.IsNullOrEmpty(現テキスト.Trim());
            var imNames = new List<string>();
            var lbNames = new List<string>();

            if (has現)
            {
                imNames.Add("IM_明細_現");
                lbNames.Add("LB_明細_現");
                lbNames.Add("LB_種目_現");
            }
            else
            {
                SetVisible(p, "IM_明細_現", false);
                SetVisible(p, "LB_明細_現", false);
                SetVisible(p, "LB_種目_現", false);
            }

            for (int i = 1; i <= 3; i++)
            {
                if (i <= nextList.Count)
                {
                    imNames.Add($"IM_明細_次{i}");
                    lbNames.Add($"LB_明細_次{i}");
                    lbNames.Add($"LB_時刻_次{i}");
                }
                else
                {
                    SetVisible(p, $"IM_明細_次{i}", false);
                    SetVisible(p, $"LB_明細_次{i}", false);
                    SetVisible(p, $"LB_時刻_次{i}", false);
                }
            }

            foreach (var n in imNames) { SetOpacity(p, n, 0); SetVisible(p, n, true); }
            foreach (var n in lbNames) { SetOpacity(p, n, 0); SetVisible(p, n, true); }

            if (_partsMain == null) { foreach (var n in imNames.Concat(lbNames)) SetOpacity(p, n, 1); return; }

            var imSb = new Storyboard();
            for (int i = 0; i < imNames.Count; i++)
                if (p.FindName(imNames[i]) is UIElement el)
                    _partsMain.フェードイン(true, el, imSb, i * 100);

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

        /// <summary>STEP4: STEP3 で表示したものをフェードアウトして非表示</summary>
        private void Step4()
        {
            EnsurePartsMainInitialized();
            var p = PartsPRG001;

            var targets = new List<string> { "IM_明細_現", "LB_明細_現", "LB_種目_現" };
            for (int i = 1; i <= 3; i++)
            {
                targets.Add($"IM_明細_次{i}");
                targets.Add($"LB_明細_次{i}");
                targets.Add($"LB_時刻_次{i}");
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
            var p = PartsPRG001;
            SetVisible(p, "IM_タイトル_現", false);
            SetVisible(p, "LB_タイトル_現", false);
            SetVisible(p, "IM_タイトル_次", false);
            SetVisible(p, "LB_タイトル_次", false);
            _step2Visible = false;
            RaiseScreenCompleted();
        }

        #endregion

        #region 公開メソッド
        public void RequestClose() => _closeRequested = true;
        #endregion

        #region ヘルパー

        private void HideAllParts()
        {
            var p = PartsPRG001;
            // ラベルコンテンツをクリア
            foreach (var name in new[] {
                "LB_明細_現", "LB_種目_現",
                "LB_明細_次1", "LB_時刻_次1",
                "LB_明細_次2", "LB_時刻_次2",
                "LB_明細_次3", "LB_時刻_次3" })
                SetLabelContent(p, name, string.Empty);

            foreach (var name in new[] {
                "IM_タイトル_現", "LB_タイトル_現",
                "IM_タイトル_次", "LB_タイトル_次",
                "IM_明細_現", "LB_明細_現", "LB_種目_現",
                "IM_明細_次1", "LB_明細_次1", "LB_時刻_次1",
                "IM_明細_次2", "LB_明細_次2", "LB_時刻_次2",
                "IM_明細_次3", "LB_明細_次3", "LB_時刻_次3" })
                SetVisible(p, name, false);
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
