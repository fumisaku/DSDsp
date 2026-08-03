using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_PRG_005_進行表示ヒート表_小.xaml の相互作用ロジック
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): COM001（競技会名・現在進行情報）、COM002（現在時刻）、COM003（種目記号）を表示
    ///   STEP2 (case 1): IM_現、LB_現、IM_次、LB_次 を表示（フェードイン）
    ///   STEP3 (case 2): IM_明細_現、LB_明細_現、IM_明細_次、LB_明細_次 を表示（フェードイン）
    ///   STEP4 (case 3): STEP3 で表示したものをフェードアウトして非表示
    ///   STEP5 (case 4〜): STEP2 で表示したものをフェードアウトして非表示 → RaiseScreenCompleted()
    ///                     次ヒートに進む場合は STEP4→STEP1→STEP2→STEP3 を繰り返す
    /// </summary>
    public partial class DSP_PRG_005_進行表示ヒート表_小 : DSDspScreenBase
    {
        #region 定数
        private const string FontFamilyName = "Segoe UI Semibold";
        private static readonly Brush CurrentBrush = new SolidColorBrush(Color.FromRgb(204, 85, 0));   // 濃いオレンジ
        private static readonly Brush DefaultBrush = new SolidColorBrush(Color.FromRgb(0, 0, 139));   // ダークブルー
        private static readonly Brush WhiteBrush = Brushes.White;
        #endregion

        #region フィールド
        // 全ヒートシーケンス（種目番号, ヒート番号）の順序付きリスト
        private List<(int DncNo, int HeatNo)> _heatSequence = new();
        // 全種目リスト（DncNo, DncCd）
        private List<(int DncNo, string DncCd)> _danceList = new();
        // 全種目全ヒート背番号マップ
        private Dictionary<int, Dictionary<int, List<string>>> _heatMap = new();
        // STEP2 の表示状態
        private bool _step2Visible = false;
        #endregion

        #region プロパティ
        // TotalSteps は RaiseScreenCompleted で制御するため十分大きな値を返す
        protected override int TotalSteps => _heatSequence.Count > 0
            ? 1 + _heatSequence.Count * 4 + 1   // STEP1 + ヒート数×(STEP1+STEP2+STEP3+STEP4) + STEP5
            : 6;
        #endregion

        #region コンストラクタ
        public DSP_PRG_005_進行表示ヒート表_小()
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
            // ステップマッピング:
            //   0          → STEP1（初回）
            //   1          → STEP2
            //   2          → STEP3
            //   3          → STEP4 + 次ヒートへ進むか判定
            //   4          → STEP1（次ヒート）
            //   5          → STEP2
            //   6          → STEP3
            //   7          → STEP4 ...
            //   最後の STEP4 → STEP5

            int s = _currentStep;

            if (s == 0)
            {
                // 初回 STEP1
                PrepareHeatData();
                Step1(0);
                return;
            }

            // s >= 1 以降: ヒートループ
            // 各ヒートのブロックは 4ステップ（STEP2, STEP3, STEP4, STEP1(次)）
            // ただし最初のSTEP1は s=0 で実行済み
            // s=1 → STEP2(hIdx=0)
            // s=2 → STEP3(hIdx=0)
            // s=3 → STEP4(hIdx=0) + 次ヒートSTEP1 or STEP5
            // s=4 → STEP2(hIdx=1)
            // ...

            // s=1,2,3 は hIdx=0, s=4,5,6,7 は hIdx=1 ...
            // ブロック: s=1〜3 → ブロック0、s=4〜7 → ブロック1 ...
            // 実際には s=1 が STEP2, s=2 が STEP3, s=3 が STEP4 (ブロック0)
            // s=4 は STEP1(ヒート1), s=5 は STEP2(ヒート1), s=6 は STEP3(ヒート1), s=7 が STEP4(ヒート1)
            // → 最初だけ STEP1 が先行する変則パターン

            // 変換: s=1 以降を 3ステップ×ヒート0 + 4ステップ×ヒートN で解釈
            // ブロック0: s=1(STEP2), s=2(STEP3), s=3(STEP4)  → subStep=0,1,2
            // ブロックN(N>0): s=3+N*4-3(STEP1), s=..+1(STEP2), s=..+2(STEP3), s=..+3(STEP4) → subStep=0,1,2,3

            if (s <= 3)
            {
                int subStep = s - 1; // 0=STEP2, 1=STEP3, 2=STEP4
                ExecuteHeatBlockStep(0, subStep + 1); // STEP2=1, STEP3=2, STEP4=3
            }
            else
            {
                int offset = s - 4;             // 0-based offset after block0
                int heatIdx = offset / 4 + 1;  // which heat (1-based)
                int subStep = offset % 4;       // 0=STEP1, 1=STEP2, 2=STEP3, 3=STEP4

                ExecuteHeatBlockStep(heatIdx, subStep);
            }
        }

        /// <summary>
        /// ヒートブロック内のサブステップを実行する。
        /// subStep: 0=STEP1, 1=STEP2, 2=STEP3, 3=STEP4
        /// </summary>
        private void ExecuteHeatBlockStep(int heatIdx, int subStep)
        {
            switch (subStep)
            {
                case 0:
                    // STEP1（次ヒートへ進む前に COM001/COM002/COM003 を更新）
                    Step1(heatIdx);
                    break;
                case 1:
                    Step2(heatIdx);
                    break;
                case 2:
                    Step3(heatIdx);
                    break;
                case 3:
                    Step4(heatIdx);
                    break;
            }
        }
        #endregion

        #region ステップ実装

        /// <summary>STEP1: COM001, COM002, COM003 を表示。ラベルをクリア。</summary>
        private void Step1(int heatIdx)
        {
            EnsurePartsMainInitialized();

            // 全ラベルをクリア（前回ゴミを消す）
            var p = PartsPRG005;
            foreach (var name in new[] {
                "LB_現", "LB_次",
                "LB_明細_現1", "LB_明細_現2",
                "LB_明細_次1", "LB_明細_次2" })
                SetLabelContent(p, name, string.Empty);

            // COM001: 競技会名 (TB_左上1) と 現在進行情報 (TB_左上2)
            if (PartsCOM001.FindName("TB_左上1") is System.Windows.Controls.TextBlock tb1)
                tb1.Text = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;

            if (PartsCOM001.FindName("TB_左上2") is System.Windows.Controls.TextBlock tb2)
            {
                string prgNo = GetCurrentPrgNo();
                string kbnName = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : "";
                string rndName = DA_Master != null ? DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号) : "";
                tb2.Text = $"{prgNo}　{kbnName}　{rndName}";
            }

            // COM002: 現在時刻
            if (PartsCOM002.FindName("LB_右上") is System.Windows.Controls.Label lbRight)
                lbRight.Content = $"現在時刻　{DateTime.Now:HH:mm}";
            StartClock();

            // COM003: 種目記号（現在種目は赤、その他は白）
            UpdateCOM003種目(heatIdx);
        }

        /// <summary>STEP2: IM_現、LB_現、IM_次、LB_次 を表示（フェードイン）</summary>
        private void Step2(int heatIdx)
        {
            EnsurePartsMainInitialized();
            var p = PartsPRG005;

            if (heatIdx >= _heatSequence.Count)
            {
                Step5();
                return;
            }

            var (curDncNo, curHeatNo) = _heatSequence[heatIdx];
            string curDncCd = GetDncCd(curDncNo);

            // LB_現: 「現在　W　2H」
            string 現テキスト = $"現在　{curDncCd}　{curHeatNo}H";
            SetLabelContent(p, "LB_現", 現テキスト);
            if (p.FindName("LB_現") is Label lb現)
                _partsMain?.フォントサイズ自動調整(lb現, 現テキスト, 175, 11, 7, FontFamilyName);

            // 次のヒート情報
            var next = DSDspDataHelper.Get次ヒート情報(DS_Status, DA_Master, 区分番号, ラウンド番号, curDncNo, curHeatNo);

            string? 次テキスト = null;
            if (next.HasValue)
            {
                次テキスト = $"Next　{next.Value.DncCd}　{next.Value.HeatNo}H";
                SetLabelContent(p, "LB_次", 次テキスト);
                if (p.FindName("LB_次") is Label lb次)
                    _partsMain?.フォントサイズ自動調整(lb次, 次テキスト, 175, 11, 7, FontFamilyName);
            }
            else
            {
                SetLabelContent(p, "LB_次", string.Empty);
            }

            // Opacity=0 でセット → フェードイン
            var imNames = new List<string> { "IM_現" };
            var lbNames = new List<string> { "LB_現" };
            if (next.HasValue) { imNames.Add("IM_次"); lbNames.Add("LB_次"); }

            foreach (var n in imNames) { SetOpacity(p, n, 0); SetVisible(p, n, true); }
            foreach (var n in lbNames) { SetOpacity(p, n, 0); SetVisible(p, n, true); }
            if (!next.HasValue) { SetVisible(p, "IM_次", false); SetVisible(p, "LB_次", false); }

            _step2Visible = true;

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

        /// <summary>STEP3: IM_明細_現、LB_明細_現、IM_明細_次、LB_明細_次 を表示（フェードイン）</summary>
        private void Step3(int heatIdx)
        {
            EnsurePartsMainInitialized();
            var p = PartsPRG005;

            if (heatIdx >= _heatSequence.Count)
            {
                Step5();
                return;
            }

            var (curDncNo, curHeatNo) = _heatSequence[heatIdx];

            // 現在ヒートの背番号
            var curPlayers = GetPlayers(curDncNo, curHeatNo);
            SetPlayerRows(p, curPlayers, "現");

            // 次のヒートの背番号
            var next = DSDspDataHelper.Get次ヒート情報(DS_Status, DA_Master, 区分番号, ラウンド番号, curDncNo, curHeatNo);
            if (next.HasValue)
            {
                var nextPlayers = GetPlayers(next.Value.DncNo, next.Value.HeatNo);
                SetPlayerRows(p, nextPlayers, "次");
            }
            else
            {
                SetVisible(p, "IM_明細_次1", false);
                SetVisible(p, "LB_明細_次1", false);
                SetVisible(p, "IM_明細_次2", false);
                SetVisible(p, "LB_明細_次2", false);
            }

            // フェードイン対象を収集
            var imNames = new List<string>();
            var lbNames = new List<string>();
            foreach (var suffix in new[] { "現1", "現2", "次1", "次2" })
            {
                string imN = $"IM_明細_{suffix}";
                string lbN = $"LB_明細_{suffix}";
                if (p.FindName(imN) is UIElement iel && iel.Visibility == Visibility.Visible)
                { imNames.Add(imN); lbNames.Add(lbN); }
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
        private void Step4(int heatIdx)
        {
            EnsurePartsMainInitialized();
            var p = PartsPRG005;

            var targets = new List<string>();
            foreach (var suffix in new[] { "現1", "現2", "次1", "次2" })
            {
                targets.Add($"IM_明細_{suffix}");
                targets.Add($"LB_明細_{suffix}");
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

        /// <summary>STEP5: STEP2 で表示したものをフェードアウトして非表示 → ScreenCompleted</summary>
        private void Step5()
        {
            if (!_step2Visible) return;
            EnsurePartsMainInitialized();
            var p = PartsPRG005;

            var targets = new List<string> { "IM_現", "LB_現", "IM_次", "LB_次" };

            if (_partsMain == null)
            {
                foreach (var n in targets) SetVisible(p, n, false);
                _step2Visible = false;
                RaiseScreenCompleted();
                return;
            }

            var sb = new Storyboard();
            bool any = false;
            foreach (var n in targets)
                if (p.FindName(n) is UIElement el && el.Visibility == Visibility.Visible && el.Opacity > 0)
                { _partsMain.フェードアウト(true, el, sb, 0); any = true; }

            _step2Visible = false;

            if (!any)
            {
                foreach (var n in targets) SetVisible(p, n, false);
                RaiseScreenCompleted();
                return;
            }

            sb.Completed += (s2, e) =>
            {
                foreach (var n in targets) SetVisible(p, n, false);
                RaiseScreenCompleted();
            };
            sb.Begin();
        }

        #endregion

        #region ヒートデータ準備・表示ロジック

        private void PrepareHeatData()
        {
            _heatSequence.Clear();
            _heatMap.Clear();
            _danceList.Clear();

            if (DS_Status == null || DA_Master == null) return;

            _danceList = DSDspDataHelper.Get全種目リスト(DA_Master, 区分番号, ラウンド番号);
            _heatMap = DSDspDataHelper.Get全種目全ヒート背番号マップ(DS_Status, 区分番号, ラウンド番号);

            // 全ヒートシーケンスを種目×ヒート順に構築
            var fullSequence = new List<(int DncNo, int HeatNo)>();
            foreach (var (dncNo, _) in _danceList)
            {
                if (!_heatMap.TryGetValue(dncNo, out var heatDic)) continue;
                foreach (var hNo in heatDic.Keys.OrderBy(n => n))
                    fullSequence.Add((dncNo, hNo));
            }

            // 現在種目・現在ヒートの位置から開始する
            int curDncNo  = GetCurrentDncNo();
            int curHeatNo = GetCurrentHeatNo();
            int startIdx  = 0;
            if (curDncNo > 0)
            {
                int found = fullSequence.FindIndex(h => h.DncNo == curDncNo && h.HeatNo == curHeatNo);
                if (found >= 0) startIdx = found;
                else
                {
                    // ヒートが見つからない場合、種目だけ一致する先頭ヒートを探す
                    found = fullSequence.FindIndex(h => h.DncNo == curDncNo);
                    if (found >= 0) startIdx = found;
                }
            }
            _heatSequence.AddRange(fullSequence.Skip(startIdx));
        }

        /// <summary>COM003 の種目記号を更新（現在種目のみ赤色、その他は白色）</summary>
        private void UpdateCOM003種目(int heatIdx)
        {
            if (DA_Master == null) return;

            var dances = _danceList.Count > 0
                ? _danceList
                : DSDspDataHelper.Get全種目リスト(DA_Master, 区分番号, ラウンド番号);

            // 現在のヒートインデックスから現在種目を決定
            int curDncNo = (heatIdx < _heatSequence.Count)
                ? _heatSequence[heatIdx].DncNo
                : GetCurrentDncNo();

            // Label.Content に TextBlock をセットして種目ごとに色を変える
            if (PartsCOM003.FindName("LB_右上") is System.Windows.Controls.Label lb種目)
            {
                var tb = new System.Windows.Controls.TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment   = VerticalAlignment.Center,
                    TextAlignment       = System.Windows.TextAlignment.Right,
                };
                for (int i = 0; i < dances.Count; i++)
                {
                    if (i > 0)
                        tb.Inlines.Add(new Run("  ") { Foreground = WhiteBrush });
                    bool isCur = (dances[i].DncNo == curDncNo);
                    tb.Inlines.Add(new Run(dances[i].DncCd)
                    {
                        Foreground = isCur ? CurrentBrush : WhiteBrush
                    });
                }
                lb種目.Content = tb;
            }
        }

        /// <summary>背番号を「現」または「次」のラベル2行に設定</summary>
        private void SetPlayerRows(パーツ.PRG005_ヒート表示_小 p, List<string> players, string key)
        {
            // key = "現" or "次"
            if (players.Count == 0)
            {
                SetVisible(p, $"IM_明細_{key}1", false);
                SetVisible(p, $"LB_明細_{key}1", false);
                SetVisible(p, $"IM_明細_{key}2", false);
                SetVisible(p, $"LB_明細_{key}2", false);
                return;
            }

            string allText = string.Join("  ", players);

            // 1行の最大文字数を簡易判定
            // ラベル幅 222px、フォントサイズ11 でおよそ30文字程度が目安
            const int MaxCharsPerRow = 30;
            bool needsSecondRow = allText.Length > MaxCharsPerRow;

            if (!needsSecondRow)
            {
                string text1 = allText;
                SetLabelContent(p, $"LB_明細_{key}1", text1);
                if (p.FindName($"LB_明細_{key}1") is Label lb1)
                    _partsMain?.フォントサイズ自動調整(lb1, text1, 222, 11, 7, FontFamilyName);
                SetVisible(p, $"IM_明細_{key}1", true);
                SetVisible(p, $"LB_明細_{key}1", true);
                SetVisible(p, $"IM_明細_{key}2", false);
                SetVisible(p, $"LB_明細_{key}2", false);
            }
            else
            {
                int half = (players.Count + 1) / 2;
                string line1 = string.Join("  ", players.Take(half));
                string line2 = string.Join("  ", players.Skip(half));

                SetLabelContent(p, $"LB_明細_{key}1", line1);
                if (p.FindName($"LB_明細_{key}1") is Label lb1)
                    _partsMain?.フォントサイズ自動調整(lb1, line1, 222, 11, 7, FontFamilyName);
                SetVisible(p, $"IM_明細_{key}1", true);
                SetVisible(p, $"LB_明細_{key}1", true);

                SetLabelContent(p, $"LB_明細_{key}2", line2);
                if (p.FindName($"LB_明細_{key}2") is Label lb2)
                    _partsMain?.フォントサイズ自動調整(lb2, line2, 222, 11, 7, FontFamilyName);
                SetVisible(p, $"IM_明細_{key}2", true);
                SetVisible(p, $"LB_明細_{key}2", true);
            }
        }

        /// <summary>全パーツを非表示にする（初期化時）</summary>
        private void HideAllParts()
        {
            var p = PartsPRG005;

            // ラベルコンテンツをクリア（前回ゴミを消す）
            foreach (var name in new[] {
                "LB_現", "LB_次",
                "LB_明細_現1", "LB_明細_現2",
                "LB_明細_次1", "LB_明細_次2" })
                SetLabelContent(p, name, string.Empty);

            foreach (var name in new[] {
                "IM_現", "LB_現", "IM_次", "LB_次",
                "IM_明細_現1", "LB_明細_現1",
                "IM_明細_現2", "LB_明細_現2",
                "IM_明細_次1", "LB_明細_次1",
                "IM_明細_次2", "LB_明細_次2" })
            {
                SetVisible(p, name, false);
            }
        }

        #endregion

        #region ヘルパー

        /// <summary>現在の進行番号文字列を取得</summary>
        private string GetCurrentPrgNo()
        {
            if (DS_Status == null) return string.Empty;
            var floors = DS_Status["DS_FLOORs"]?.AsArray();
            if (floors == null) return string.Empty;
            foreach (var floor in floors)
            {
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) continue;
                foreach (var prg in prgrs)
                {
                    if (prg?["DS_KbnNo"]?.ToString() == 区分番号 &&
                        prg?["DS_RndNo"]?.ToString() == ラウンド番号)
                        return prg?["DS_PrgNo"]?.ToString() ?? string.Empty;
                }
            }
            return string.Empty;
        }

        /// <summary>DS_Status から現在種目番号を取得（種目番号プロパティ優先）</summary>
        private int GetCurrentDncNo()
        {
            if (種目番号 > 0) return 種目番号;
            if (DS_Status == null) return 0;
            var floors = DS_Status["DS_FLOORs"]?.AsArray();
            if (floors == null) return 0;
            foreach (var floor in floors)
            {
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) continue;
                foreach (var prg in prgrs)
                {
                    if (prg?["DS_KbnNo"]?.ToString() == 区分番号 &&
                        prg?["DS_RndNo"]?.ToString() == ラウンド番号)
                    {
                        if (int.TryParse(prg?["DS_CurDanNo"]?.ToString(), out var dncNo)) return dncNo;
                    }
                }
            }
            return 0;
        }

        /// <summary>DS_Status から現在ヒート番号を取得（ヒート番号プロパティ優先）</summary>
        private int GetCurrentHeatNo()
        {
            if (ヒート番号 > 0) return ヒート番号;
            if (DS_Status == null) return 0;
            var floors = DS_Status["DS_FLOORs"]?.AsArray();
            if (floors == null) return 0;
            foreach (var floor in floors)
            {
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) continue;
                foreach (var prg in prgrs)
                {
                    if (prg?["DS_KbnNo"]?.ToString() == 区分番号 &&
                        prg?["DS_RndNo"]?.ToString() == ラウンド番号)
                    {
                        if (int.TryParse(prg?["DS_CurHeat"]?.ToString(), out var heatNo)) return heatNo;
                    }
                }
            }
            return 0;
        }

        /// <summary>種目番号から種目記号を取得</summary>
        private string GetDncCd(int dncNo)
        {
            var dance = _danceList.FirstOrDefault(d => d.DncNo == dncNo);
            if (dance.DncCd != null) return dance.DncCd;
            return DA_Master != null
                ? DSDspDataHelper.Get種目記号(DA_Master, 区分番号, ラウンド番号, dncNo)
                : string.Empty;
        }

        /// <summary>指定種目・ヒートの背番号リストを取得</summary>
        private List<string> GetPlayers(int dncNo, int heatNo)
        {
            if (_heatMap.TryGetValue(dncNo, out var heatDic) &&
                heatDic.TryGetValue(heatNo, out var players))
                return players;
            return new List<string>();
        }

        /// <summary>パーツ内の要素の Visibility を設定</summary>
        private static void SetVisible(System.Windows.FrameworkElement p, string name, bool visible)
        {
            if (p.FindName(name) is UIElement el)
                el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>パーツ内の要素の Opacity を設定</summary>
        private static void SetOpacity(System.Windows.FrameworkElement p, string name, double opacity)
        {
            if (p.FindName(name) is UIElement el) el.Opacity = opacity;
        }

        /// <summary>Label の Content を設定</summary>
        private static void SetLabelContent(System.Windows.FrameworkElement p, string name, string text)
        {
            if (p.FindName(name) is System.Windows.Controls.Label lb)
                lb.Content = text;
        }

        #endregion
    }
}

// Made with Bob
