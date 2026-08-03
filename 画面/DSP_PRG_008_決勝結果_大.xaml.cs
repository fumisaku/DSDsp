using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_PRG_008_決勝結果_大.xaml の相互作用ロジック
    ///
    /// 使用パーツ: PartsLST003（LST003_大リスト10_結果）
    ///   LB_タイトル1   競技会名
    ///   LB_タイトル2   区分番号 + 区分名
    ///   LB_タイトル3   ラウンド名 + 結果（決勝なら「決勝結果」）
    ///   LB_タイトル4   ページ範囲（例「1位 ～ 10位」）、1ページ時はブランク
    ///   LB_タイトル_Total  AJS採点時のみ表示
    ///   LB_結果N_順位  順位テキスト（1位→優勝、2位→準優勝、3位以降→N位）
    ///   LB_結果N_背番号 背番号
    ///   LB_結果N_選手名 L表記名+「・」+P表記名
    ///   LB_結果N_所属  カップル所属 or L/P所属。AJS・非AJS共通で右寄せ表示（Width=140, Canvas.Left=282）
    ///   LB_結果N_得点  総合得点（AJS採点時のみ）
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): COM001（競技会名・「表彰式」）、COM002（現在時刻）を表示。
    ///                   選手リストを構築。
    ///   STEP2 (case 1): IM_タイトル1-3、LB_タイトル1-3 を表示。
    ///
    ///   ■ 1組ずつ表示モード（IsPageMode=false）
    ///     STEP3〜: 昇順の場合は 1位から、降順の場合は最終順位から 1組ずつ表示。
    ///              LB_タイトル4 はページが変わるタイミングで設定。
    ///     STEPx:  STEP3以降に表示した明細をすべて非表示。
    ///             → 次ページがあれば再び STEP3 を繰り返す。
    ///     STEPxx: STEP2 の表示物を非表示 → RaiseScreenCompleted()
    ///
    ///   ■ ページ毎表示モード（IsPageMode=true）
    ///     STEP3: 1ページ目の全選手（最大10行）を一括表示。
    ///     STEPx: 明細を非表示 → 次ページがあれば STEP3 を繰り返す。
    ///     STEPxx: STEP2 の表示物を非表示 → RaiseScreenCompleted()
    /// </summary>
    public partial class DSP_PRG_008_決勝結果_大 : DSDspScreenBase
    {
        #region 定数
        private const int MaxRows = 10;
        #endregion

        #region フィールド
        /// <summary>STEP2 を表示中かどうか</summary>
        private bool _step2Visible = false;
        /// <summary>終了要求フラグ（次の STEP3 相当で STEP5 を実行する）</summary>
        private bool _closeRequested = false;
        /// <summary>AJS採点かどうか</summary>
        private bool _isAJS = false;
        /// <summary>ページ数</summary>
        private int _pageCount = 1;
        /// <summary>
        /// 表示用選手リスト（昇順）。
        /// (順位番号, 背番号, 得点, 順位表記, 選手名, 所属)
        /// </summary>
        private List<(int 順位番号, string 背番号, decimal 得点, string 順位表記, string 選手名, string 所属)> _resultList = new();
        /// <summary>内部フェーズ: 0=未開始, 1=STEP1完了, 2=STEP2表示中, 3=明細表示中, 4=完了</summary>
        private int _phase = 0;
        /// <summary>現在の表示カーソル（1組ずつモード: 選手インデックス、ページ毎モード: ページインデックス）</summary>
        private int _cursor = 0;
        /// <summary>現在表示中のページ（1組ずつモードでページ切り替え判定に使用）</summary>
        private int _currentPage = 0;
        #endregion

        #region プロパティ
        /// <summary>所属表示方式。true=カップル所属名優先、false=L所属+"/"+P所属</summary>
        public bool カップル所属表示 { get; set; } = true;

        /// <summary>表示順序。true=昇順（1位から）、false=降順（最終順位から）</summary>
        public bool 昇順表示 { get; set; } = true;

        /// <summary>
        /// ページ毎表示モード。
        /// true=1ページ（最大10行）を一括表示してからページを切り替える。
        /// false=1組ずつ表示する。
        /// </summary>
        public bool IsPageMode { get; set; } = false;

        // TotalSteps は RaiseScreenCompleted で管理するため大きな値を返す
        protected override int TotalSteps => 100;
        #endregion

        #region コンストラクタ
        public DSP_PRG_008_決勝結果_大()
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
            switch (_currentStep)
            {
                case 0:
                    DoStep1();
                    break;
                case 1:
                    DoStep2();
                    break;
                default:
                    DoStep3OrLater();
                    break;
            }
        }
        #endregion

        #region ステップ実装

        /// <summary>STEP1: COM001, COM002, COM003 を表示。選手リストを構築。</summary>
        private void DoStep1()
        {
            // COM001: 競技会名 (TB_左上1)
            if (PartsCOM001.FindName("TB_左上1") is TextBlock tb1)
                tb1.Text = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;

            // COM001: TB_左上2 = 区分名
            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
                tb2.Text = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : string.Empty;

            // COM002(右上01): 「表彰式」
            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
                lbRight.Content = "表彰式";

            // COM003(右上02): クリア（表彰式では不要）
            if (PartsCOM003.FindName("LB_右上") is Label lb003)
                lb003.Content = string.Empty;

            // 選手リストを構築
            BuildResultList();
            _phase = 1;
        }

        /// <summary>STEP2: IM_タイトル1-3、LB_タイトル1-3 を表示</summary>
        private void DoStep2()
        {
            var p = PartsLST003;

            // LB_タイトル1: 競技会名
            SetLabel(p, "LB_タイトル1", DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty);
            SetVisible(p, "IM_タイトル1", true);
            SetVisible(p, "LB_タイトル1", true);

            // LB_タイトル2: 区分番号 + 区分名
            string kbnName = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : string.Empty;
            SetLabel(p, "LB_タイトル2", $"{区分番号}　{kbnName}");
            SetVisible(p, "IM_タイトル2", true);
            SetVisible(p, "LB_タイトル2", true);

            // LB_タイトル3: ラウンド名+結果（決勝→「決勝結果」）
            string rndName = DA_Master != null ? DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号) : string.Empty;
            string title3 = rndName.Contains("決勝") ? "決勝結果" : $"{rndName}結果";
            SetLabel(p, "LB_タイトル3", title3);
            SetVisible(p, "IM_タイトル3", true);
            SetVisible(p, "LB_タイトル3", true);

            // LB_タイトル_Total: AJS採点時のみ表示
            SetVisible(p, "LB_タイトル_Total", _isAJS);

            // LB_タイトル4 は STEP3 でページ毎に設定するためここでは非表示
            SetLabel(p, "LB_タイトル4", string.Empty);
            SetVisible(p, "LB_タイトル4", false);

            _step2Visible = true;
            _phase = 2;
        }

        /// <summary>STEP3以降: フェーズに応じて表示/非表示/完了を制御</summary>
        private void DoStep3OrLater()
        {
            if (_phase == 2)
            {
                // STEP3: 明細を表示
                if (_closeRequested)
                {
                    DoStep5();
                    return;
                }
                ShowCurrentEntries();
                _phase = 3;
            }
            else if (_phase == 3)
            {
                // STEPx:
                // ■ ページ毎モード: 明細を非表示にして次ページへ
                // ■ 1組ずつモード : 行を維持したまま次の選手を追加表示
                if (IsPageMode)
                    HideAllRows();

                // 次のエントリ/ページがあるか確認
                if (_closeRequested || !AdvanceCursor())
                {
                    // 全選手表示完了
                    if (IsPageMode)
                    {
                        // ページ毎モード（一括）: 直接STEP5
                        DoStep5();
                    }
                    else
                    {
                        // 1組ずつモード（昇順/降順）: 明細非表示待ちフェーズへ
                        _phase = 5;
                    }
                }
                else
                {
                    // 次表示へ
                    _phase = 2;
                }
            }
            else if (_phase == 5)
            {
                // 全選手表示後の1回目の再生: 明細を非表示
                HideAllRows();
                _phase = 6;
            }
            else if (_phase == 6)
            {
                // 全選手表示後の2回目の再生: タイトルを非表示 → 完了
                DoStep5();
            }
        }
        /// <summary>STEP5: STEP2 の全表示要素を非表示にして完了通知</summary>
        private void DoStep5()
        {
            if (!_step2Visible) return;
            var p = PartsLST003;

            foreach (var name in new[] {
                "IM_タイトル1", "LB_タイトル1",
                "IM_タイトル2", "LB_タイトル2",
                "IM_タイトル3", "LB_タイトル3",
                "LB_タイトル4", "LB_タイトル_Total" })
            {
                SetVisible(p, name, false);
            }

            // COM001 の TB_左上2 を非表示
            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
                tb2.Text = string.Empty;

            _step2Visible = false;
            _phase = 4;
            RaiseScreenCompleted();
        }

        #endregion

        #region 公開メソッド

        /// <summary>
        /// 画面を閉じる要求をセットする。
        /// 次の STEPx（明細非表示）のタイミングで STEP5 を実行する。
        /// </summary>
        public void RequestClose()
        {
            _closeRequested = true;
        }

        #endregion

        #region 明細表示ロジック

        /// <summary>
        /// 現在のカーソル位置に応じて選手明細を表示する。
        /// 1組ずつモード: _cursor が示す選手1名分を行に表示。
        /// ページ毎モード: _cursor が示すページの全選手（最大10行）を表示。
        /// </summary>
        private void ShowCurrentEntries()
        {
            var p = PartsLST003;

            if (IsPageMode)
            {
                // ページ毎表示: _cursor = ページインデックス
                int startIdx = _cursor * MaxRows;
                int endIdx   = Math.Min(startIdx + MaxRows, _resultList.Count);
                ShowPageEntries(startIdx, endIdx);
                UpdateLBタイトル4(startIdx, endIdx);
            }
            else
            {
                // 1組ずつ表示: 降順の場合は下の行から埋める
                int playerIdx = GetDisplayIndex(_cursor);
                if (playerIdx < 0 || playerIdx >= _resultList.Count) return;

                int row;
                if (!昇順表示)
                {
                    // 降順: ページ内の何行目に当たるかを逆順で計算
                    int pageIdx   = _cursor / MaxRows;
                    int pageStart = pageIdx * MaxRows;
                    int pageEnd   = Math.Min(pageStart + MaxRows, _resultList.Count);
                    int pageSize  = pageEnd - pageStart;
                    int posInPage = _cursor % MaxRows; // 0-based: 0=最初に表示（=最下位）
                    row = pageSize - posInPage;         // 最下位が最後行、最上位が1行目
                }
                else
                {
                    row = (_cursor % MaxRows) + 1;
                }
                ShowEntryAtRow(playerIdx, row);

                // LB_タイトル4: ページ先頭の選手が表示されるタイミングで更新
                if (_cursor % MaxRows == 0)
                {
                    int pageStart = _cursor / MaxRows * MaxRows;
                    int pageEnd   = Math.Min(pageStart + MaxRows, _resultList.Count);
                    UpdateLBタイトル4(pageStart, pageEnd);
                }
            }
        }

        /// <summary>ページ毎表示: 指定範囲の選手を一括表示（フェードイン付き）</summary>
        private void ShowPageEntries(int startIdx, int endIdx)
        {
            if (_partsMain == null) { ShowPageEntriesImmediate(startIdx, endIdx); return; }
            var p = PartsLST003;
            var sb = new System.Windows.Media.Animation.Storyboard();
            for (int row = 1; row <= MaxRows; row++)
            {
                int idx = startIdx + row - 1;
                if (idx < endIdx)
                {
                    SetEntryLabels(idx, row);
                    FadeInRow(row, sb, (row - 1) * 40); // 各行を40msずつずらしてフェードイン
                }
                else
                    HideRow(row);
            }
            sb.Begin();
        }

        /// <summary>フェードなしでページ一括表示（_partsMain が null の場合のフォールバック）</summary>
        private void ShowPageEntriesImmediate(int startIdx, int endIdx)
        {
            var p = PartsLST003;
            for (int row = 1; row <= MaxRows; row++)
            {
                int idx = startIdx + row - 1;
                if (idx < endIdx) { SetEntryLabels(idx, row); SetRowVisible(row, true); }
                else HideRow(row);
            }
        }

        /// <summary>指定の選手データを指定行に設定・フェードイン表示する</summary>
        private void ShowEntryAtRow(int playerIdx, int row)
        {
            if (_partsMain == null) { SetEntryLabels(playerIdx, row); SetRowVisible(row, true); return; }
            SetEntryLabels(playerIdx, row);
            var sb = new System.Windows.Media.Animation.Storyboard();
            FadeInRow(row, sb, 0);
            sb.Begin();
        }

        /// <summary>指定行の要素をStoryboardに追加してフェードインアニメーションを設定する</summary>
        private void FadeInRow(int row, System.Windows.Media.Animation.Storyboard sb, int delayMs)
        {
            var p = PartsLST003;
            foreach (var name in GetRowElementNames(row))
            {
                if (p.FindName(name) is UIElement el)
                {
                    el.Opacity = 0;
                    el.Visibility = Visibility.Visible;
                    _partsMain!.フェードイン(true, el, sb, delayMs);
                }
            }
        }

        /// <summary>指定行の要素をフェードアウトするStoryboardを生成して開始する</summary>
        private void FadeOutRow(int row, System.Windows.Media.Animation.Storyboard sb, int delayMs)
        {
            var p = PartsLST003;
            foreach (var name in GetRowElementNames(row))
            {
                if (p.FindName(name) is UIElement el && el.Visibility == Visibility.Visible)
                    _partsMain!.フェードアウト(true, el, sb, delayMs);
            }
        }

        /// <summary>行を構成する UIElement の名前一覧を返す（IM_明細N + LB_結果N_*）</summary>
        private IEnumerable<string> GetRowElementNames(int row)
        {
            yield return $"IM_明細{row}";
            yield return $"LB_結果{row}_順位";
            yield return $"LB_結果{row}_背番号";
            yield return $"LB_結果{row}_選手名";
            yield return $"LB_結果{row}_所属";
            yield return $"LB_結果{row}_得点";
        }

        /// <summary>行の Visibility を一括設定する（フェードなし）</summary>
        private void SetRowVisible(int row, bool visible)
        {
            var p = PartsLST003;
            foreach (var name in GetRowElementNames(row))
                SetVisible(p, name, visible);
        }

        /// <summary>指定行にデータをセットする（表示/非表示は変更しない）</summary>
        private void SetEntryLabels(int playerIdx, int row)
        {
            var p = PartsLST003;
            var entry = _resultList[playerIdx];

            string rankText = DSDspDataHelper.Format順位テキスト(entry.順位番号, entry.順位表記);
            SetLabel(p, $"LB_結果{row}_順位", rankText);
            SetLabel(p, $"LB_結果{row}_背番号", entry.背番号);

            SetLabel(p, $"LB_結果{row}_選手名", entry.選手名);
            if (_partsMain != null && p.FindName($"LB_結果{row}_選手名") is Label lbName)
                _partsMain.フォントサイズ自動調整(lbName, entry.選手名, 188, 16, 8, "Segoe UI Semibold");

            // AJS・非AJS 共通: 所属を右寄せ・Width=140 で表示
            if (p.FindName($"LB_結果{row}_所属") is Label lbShozoku)
            {
                lbShozoku.Content = entry.所属;
                lbShozoku.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Right;
                if (_partsMain != null)
                    _partsMain.フォントサイズ自動調整(lbShozoku, entry.所属, 140, 14, 7, "Segoe UI Semibold");
                lbShozoku.Visibility = Visibility.Collapsed; // フェードインで表示
                if (_isAJS)
                { 
                    
                }
                else
                {
                    // 非AJSの時はCanvas.Leftを361に変更
                    Canvas.SetLeft(lbShozoku, 361);
                }
            }

            if (_isAJS)
            {
                // AJS: 得点も表示
                SetLabel(p, $"LB_結果{row}_得点", entry.得点.ToString("F3"));
                if (p.FindName($"LB_結果{row}_得点") is UIElement el得点)
                    el得点.Visibility = Visibility.Collapsed; // フェードインで表示
            }
            else
            {
                // 非AJS: 得点欄は非表示
                SetLabel(p, $"LB_結果{row}_得点", String.Empty);
                if (p.FindName($"LB_結果{row}_得点") is UIElement el得点2)                   
                    el得点2.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>LB_タイトル4 を表示範囲に応じて設定する。1ページのみの場合はブランク。</summary>
        private void UpdateLBタイトル4(int startIdx, int endIdx)
        {
            var p = PartsLST003;
            if (_pageCount <= 1)
            {
                SetLabel(p, "LB_タイトル4", string.Empty);
                SetVisible(p, "LB_タイトル4", false);
                return;
            }

            var first = _resultList[startIdx];
            var last  = _resultList[endIdx - 1];
            SetLabel(p, "LB_タイトル4",
                $"{DSDspDataHelper.Format順位テキスト(first.順位番号, first.順位表記)} ～ " +
                $"{DSDspDataHelper.Format順位テキスト(last.順位番号, last.順位表記)}");
            SetVisible(p, "LB_タイトル4", true);
        }

        /// <summary>全明細行をフェードアウトして非表示にする（STEPx）</summary>
        private void HideAllRows()
        {
            var p = PartsLST003;
            SetVisible(p, "LB_タイトル4", false);

            if (_partsMain != null)
            {
                var sb = new System.Windows.Media.Animation.Storyboard();
                for (int row = 1; row <= MaxRows; row++)
                    FadeOutRow(row, sb, 0);
                sb.Completed += (_, _) => { for (int row = 1; row <= MaxRows; row++) HideRow(row); };
                sb.Begin();
            }
            else
            {
                for (int row = 1; row <= MaxRows; row++)
                    HideRow(row);
            }
        }

        private void HideRow(int row)
        {
            var p = PartsLST003;
            SetVisible(p, $"IM_明細{row}", false);
            SetVisible(p, $"LB_結果{row}_順位", false);
            SetVisible(p, $"LB_結果{row}_背番号", false);
            SetVisible(p, $"LB_結果{row}_選手名", false);
            SetVisible(p, $"LB_結果{row}_所属", false);
            SetVisible(p, $"LB_結果{row}_得点", false);
        }

        #endregion

        #region カーソル制御

        /// <summary>
        /// カーソルを次に進める。
        /// 1組ずつモード: 次の選手へ、全選手表示済みなら false。
        /// ページ毎モード: 次のページへ、全ページ表示済みなら false。
        /// </summary>
        private bool AdvanceCursor()
        {
            _cursor++;
            if (IsPageMode)
                return _cursor < _pageCount;
            else
                return _cursor < _resultList.Count;
        }

        /// <summary>
        /// 1組ずつモードで、カーソル位置から実際の選手インデックスを取得する。
        /// 昇順: _cursor がそのまま選手インデックス（0=1位）
        /// 降順: _cursor が末尾からのインデックス（0=最終順位）
        /// </summary>
        private int GetDisplayIndex(int cursor)
        {
            if (昇順表示)
                return cursor;
            else
                return _resultList.Count - 1 - cursor;
        }

        #endregion

        #region データ構築

        /// <summary>DV_Result と DA_Master から表示用選手リストを構築する</summary>
        private void BuildResultList()
        {
            _resultList.Clear();
            _cursor = 0;
            _currentPage = 0;

            _isAJS = DSDspDataHelper.IsAJS採点(DV_Result);

            if (DV_Result == null || DA_Master == null) return;

            var 総合結果リスト = DSDspDataHelper.Get総合結果リスト(DV_Result);

            foreach (var (rankNo, bango, score, rankStr) in 総合結果リスト)
            {
                var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, bango, 区分番号);
                string 選手名L = DSDspDataHelper.Get選手名L(選手情報);
                string 選手名P = DSDspDataHelper.Get選手名P(選手情報);
                string 選手名 = string.IsNullOrEmpty(選手名P) ? 選手名L : $"{選手名L}・{選手名P}";
                string 所属 = Build所属テキスト(選手情報);

                _resultList.Add((rankNo, bango, score, rankStr, 選手名, 所属));
            }

            _pageCount = Math.Max(1, (int)Math.Ceiling(_resultList.Count / (double)MaxRows));
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

        #endregion

        #region 初期化

        private void HideAllParts()
        {
            var p = PartsLST003;
            foreach (var name in new[] {
                "IM_タイトル1", "LB_タイトル1",
                "IM_タイトル2", "LB_タイトル2",
                "IM_タイトル3", "LB_タイトル3",
                "LB_タイトル4", "LB_タイトル_Total" })
            {
                SetVisible(p, name, false);
            }
            for (int row = 1; row <= MaxRows; row++)
                HideRow(row);
        }

        #endregion

        #region UI ヘルパー

        private static void SetVisible(FrameworkElement p, string name, bool visible)
        {
            if (p.FindName(name) is UIElement el)
                el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void SetLabel(FrameworkElement p, string name, string text)
        {
            if (p.FindName(name) is Label lb)
                lb.Content = text;
        }

        #endregion
    }
}
