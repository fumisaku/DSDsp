using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_PRG_003_進行表示2面_大.xaml の相互作用ロジック
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): COM001（競技会名）、COM002（現在時刻）を表示
    ///   STEP2 (case 1, 4, 7, ...): タイトル系（IM_タイトル_現/フロア/次、LB系、LB_時刻_次）を表示
    ///   STEP3 (case 2, 5, 8, ...): 明細系（IM_明細_現A/B, LB_明細_現/次 A/B 1-3）を表示（フェードイン）
    ///   STEP4 (case 3, 6, 9, ...): STEP3 で表示したものをフェードアウトして非表示
    ///                               → 次の進行がある場合は STEP2→STEP3 を繰り返す
    ///                               → 終了要求時は次の STEP4 後に STEP5 を実行
    ///   STEP5: STEP2 で表示したものを非表示 → RaiseScreenCompleted()
    ///
    /// 「次の進行に進む時は STEP4→STEP2→STEP3」
    /// 「この画面を表示しない時は STEP4→STEP5 を実行する」
    /// </summary>
    public partial class DSP_PRG_003_進行表示2面_大 : DSDspScreenBase
    {
        #region 定数
        private const string FontFamilyName = "Segoe UI Semibold";
        #endregion

        #region フィールド
        // STEP2 の表示状態
        private bool _step2Visible = false;
        // 終了（STEP5）が要求されているか
        private bool _closeRequested = false;
        #endregion

        #region プロパティ
        // ステップ終了は RaiseScreenCompleted で管理するため大きな値を返す
        protected override int TotalSteps => 100;
        #endregion

        #region コンストラクタ
        public DSP_PRG_003_進行表示2面_大()
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

            if (s == 0)
            {
                Step1();
                return;
            }

            // s >= 1: ループ (STEP2→STEP3→STEP4) の繰り返し
            // ただし STEP4 後に _closeRequested なら STEP5
            // s=1  → STEP2 (block 0)
            // s=2  → STEP3 (block 0)
            // s=3  → STEP4 (block 0) → 次は STEP2 or STEP5
            // s=4  → STEP2 (block 1)
            // ...
            int phase = (s - 1) % 3; // 0=STEP2, 1=STEP3, 2=STEP4

            if (phase == 0)
            {
                // STEP2: タイトル表示
                // ただし _closeRequested の場合は STEP5
                if (_closeRequested)
                    Step5();
                else
                    Step2();
            }
            else if (phase == 1)
            {
                Step3();
            }
            else // phase == 2
            {
                Step4();
            }
        }
        #endregion

        #region ステップ実装

        /// <summary>STEP1: COM001, COM002 を表示。ラベルをクリア。</summary>
        private void Step1()
        {
            EnsurePartsMainInitialized();

            // 全ラベルをクリア（前回ゴミを消す）
            var p = PartsPRG002;
            foreach (var name in new[] {
                "LB_明細_現A1", "LB_明細_現A2",
                "LB_明細_現B1", "LB_明細_現B2",
                "LB_時刻_次" })
                SetLabelContent(p, name, string.Empty);
            for (int i = 1; i <= 3; i++)
            {
                SetLabelContent(p, $"LB_明細_次A{i}", string.Empty);
                SetLabelContent(p, $"LB_明細_次B{i}", string.Empty);
            }

            // COM001: 競技会名
            if (PartsCOM001.FindName("TB_左上1") is TextBlock tb1)
                tb1.Text = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;

            // TB_左上2 はブランク
            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
                tb2.Text = string.Empty;

            // COM002: 現在時刻
            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
                lbRight.Content = $"現在時刻　{DateTime.Now:HH:mm}";
            StartClock();
        }

        /// <summary>STEP2: タイトル系を表示（更新）</summary>
        private void Step2()
        {
            var p = PartsPRG002;

            // 現在の競技タイトル
            SetLabelContent(p, "LB_タイトル_現", "現在の競技");
            SetVisible(p, "IM_タイトル_現", true);
            SetVisible(p, "LB_タイトル_現", true);

            // フロアタイトル
            SetVisible(p, "IM_タイトル_フロア", true);
            SetVisible(p, "LB_タイトル_Aフロア", true);
            SetVisible(p, "LB_タイトル_Bフロア", true);

            // 次の競技タイトル
            SetLabelContent(p, "LB_タイトル_次", "次の競技");
            SetVisible(p, "IM_タイトル_次", true);
            SetVisible(p, "LB_タイトル_次", true);

            // LB_時刻_次: 両フロアの次進行の PStaTM のうち早い方を表示（時刻のみ）
            string? 時刻_次 = Get最早次進行時刻();
            if (!string.IsNullOrEmpty(時刻_次))
            {
                SetLabelContent(p, "LB_時刻_次", $"開始予定　{DSDspDataHelper.ExtractTimeOnly(時刻_次)}");
                SetVisible(p, "LB_時刻_次", true);
            }
            else
            {
                SetLabelContent(p, "LB_時刻_次", string.Empty);
                SetVisible(p, "LB_時刻_次", false);
            }

            _step2Visible = true;
        }

        /// <summary>STEP3: 明細系をフェードインで表示</summary>
        private void Step3()
        {
            EnsurePartsMainInitialized();
            var p = PartsPRG002;

            // ─── Aフロア 現在の競技 ───
            var curA = DSDspDataHelper.Getフロア現在進行情報(DS_Status, DA_Master, "A");
            if (curA.HasValue && DA_Master != null)
            {
                string kbnNameA = DSDspDataHelper.Get区分名(DA_Master, curA.Value.KbnNo);
                string rndNameA = DSDspDataHelper.Getラウンド名(DA_Master, curA.Value.KbnNo, curA.Value.RndNo);
                string dancesA  = Get種目記号テキスト(curA.Value.KbnNo, curA.Value.RndNo);

                string textA1 = $"{curA.Value.PrgNo}　{kbnNameA}";
                string textA2 = $"{rndNameA}　({dancesA})";
                SetLabelContent(p, "LB_明細_現A1", textA1);
                SetLabelContent(p, "LB_明細_現A2", textA2);
                // LB幅258, FontSize16
                if (p.FindName("LB_明細_現A1") is Label lbA1)
                    _partsMain?.フォントサイズ自動調整(lbA1, textA1, 258, 16, 8, FontFamilyName);
                if (p.FindName("LB_明細_現A2") is Label lbA2)
                    _partsMain?.フォントサイズ自動調整(lbA2, textA2, 258, 16, 8, FontFamilyName);
                SetVisible(p, "IM_明細_現A", true);
                SetVisible(p, "LB_明細_現A1", true);
                SetVisible(p, "LB_明細_現A2", true);
            }
            else
            {
                SetVisible(p, "IM_明細_現A", false);
                SetVisible(p, "LB_明細_現A1", false);
                SetVisible(p, "LB_明細_現A2", false);
            }

            // ─── Bフロア 現在の競技 ───
            var curB = DSDspDataHelper.Getフロア現在進行情報(DS_Status, DA_Master, "B");
            if (curB.HasValue && DA_Master != null)
            {
                string kbnNameB = DSDspDataHelper.Get区分名(DA_Master, curB.Value.KbnNo);
                string rndNameB = DSDspDataHelper.Getラウンド名(DA_Master, curB.Value.KbnNo, curB.Value.RndNo);
                string dancesB  = Get種目記号テキスト(curB.Value.KbnNo, curB.Value.RndNo);

                string textB1 = $"{curB.Value.PrgNo}　{kbnNameB}";
                string textB2 = $"{rndNameB}　({dancesB})";
                SetLabelContent(p, "LB_明細_現B1", textB1);
                SetLabelContent(p, "LB_明細_現B2", textB2);
                if (p.FindName("LB_明細_現B1") is Label lbB1)
                    _partsMain?.フォントサイズ自動調整(lbB1, textB1, 258, 16, 8, FontFamilyName);
                if (p.FindName("LB_明細_現B2") is Label lbB2)
                    _partsMain?.フォントサイズ自動調整(lbB2, textB2, 258, 16, 8, FontFamilyName);
                SetVisible(p, "IM_明細_現B", true);
                SetVisible(p, "LB_明細_現B1", true);
                SetVisible(p, "LB_明細_現B2", true);
            }
            else
            {
                SetVisible(p, "IM_明細_現B", false);
                SetVisible(p, "LB_明細_現B1", false);
                SetVisible(p, "LB_明細_現B2", false);
            }

            // ─── Aフロア 次の競技（最大3件）───
            var nextListA = DSDspDataHelper.Getフロア次進行情報リスト(DS_Status, "A", 3);
            SetNextRows(p, nextListA, "A");

            // ─── Bフロア 次の競技（最大3件）───
            var nextListB = DSDspDataHelper.Getフロア次進行情報リスト(DS_Status, "B", 3);
            SetNextRows(p, nextListB, "B");

            // ───フェードイン───
            var imNames = new List<string>();
            var lbNames = new List<string>();

            if (curA.HasValue) { imNames.Add("IM_明細_現A"); lbNames.Add("LB_明細_現A1"); lbNames.Add("LB_明細_現A2"); }
            if (curB.HasValue) { imNames.Add("IM_明細_現B"); lbNames.Add("LB_明細_現B1"); lbNames.Add("LB_明細_現B2"); }
            for (int i = 1; i <= 3; i++)
            {
                if (i <= nextListA.Count) { imNames.Add($"IM_明細_次A{i}"); lbNames.Add($"LB_明細_次A{i}"); }
                if (i <= nextListB.Count) { imNames.Add($"IM_明細_次B{i}"); lbNames.Add($"LB_明細_次B{i}"); }
            }

            foreach (var n in imNames) { SetOpacity(p, n, 0); }
            foreach (var n in lbNames) { SetOpacity(p, n, 0); }

            if (_partsMain == null)
            {
                foreach (var n in imNames.Concat(lbNames)) SetOpacity(p, n, 1);
                return;
            }

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
            var p = PartsPRG002;

            var targets = new List<string>
            {
                "IM_明細_現A", "LB_明細_現A1", "LB_明細_現A2",
                "IM_明細_現B", "LB_明細_現B1", "LB_明細_現B2"
            };
            for (int i = 1; i <= 3; i++)
            {
                targets.Add($"IM_明細_次A{i}"); targets.Add($"LB_明細_次A{i}");
                targets.Add($"IM_明細_次B{i}"); targets.Add($"LB_明細_次B{i}");
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
            var p = PartsPRG002;

            SetVisible(p, "IM_タイトル_現", false);
            SetVisible(p, "LB_タイトル_現", false);
            SetVisible(p, "IM_タイトル_フロア", false);
            SetVisible(p, "LB_タイトル_Aフロア", false);
            SetVisible(p, "LB_タイトル_Bフロア", false);
            SetVisible(p, "IM_タイトル_次", false);
            SetVisible(p, "LB_タイトル_次", false);
            SetVisible(p, "LB_時刻_次", false);

            _step2Visible = false;
            RaiseScreenCompleted();
        }

        #endregion

        #region 公開メソッド

        /// <summary>
        /// 画面を閉じる要求をセットする。
        /// 次の phase=0（STEP2 相当）のタイミングで STEP5 を実行する。
        /// MainWindow から「この画面を表示しない時」に呼ぶ。
        /// </summary>
        public void RequestClose()
        {
            _closeRequested = true;
        }

        #endregion

        #region プライベートヘルパー

        /// <summary>
        /// 次の進行A/B フロアの PStaTM のうち最も早い時刻文字列を返す。
        /// どちらもなければ null。
        /// </summary>
        private string? Get最早次進行時刻()
        {
            var nextA = DSDspDataHelper.Getフロア次進行情報リスト(DS_Status, "A", 1);
            var nextB = DSDspDataHelper.Getフロア次進行情報リスト(DS_Status, "B", 1);

            string? tmA = nextA.Count > 0 ? nextA[0].PStaTM : null;
            string? tmB = nextB.Count > 0 ? nextB[0].PStaTM : null;

            if (string.IsNullOrEmpty(tmA) && string.IsNullOrEmpty(tmB)) return null;
            if (string.IsNullOrEmpty(tmA)) return tmB;
            if (string.IsNullOrEmpty(tmB)) return tmA;

            // DateTime でパースして早い方を返す（日付付き文字列にも対応）
            bool parsedA = DateTime.TryParse(tmA, out var dtA);
            bool parsedB = DateTime.TryParse(tmB, out var dtB);
            if (parsedA && parsedB) return dtA <= dtB ? tmA : tmB;
            return string.Compare(tmA, tmB, StringComparison.Ordinal) <= 0 ? tmA : tmB;
        }

        /// <summary>指定区分・ラウンドの全種目記号を "WTVFQ" 形式で返す</summary>
        private string Get種目記号テキスト(string kbnNo, string rndNo)
        {
            if (DA_Master == null) return string.Empty;
            var danceList = DSDspDataHelper.Get全種目リスト(DA_Master, kbnNo, rndNo);
            return string.Join("", danceList.Select(d => d.DncCd));
        }

        /// <summary>次の競技明細行（A または B フロア）を設定する</summary>
        private void SetNextRows(
            パーツ.PRG002_競技区分表示_2 p,
            List<(string PrgNo, string KbnNo, string RndNo, string? PStaTM)> list,
            string flr)
        {
            for (int i = 1; i <= 3; i++)
            {
                if (i <= list.Count && DA_Master != null)
                {
                    var item = list[i - 1];
                    string kbnName = DSDspDataHelper.Get区分名(DA_Master, item.KbnNo);
                    string rndName = DSDspDataHelper.Getラウンド名(DA_Master, item.KbnNo, item.RndNo);
                    string text = $"{item.PrgNo}　{kbnName}　{rndName}";
                    SetLabelContent(p, $"LB_明細_次{flr}{i}", text);
                    // LB幅258, FontSize16
                    if (p.FindName($"LB_明細_次{flr}{i}") is Label lb)
                        _partsMain?.フォントサイズ自動調整(lb, text, 258, 16, 8, FontFamilyName);
                    SetVisible(p, $"IM_明細_次{flr}{i}", true);
                    SetVisible(p, $"LB_明細_次{flr}{i}", true);
                }
                else
                {
                    SetLabelContent(p, $"LB_明細_次{flr}{i}", string.Empty);
                    SetVisible(p, $"IM_明細_次{flr}{i}", false);
                    SetVisible(p, $"LB_明細_次{flr}{i}", false);
                }
            }
        }

        /// <summary>全パーツを非表示にする（初期化時）</summary>
        private void HideAllParts()
        {
            var p = PartsPRG002;

            // ラベルコンテンツをクリア（前回ゴミを消す）
            foreach (var name in new[] {
                "LB_明細_現A1", "LB_明細_現A2",
                "LB_明細_現B1", "LB_明細_現B2",
                "LB_時刻_次" })
                SetLabelContent(p, name, string.Empty);
            for (int i = 1; i <= 3; i++)
            {
                SetLabelContent(p, $"LB_明細_次A{i}", string.Empty);
                SetLabelContent(p, $"LB_明細_次B{i}", string.Empty);
            }

            foreach (var name in new[] {
                "IM_タイトル_現", "LB_タイトル_現",
                "IM_タイトル_フロア", "LB_タイトル_Aフロア", "LB_タイトル_Bフロア",
                "IM_タイトル_次", "LB_タイトル_次", "LB_時刻_次",
                "IM_明細_現A", "LB_明細_現A1", "LB_明細_現A2",
                "IM_明細_現B", "LB_明細_現B1", "LB_明細_現B2" })
            {
                SetVisible(p, name, false);
            }
            for (int i = 1; i <= 3; i++)
            {
                SetVisible(p, $"IM_明細_次A{i}", false);
                SetVisible(p, $"LB_明細_次A{i}", false);
                SetVisible(p, $"IM_明細_次B{i}", false);
                SetVisible(p, $"LB_明細_次B{i}", false);
            }
        }

        /// <summary>パーツ内の要素の Visibility を設定</summary>
        private static void SetVisible(FrameworkElement p, string name, bool visible)
        {
            if (p.FindName(name) is UIElement el)
                el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>パーツ内の要素の Opacity を設定</summary>
        private static void SetOpacity(FrameworkElement p, string name, double opacity)
        {
            if (p.FindName(name) is UIElement el) el.Opacity = opacity;
        }

        /// <summary>Label の Content を設定</summary>
        private static void SetLabelContent(FrameworkElement p, string name, string text)
        {
            if (p.FindName(name) is Label lb)
                lb.Content = text;
        }

        #endregion
    }
}

// Made with Bob
