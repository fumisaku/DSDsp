using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_PRG_007_決勝進出者_小.xaml の相互作用ロジック
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): COM001/COM002/COM003 を表示・設定、選手リストを構築
    ///   STEP2 (case 1): IM_タイトル1-3、LB_タイトル1-3 を表示
    ///   STEP3 (case 2, 4, 6, ...): LB_タイトル4 と IM_明細N / LB_結果N_背番号/選手名/得点 を表示（ページ毎）
    ///   STEP4 (case 3, 5, 7, ...): STEP3 で表示したものを非表示
    ///   STEP5: STEP2 で表示したものを非表示 → RaiseScreenCompleted()
    ///
    /// LB_結果N_順位   → 非表示（画面側で隠す）
    /// LB_結果N_背番号 → Canvas.Left=2 に調整して表示
    /// LB_結果N_選手名 → Canvas.Left=23 に調整して表示
    /// LB_結果N_得点   → 所属名を表示。フォントサイズ自動調整。
    /// </summary>
    public partial class DSP_PRG_007_決勝進出者_小 : DSDspScreenBase
    {
        #region 定数
        private const int MaxRows = 10;
        private const string FontFamilyName = "Segoe UI Semibold";
        #endregion

        #region フィールド
        private bool _step2Visible = false;
        private bool _closeRequested = false;
        private List<(string 背番号, string 選手名, string 所属)> _playerList = new();
        private int _pageCount = 1;
        #endregion

        #region プロパティ
        /// <summary>所属表示方式。true=カップル所属名優先、false=L所属+"/"+P所属</summary>
        public bool カップル所属表示 { get; set; } = true;

        protected override int TotalSteps => 100;
        #endregion

        #region コンストラクタ
        public DSP_PRG_007_決勝進出者_小()
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

        /// <summary>STEP1: COM001/COM002/COM003 を設定し選手リストを構築。ラベルをクリア。</summary>
        private void Step1()
        {
            EnsurePartsMainInitialized();

            // 全ラベルをクリア（前回ゴミを消す）
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

            // COM003 の種目ラベルをクリア
            if (PartsCOM003.FindName("LB_右上") is Label lb003クリア)
                lb003クリア.Content = string.Empty;

            if (PartsCOM001.FindName("TB_左上1") is TextBlock tb1)
                tb1.Text = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;

            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
            {
                string prgNo   = DSDspDataHelper.Get現在進行番号(DS_Status, 区分番号, ラウンド番号, DGrpNo);
                string kbnName = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : "";
                string rndName = DA_Master != null ? DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号) : "";
                tb2.Text = $"{prgNo}　{kbnName}　{rndName}";
            }

            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
                lbRight.Content = $"現在時刻　{DateTime.Now:HH:mm}";
            StartClock();

            if (PartsCOM003.FindName("LB_右上") is Label lb003)
            {
                string 種目テキスト = string.Empty;
                if (DA_Master != null)
                {
                    var danceList = DSDspDataHelper.Get全種目リスト(DA_Master, 区分番号, ラウンド番号);
                    種目テキスト = string.Join("  ", danceList.Select(d => d.DncCd));
                }
                lb003.Content = 種目テキスト;
            }

            BuildPlayerList();
        }

        /// <summary>STEP2: IM_タイトル1-3、LB_タイトル1-3 を表示</summary>
        private void Step2()
        {
            EnsurePartsMainInitialized();
            var p = PartsLST004;

            // LB_タイトル1: 競技会名（Width=181, FontSize=9）
            string title1 = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;
            SetLabelContent(p, "LB_タイトル1", title1);
            if (p.FindName("LB_タイトル1") is Label lbTitle1)
                _partsMain?.フォントサイズ自動調整(lbTitle1, title1, 181, 9, 6, FontFamilyName);
            SetVisible(p, "IM_タイトル1", true);
            SetVisible(p, "LB_タイトル1", true);

            // LB_タイトル2: 区分名+ラウンド名（Width=168, FontSize=10）
            string kbnName = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : "";
            string rndName = DA_Master != null ? DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号) : "";
            string title2 = $"{kbnName}　{rndName}";
            SetLabelContent(p, "LB_タイトル2", title2);
            if (p.FindName("LB_タイトル2") is Label lbTitle2)
                _partsMain?.フォントサイズ自動調整(lbTitle2, title2, 160, 10, 4, FontFamilyName);
            SetVisible(p, "IM_タイトル2", true);
            SetVisible(p, "LB_タイトル2", true);

            // LB_タイトル3: 決勝なら「決勝進出者」、それ以外は「出場選手一覧」
            string title3 = Is決勝() ? "決勝進出者" : "出場選手一覧";
            SetLabelContent(p, "LB_タイトル3", title3);
            if (p.FindName("LB_タイトル3") is Label lbTitle3)
                _partsMain?.フォントサイズ自動調整(lbTitle3, title3, 140, 9, 6, FontFamilyName);
            SetVisible(p, "IM_タイトル3", true);
            SetVisible(p, "LB_タイトル3", true);

            _step2Visible = true;
        }

        /// <summary>STEP3: 指定ページの選手明細を表示</summary>
        private void Step3(int pageIdx)
        {
            EnsurePartsMainInitialized();
            var p = PartsLST004;
            int startIdx = pageIdx * MaxRows;
            int endIdx   = Math.Min(startIdx + MaxRows, _playerList.Count);

            // LB_タイトル3 は常に表示。LB_タイトル4 は2ページ以上の時のみ範囲を追加表示。
            SetVisible(p, "LB_タイトル3", true);
            if (_pageCount > 1)
            {
                SetLabelContent(p, "LB_タイトル4", $"{startIdx + 1} ～ {endIdx}");
                SetVisible(p, "LB_タイトル4", true);
            }
            else
            {
                SetLabelContent(p, "LB_タイトル4", string.Empty);
                SetVisible(p, "LB_タイトル4", false);
            }

            for (int row = 1; row <= MaxRows; row++)
            {
                int playerIdx = startIdx + row - 1;
                if (playerIdx < _playerList.Count)
                {
                    var player = _playerList[playerIdx];

                    // LB_結果N_順位 → 非表示
                    SetVisible(p, $"LB_結果{row}_順位", false);

                    // LB_結果N_背番号: Canvas.Left=2 に調整
                    if (p.FindName($"LB_結果{row}_背番号") is Label lb背番号)
                    {
                        System.Windows.Controls.Canvas.SetLeft(lb背番号, 1);
                        // widthを26に調整                    
                        lb背番号.Width = 26;
                    }

                    SetLabelContent(p, $"LB_結果{row}_背番号", player.背番号);

                    // LB_結果N_選手名: Canvas.Left=23 に調整（Width=77, FontSize=10）
                    if (p.FindName($"LB_結果{row}_選手名") is Label lb選手名)
                    {
                        System.Windows.Controls.Canvas.SetLeft(lb選手名, 23);
                        _partsMain?.フォントサイズ自動調整(lb選手名, player.選手名, 72, 10, 6, FontFamilyName);
                    }
                    SetLabelContent(p, $"LB_結果{row}_選手名", player.選手名);

                    // LB_結果N_得点（所属）: Width=76, FontSize=10  Canvas.Left=100
                    SetLabelContent(p, $"LB_結果{row}_得点", player.所属);
                    if (p.FindName($"LB_結果{row}_得点") is Label lb得点)
                    {
                        System.Windows.Controls.Canvas.SetLeft(lb得点, 98);
                        _partsMain?.フォントサイズ自動調整(lb得点, player.所属, 68, 10, 5, FontFamilyName);
                        lb得点.Width = 79;
                    }

                    SetVisible(p, $"IM_明細{row}",         true);
                    SetVisible(p, $"LB_結果{row}_背番号",  true);
                    SetVisible(p, $"LB_結果{row}_選手名",  true);
                    SetVisible(p, $"LB_結果{row}_得点",    true);
                }
                else
                {
                    SetVisible(p, $"LB_結果{row}_順位",    false);
                    SetVisible(p, $"IM_明細{row}",         false);
                    SetVisible(p, $"LB_結果{row}_背番号",  false);
                    SetVisible(p, $"LB_結果{row}_選手名",  false);
                    SetVisible(p, $"LB_結果{row}_得点",    false);
                }
            }

            // フェードイン対象を収集（データがある行のみ）
            var imNames = new List<string>();
            var lbNames = new List<string>();
            for (int row = 1; row <= MaxRows; row++)
            {
                int playerIdx = startIdx + row - 1;
                if (playerIdx < _playerList.Count)
                {
                    imNames.Add($"IM_明細{row}");
                    lbNames.Add($"LB_結果{row}_背番号");
                    lbNames.Add($"LB_結果{row}_選手名");
                    lbNames.Add($"LB_結果{row}_得点");
                }
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

        /// <summary>STEP4: STEP3 で表示したものをフェードアウトして非表示</summary>
        private void Step4()
        {
            EnsurePartsMainInitialized();
            var p = PartsLST004;

            // LB_タイトル3 を再表示、LB_タイトル4 を非表示
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

            SetVisible(p, "IM_タイトル1", false);
            SetVisible(p, "LB_タイトル1", false);
            SetVisible(p, "IM_タイトル2", false);
            SetVisible(p, "LB_タイトル2", false);
            SetVisible(p, "IM_タイトル3", false);
            SetVisible(p, "LB_タイトル3", false);

            _step2Visible = false;
            RaiseScreenCompleted();
        }

        #endregion

        #region 公開メソッド

        /// <summary>画面を閉じる要求。次の STEP3 相当タイミングで STEP5 を実行する。</summary>
        public void RequestClose() => _closeRequested = true;

        #endregion

        #region プライベートヘルパー

        private bool Is決勝()
        {
            // ラウンドNO=400 が決勝。「準決勝」など"決勝"を含む名前と誤判定しないようラウンド番号で判定する。
            return ラウンド番号 == "400";
        }

        /// <summary>DS_Status と DA_Master から出場選手リストを構築</summary>
        private void BuildPlayerList()
        {
            _playerList.Clear();
            if (DS_Status == null || DA_Master == null) return;

            // PlayerAssignments から背番号を収集
            var floors = DS_Status["DS_FLOORs"]?.AsArray();
            if (floors == null) return;

            var 背番号セット = new List<string>();
            foreach (var floor in floors)
            {
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) continue;
                foreach (var prg in prgrs)
                {
                    if (prg?["DS_KbnNo"]?.ToString() != 区分番号 ||
                        prg?["DS_RndNo"]?.ToString() != ラウンド番号) continue;

                    var assignments = prg?["PlayerAssignments"]?.AsArray();
                    if (assignments == null) continue;
                    foreach (var a in assignments)
                    {
                        var no = a?["PlayerNo"]?.ToString();
                        if (!string.IsNullOrEmpty(no) && !背番号セット.Contains(no!))
                            背番号セット.Add(no!);
                    }
                    break;
                }
            }

            // 背番号昇順ソート
            背番号セット.Sort((a, b) =>
            {
                if (int.TryParse(a, out var na) && int.TryParse(b, out var nb)) return na.CompareTo(nb);
                return string.Compare(a, b, StringComparison.Ordinal);
            });

            foreach (var bango in 背番号セット)
            {
                var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, bango, 区分番号);
                // 苗字のみ表示
                string lName = DSDspDataHelper.Get選手名L(選手情報);
                string pName = DSDspDataHelper.Get選手名P(選手情報);
                string l苗字 = GetFamilyName(lName);
                string p苗字 = GetFamilyName(pName);
                string 選手名 = string.IsNullOrEmpty(p苗字) ? l苗字 : $"{l苗字}・{p苗字}";

                string 所属 = Build所属テキスト(選手情報);
                _playerList.Add((bango, 選手名, 所属));
            }

            _pageCount = Math.Max(1, (int)Math.Ceiling(_playerList.Count / (double)MaxRows));
        }

        /// <summary>表示名から苗字（最初のトークン）を取得</summary>
        private static string GetFamilyName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return string.Empty;
            var parts = fullName.Split(new[] { '　', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : fullName;
        }

        /// <summary>所属テキストを構築（カップル所属 or L/P 所属）</summary>
        private string Build所属テキスト(System.Text.Json.Nodes.JsonNode? 選手情報)
        {
            if (選手情報 == null) return string.Empty;
            if (カップル所属表示)
                return DSDspDataHelper.Get所属(選手情報);

            string l所属 = 選手情報["DM_Ctry"]?.ToString() ?? string.Empty;
            string p所属 = 選手情報["DM_PCtry"]?.ToString() ?? string.Empty;
            return string.IsNullOrEmpty(p所属) ? l所属 : $"{l所属}/{p所属}";
        }

        /// <summary>全パーツを非表示にする（初期化時）</summary>
        private void HideAllParts()
        {
            var p = PartsLST004;

            // ラベルコンテンツをクリア（前回ゴミを消す）
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

            foreach (var name in new[] {
                "IM_タイトル1", "LB_タイトル1",
                "IM_タイトル2", "LB_タイトル2",
                "IM_タイトル3", "LB_タイトル3",
                "LB_タイトル4" })
            {
                SetVisible(p, name, false);
            }
            for (int row = 1; row <= MaxRows; row++)
            {
                SetVisible(p, $"LB_結果{row}_順位",    false);
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
            if (p.FindName(name) is Label lb)
                lb.Content = text;
        }

        #endregion
    }
}

// Made with Bob
