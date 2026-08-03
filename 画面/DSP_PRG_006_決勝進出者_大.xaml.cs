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
    /// DSP_PRG_006_決勝進出者_大.xaml の相互作用ロジック
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): COM001/COM002/COM003 を表示・設定
    ///   STEP2 (case 1): IM_タイトル1-3、LB_タイトル1-3 を表示
    ///   STEP3 (case 2, 4, 6, ...): LB_タイトル4 と IM_明細/LB_結果 N行 をフェードインで表示（ページ毎）
    ///   STEP4 (case 3, 5, 7, ...): STEP3 で表示したものをフェードアウトして非表示
    ///                               → 次ページがある場合は STEP3 を繰り返す
    ///                               → 終了要求時は次の STEP4 後に STEP5 を実行
    ///   STEP5: STEP2 で表示したものを非表示 → RaiseScreenCompleted()
    ///
    /// 「次のページに進む時は STEP4→STEP3」
    /// 「この画面を表示しない時は STEP4→STEP5 を実行する」
    /// </summary>
    public partial class DSP_PRG_006_決勝進出者_大 : DSDspScreenBase
    {
        #region 定数
        private const int MaxRows = 10;
        private const string FontFamilyName = "Segoe UI Semibold";
        #endregion

        #region フィールド
        // STEP2 の表示状態
        private bool _step2Visible = false;
        // 終了（STEP5）が要求されているか
        private bool _closeRequested = false;
        // 表示する選手リスト（全ページ分）
        private List<(string 背番号, string 選手名, string 所属)> _playerList = new();
        // 総ページ数
        private int _pageCount = 1;
        #endregion

        #region プロパティ
        /// <summary>
        /// 所属表示方式。true=カップル所属名優先、false=L所属+"/"+P所属
        /// </summary>
        public bool カップル所属表示 { get; set; } = true;

        // TotalSteps は RaiseScreenCompleted で管理するため大きな値を返す
        protected override int TotalSteps => 100;
        #endregion

        #region コンストラクタ
        public DSP_PRG_006_決勝進出者_大()
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
            if (s == 1)
            {
                Step2();
                return;
            }

            // s >= 2: STEP3/STEP4 交互
            int rel = s - 2;
            if (rel % 2 == 0)
            {
                // STEP3 またはSTEP5
                int pageIdx = rel / 2;
                if (_closeRequested || pageIdx >= _pageCount)
                    Step5();
                else
                    Step3(pageIdx);
            }
            else
            {
                // STEP4
                Step4();
            }
        }
        #endregion

        #region ステップ実装

        /// <summary>STEP1: COM001, COM002, COM003 を表示・設定し、選手リストを構築。ラベルをクリア。</summary>
        private void Step1()
        {
            EnsurePartsMainInitialized();

            // 全ラベルをクリア（前回ゴミを消す）
            var p = PartsLST002;
            SetLabelContent(p, "LB_タイトル1", string.Empty);
            SetLabelContent(p, "LB_タイトル2", string.Empty);
            SetLabelContent(p, "LB_タイトル3", string.Empty);
            SetLabelContent(p, "LB_タイトル4", string.Empty);
            for (int row = 1; row <= MaxRows; row++)
            {
                SetLabelContent(p, $"LB_結果{row}_背番号", string.Empty);
                SetLabelContent(p, $"LB_結果{row}_選手名", string.Empty);
                SetLabelContent(p, $"LB_結果{row}_所属", string.Empty);
            }

            // COM003 の種目ラベルをクリア
            if (PartsCOM003.FindName("LB_右上") is Label lb003クリア)
                lb003クリア.Content = string.Empty;

            // COM001: 競技会名 (TB_左上1)
            if (PartsCOM001.FindName("TB_左上1") is TextBlock tb1)
                tb1.Text = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;

            // COM001: 現在進行番号+区分名+ラウンド名 (TB_左上2)
            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
            {
                string prgNo   = DSDspDataHelper.Get現在進行番号(DS_Status, 区分番号, ラウンド番号);
                string kbnName = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : "";
                string rndName = DA_Master != null ? DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号) : "";
                tb2.Text = $"{prgNo}　{kbnName}　{rndName}";
            }

            // COM002: 現在時刻
            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
                lbRight.Content = $"現在時刻　{DateTime.Now:HH:mm}";
            StartClock();

            // COM003: 種目記号
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

            // 選手リストを構築
            BuildPlayerList();
        }

        /// <summary>STEP2: IM_タイトル1-3、LB_タイトル1-3 を表示</summary>
        private void Step2()
        {
            EnsurePartsMainInitialized();
            var p = PartsLST002;

            // LB_タイトル1: 競技会名
            string title1 = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;
            SetLabelContent(p, "LB_タイトル1", title1);
            SetVisible(p, "IM_タイトル1", true);
            SetVisible(p, "LB_タイトル1", true);

            // LB_タイトル2: 区分名+ラウンド名（幅490, FontSize18）
            string kbnName = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : "";
            string rndName = DA_Master != null ? DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号) : "";
            string title2 = $"{kbnName}　{rndName}";
            SetLabelContent(p, "LB_タイトル2", title2);
            if (p.FindName("LB_タイトル2") is Label lbTitle2)
                _partsMain?.フォントサイズ自動調整(lbTitle2, title2, 490, 18, 8, FontFamilyName);
            SetVisible(p, "IM_タイトル2", true);
            SetVisible(p, "LB_タイトル2", true);

            // LB_タイトル3: 決勝なら「決勝進出者」、それ以外は「出場選手一覧」
            string title3 = Is決勝() ? "決勝進出者" : "出場選手一覧";
            SetLabelContent(p, "LB_タイトル3", title3);
            SetVisible(p, "IM_タイトル3", true);
            SetVisible(p, "LB_タイトル3", true);

            _step2Visible = true;
        }

        /// <summary>STEP3: 指定ページの選手明細をフェードインで表示</summary>
        private void Step3(int pageIdx)
        {
            EnsurePartsMainInitialized();
            var p = PartsLST002;
            int startIdx = pageIdx * MaxRows;
            int endIdx   = Math.Min(startIdx + MaxRows, _playerList.Count);
            int count    = endIdx - startIdx;

            // LB_タイトル4: ページ番号範囲（2ページ以上の場合のみ）
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

            // 行データを設定
            for (int row = 1; row <= MaxRows; row++)
            {
                int playerIdx = startIdx + row - 1;
                if (playerIdx < _playerList.Count)
                {
                    var player = _playerList[playerIdx];
                    SetLabelContent(p, $"LB_結果{row}_背番号", player.背番号);
                    SetLabelContent(p, $"LB_結果{row}_選手名", player.選手名);
                    SetLabelContent(p, $"LB_結果{row}_所属",   player.所属);
                    // LB_結果N_選手名: Width=316, FontSize=16
                    if (p.FindName($"LB_結果{row}_選手名") is Label lb選手名)
                        _partsMain?.フォントサイズ自動調整(lb選手名, player.選手名, 316, 16, 8, FontFamilyName);
                    // LB_結果N_所属: Width=150, FontSize=16
                    if (p.FindName($"LB_結果{row}_所属") is Label lb所属)
                        _partsMain?.フォントサイズ自動調整(lb所属, player.所属, 114, 16, 8, FontFamilyName);
                }
                else
                {
                    SetLabelContent(p, $"LB_結果{row}_背番号", string.Empty);
                    SetLabelContent(p, $"LB_結果{row}_選手名", string.Empty);
                    SetLabelContent(p, $"LB_結果{row}_所属",   string.Empty);
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
                    lbNames.Add($"LB_結果{row}_所属");
                }
                else
                {
                    SetVisible(p, $"IM_明細{row}",         false);
                    SetVisible(p, $"LB_結果{row}_背番号",  false);
                    SetVisible(p, $"LB_結果{row}_選手名",  false);
                    SetVisible(p, $"LB_結果{row}_所属",    false);
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
            var p = PartsLST002;

            var targets = new List<string> { "LB_タイトル4" };
            for (int row = 1; row <= MaxRows; row++)
            {
                targets.Add($"IM_明細{row}");
                targets.Add($"LB_結果{row}_背番号");
                targets.Add($"LB_結果{row}_選手名");
                targets.Add($"LB_結果{row}_所属");
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
            var p = PartsLST002;

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

        /// <summary>
        /// 画面を閉じる要求をセットする。
        /// 次の STEP3 相当のタイミングで STEP5 を実行する。
        /// MainWindow から「この画面を表示しない時」に呼ぶ。
        /// </summary>
        public void RequestClose()
        {
            _closeRequested = true;
        }

        #endregion

        #region プライベートヘルパー

        /// <summary>現在ラウンドが決勝かどうかを判定</summary>
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

            // DS_Status の PlayerAssignments から背番号リストを取得
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

            // 背番号順でソート
            背番号セット.Sort((a, b) =>
            {
                if (int.TryParse(a, out var na) && int.TryParse(b, out var nb))
                    return na.CompareTo(nb);
                return string.Compare(a, b, StringComparison.Ordinal);
            });

            // DA_Master から選手情報を取得してリスト構築
            foreach (var bango in 背番号セット)
            {
                var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, bango, 区分番号);
                string 選手名L = DSDspDataHelper.Get選手名L(選手情報);
                string 選手名P = DSDspDataHelper.Get選手名P(選手情報);
                string 選手名 = string.IsNullOrEmpty(選手名P) ? 選手名L : $"{選手名L}・{選手名P}";

                string 所属 = Build所属テキスト(選手情報);

                _playerList.Add((bango, 選手名, 所属));
            }

            _pageCount = Math.Max(1, (int)Math.Ceiling(_playerList.Count / (double)MaxRows));
        }

        /// <summary>所属テキストを構築（カップル所属 or L/P 所属）</summary>
        private string Build所属テキスト(System.Text.Json.Nodes.JsonNode? 選手情報)
        {
            if (選手情報 == null) return string.Empty;

            if (カップル所属表示)
            {
                return DSDspDataHelper.Get所属(選手情報);
            }
            else
            {
                string l所属 = 選手情報["DM_Ctry"]?.ToString() ?? string.Empty;
                string p所属 = 選手情報["DM_PCtry"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(p所属)) return l所属;
                return $"{l所属}/{p所属}";
            }
        }

        /// <summary>全パーツを非表示にする（初期化時）</summary>
        private void HideAllParts()
        {
            var p = PartsLST002;

            // ラベルコンテンツをクリア（前回ゴミを消す）
            SetLabelContent(p, "LB_タイトル1", string.Empty);
            SetLabelContent(p, "LB_タイトル2", string.Empty);
            SetLabelContent(p, "LB_タイトル3", string.Empty);
            SetLabelContent(p, "LB_タイトル4", string.Empty);
            for (int row = 1; row <= MaxRows; row++)
            {
                SetLabelContent(p, $"LB_結果{row}_背番号", string.Empty);
                SetLabelContent(p, $"LB_結果{row}_選手名", string.Empty);
                SetLabelContent(p, $"LB_結果{row}_所属",   string.Empty);
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
                SetVisible(p, $"IM_明細{row}",        false);
                SetVisible(p, $"LB_結果{row}_背番号", false);
                SetVisible(p, $"LB_結果{row}_選手名", false);
                SetVisible(p, $"LB_結果{row}_所属",   false);
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
