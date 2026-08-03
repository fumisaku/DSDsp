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
    /// DSP_PRG_009_決勝結果_小.xaml の相互作用ロジック
    ///
    /// 使用パーツ: PartsLST004（LST004_右_小リスト8）
    ///   LB_タイトル1   競技会名
    ///   LB_タイトル2   区分名 + ラウンド名
    ///   LB_タイトル3   「決勝進出者」 or 「出場選手一覧」（LB_タイトル4 表示時は非表示）
    ///   LB_タイトル4   ページ範囲（例「1 ～ 10」）、1ページ時はブランク
    ///   LB_結果N_順位  順位テキスト（1→優勝、2→準優勝、3以降→N位）
    ///   LB_結果N_背番号 背番号
    ///   LB_結果N_選手名 L姓+「・」+P姓（苗字のみ）
    ///   LB_結果N_得点  AJS採点 → 総合得点、AJS以外 → 所属（折り返し許可）
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): COM001（競技会名・「表彰式」）、COM002（現在時刻）、COM003 を表示。
    ///                   選手リストを構築。
    ///   STEP2 (case 1): IM_タイトル1-3、LB_タイトル1-3 を表示。
    ///   STEP3 (case 2, 4, 6, ...): LB_タイトル4 と IM_明細/LB_結果 を表示（ページ毎）。
    ///   STEP4 (case 3, 5, 7, ...): STEP3 で表示したものを非表示。
    ///     → 次ページがある場合は STEP3 を繰り返す。
    ///     → この画面を閉じる場合は STEP5 を実行。
    ///   STEP5: STEP2 で表示したものを非表示。TB_左上2 をクリア。→ RaiseScreenCompleted()
    /// </summary>
    public partial class DSP_PRG_009_決勝結果_小 : DSDspScreenBase
    {
        #region 定数
        private const int MaxRows = 10;
        #endregion

        #region フィールド
        private bool _step2Visible = false;
        private bool _closeRequested = false;
        private bool _isAJS = false;
        private int _pageCount = 1;
        /// <summary>
        /// 表示用選手リスト（総合順位番号昇順）。
        /// (順位番号, 背番号, 得点, 順位表記, 選手名, 所属)
        /// </summary>
        private List<(int 順位番号, string 背番号, decimal 得点, string 順位表記, string 選手名, string 所属)> _resultList = new();
        /// <summary>1組ずつモードの現在表示カーソル（選手インデックス）</summary>
        private int _cursor = 0;
        /// <summary>現在の内部フェーズ（1組ずつモード用）: 2=明細表示中, 3=次選手待ち</summary>
        private int _phase = 0;
        #endregion

        #region プロパティ
        /// <summary>所属表示方式。true=カップル所属名優先、false=L所属+"/"+P所属</summary>
        public bool カップル所属表示 { get; set; } = true;

        /// <summary>表示順序。true=昇順（1位から）、false=降順（最終順位から）</summary>
        public bool 昇順表示 { get; set; } = true;

        /// <summary>
        /// ページ毎表示モード。
        /// true=1ページ（最大10行）を一括表示してからページを切り替える（一括）。
        /// false=1組ずつ表示する（昇順/降順）。
        /// </summary>
        public bool IsPageMode { get; set; } = true;

        protected override int TotalSteps => 100;
        #endregion

        #region コンストラクタ
        public DSP_PRG_009_決勝結果_小()
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

            // クロマキ背景色をAppSettingsから設定
            try
            {
                var colorStr = AppSettings.Instance.ChromaKeySettings.BackgroundColor;
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                if (RootGrid != null)
                    RootGrid.Background = new SolidColorBrush(color);
            }
            catch { /* デフォルト（黒）のまま */ }
        }
        #endregion

        #region オーバーライドメソッド
        protected override void ExecuteCurrentStep()
        {
            int s = _currentStep;

            if (s == 0) { Step1(); return; }
            if (s == 1) { Step2(); return; }

            if (IsPageMode)
            {
                // ■ 一括モード: STEP3/STEP4 交互（ページ毎）
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
            else
            {
                // ■ 1組ずつモード
                if (_phase == 2)
                {
                    if (_closeRequested)
                    { Step5(); return; }
                    ShowSingleEntry();
                    _phase = 3;
                }
                else if (_phase == 3)
                {
                    // 次の選手へ（行は維持したまま追加） → カーソルを進めるだけで即座に表示しない
                    _cursor++;
                    if (_closeRequested || _cursor >= _resultList.Count)
                    {
                        // 全選手表示完了 → 明細非表示待ちフェーズへ
                        _phase = 5;
                        return;
                    }
                    // _phase = 2 にして次の Advance() を待つ（即時表示しない）
                    _phase = 2;
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
                    Step5();
                }
            }
        }
        #endregion

        #region ステップ実装

        /// <summary>STEP1: COM001, COM002, COM003 を設定し選手リストを構築</summary>
        private void Step1()
        {
            // COM001: 競技会名
            if (PartsCOM001.FindName("TB_左上1") is TextBlock tb1)
                tb1.Text = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;

            // COM001: TB_左上2 = 区分名
            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
                tb2.Text = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : string.Empty;

            // COM002: 「表彰式」（固定）
            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
                lbRight.Content = "表彰式";

            // COM003: 表彰式では表示しない（クリア）
            if (PartsCOM003.FindName("LB_右上") is Label lb003)
                lb003.Content = string.Empty;

            BuildResultList();
        }

        /// <summary>STEP2: IM_タイトル1-3、LB_タイトル1-3 を表示</summary>
        private void Step2()
        {
            var p = PartsLST004;

            // LB_タイトル1: 競技会名
            SetLabel(p, "LB_タイトル1", DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty);
            SetVisible(p, "IM_タイトル1", true);
            SetVisible(p, "LB_タイトル1", true);

            // LB_タイトル2: 区分名 + ラウンド名
            string kbnName = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : string.Empty;
            string rndName = DA_Master != null ? DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号) : string.Empty;
            SetLabel(p, "LB_タイトル2", $"{kbnName}　{rndName}");
            SetVisible(p, "IM_タイトル2", true);
            SetVisible(p, "LB_タイトル2", true);

            // LB_タイトル3: 「決勝結果」
            string title3 = "決勝結果";
            SetLabel(p, "LB_タイトル3", title3);
            SetVisible(p, "IM_タイトル3", true);
            SetVisible(p, "LB_タイトル3", true);

            _step2Visible = true;

            // 1組ずつモード: フェーズを初期化
            if (!IsPageMode)
            {
                _phase = 2;
            }
        }

        /// <summary>STEP3: 指定ページの選手明細を表示（フェードイン付き）</summary>
        private void Step3(int pageIdx)
        {
            var p = PartsLST004;
            int startIdx = pageIdx * MaxRows;
            int endIdx   = Math.Min(startIdx + MaxRows, _resultList.Count);

            // LB_タイトル4: 2ページ以上の時のみ範囲を表示し、LB_タイトル3 と切り替え
            if (_pageCount > 1)
            {
                SetLabel(p, "LB_タイトル4", $"{startIdx + 1} ～ {endIdx}");
                SetVisible(p, "LB_タイトル4", true);
                SetVisible(p, "LB_タイトル3", false);
            }
            else
            {
                SetLabel(p, "LB_タイトル4", string.Empty);
                SetVisible(p, "LB_タイトル4", false);
            }

            if (_partsMain != null)
            {
                var sb = new Storyboard();
                for (int row = 1; row <= MaxRows; row++)
                {
                    int idx = startIdx + row - 1;
                    if (idx < _resultList.Count)
                    {
                        SetRowLabels(idx, row);
                        FadeInRow(row, sb, (row - 1) * 40);
                    }
                    else
                        HideRow(row);
                }
                sb.Begin();
            }
            else
            {
                for (int row = 1; row <= MaxRows; row++)
                {
                    int idx = startIdx + row - 1;
                    if (idx < _resultList.Count) { SetRowLabels(idx, row); SetRowVisibleInst(row, true); }
                    else HideRow(row);
                }
            }
        }

        /// <summary>STEP4: STEP3 で表示したものをフェードアウトして非表示</summary>
        private void Step4()
        {
            var p = PartsLST004;
            SetVisible(p, "LB_タイトル3", true);
            SetVisible(p, "LB_タイトル4", false);

            if (_partsMain != null)
            {
                var sb = new Storyboard();
                for (int row = 1; row <= MaxRows; row++)
                    FadeOutRow(row, sb, 0);
                sb.Completed += (_, _) => { for (int row = 1; row <= MaxRows; row++) HideRow(row); };
                sb.Begin();
            }
            else
            {
                for (int row = 1; row <= MaxRows; row++) HideRow(row);
            }
        }

        /// <summary>STEP5: STEP2 の表示物を非表示 → ScreenCompleted</summary>
        private void Step5()
        {
            if (!_step2Visible) return;
            var p = PartsLST004;

            foreach (var name in new[] {
                "IM_タイトル1", "LB_タイトル1",
                "IM_タイトル2", "LB_タイトル2",
                "IM_タイトル3", "LB_タイトル3",
                "LB_タイトル4" })
            {
                SetVisible(p, name, false);
            }

            // COM001 の TB_左上2 をクリア
            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
                tb2.Text = string.Empty;

            _step2Visible = false;
            RaiseScreenCompleted();
        }

        #endregion

        #region 公開メソッド

        /// <summary>画面を閉じる要求。次の STEP3 相当タイミングで STEP5 を実行する。</summary>
        public void RequestClose() => _closeRequested = true;

        #endregion

        #region データ構築

        /// <summary>DV_Result と DA_Master から表示用選手リストを構築する</summary>
        private void BuildResultList()
        {
            _resultList.Clear();
            _cursor = 0;
            _phase = 0;

            _isAJS = DSDspDataHelper.IsAJS採点(DV_Result);

            if (DV_Result == null || DA_Master == null) return;

            var 総合結果リスト = DSDspDataHelper.Get総合結果リスト(DV_Result);

            foreach (var (rankNo, bango, score, rankStr) in 総合結果リスト)
            {
                var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, bango, 区分番号);

                // 苗字のみ取得
                string lName  = DSDspDataHelper.Get選手名L(選手情報);
                string pName  = DSDspDataHelper.Get選手名P(選手情報);
                string l苗字  = GetFamilyName(lName);
                string p苗字  = GetFamilyName(pName);
                string 選手名 = string.IsNullOrEmpty(p苗字) ? l苗字 : $"{l苗字}・{p苗字}";

                string 所属 = Build所属テキスト(選手情報);

                _resultList.Add((rankNo, bango, score, rankStr, 選手名, 所属));
            }

            _pageCount = Math.Max(1, (int)Math.Ceiling(_resultList.Count / (double)MaxRows));
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

        /// <summary>1組ずつモード: 現在のカーソル位置の選手を1行表示する（フェードイン付き）</summary>
        private void ShowSingleEntry()
        {
            int playerIdx = 昇順表示 ? _cursor : (_resultList.Count - 1 - _cursor);
            if (playerIdx < 0 || playerIdx >= _resultList.Count) return;

            // 降順: 下の行から埋める
            int row;
            if (!昇順表示)
            {
                int pageIdx   = _cursor / MaxRows;
                int pageStart = pageIdx * MaxRows;
                int pageEnd   = Math.Min(pageStart + MaxRows, _resultList.Count);
                int pageSize  = pageEnd - pageStart;
                int posInPage = _cursor % MaxRows;
                row = pageSize - posInPage; // 最下位が最後行、最上位が1行目
            }
            else
            {
                row = _cursor % MaxRows + 1;
            }

            SetRowLabels(playerIdx, row);

            if (_partsMain != null)
            {
                var sb = new Storyboard();
                FadeInRow(row, sb, 0);
                sb.Begin();
            }
            else
            {
                SetRowVisibleInst(row, true);
            }

            // LB_タイトル4: ページ先頭で範囲表示（2ページ以上の場合のみ）
            if (_cursor % MaxRows == 0 && _pageCount > 1)
            {
                int pageIdx = _cursor / MaxRows;
                int startIdx = pageIdx * MaxRows;
                int endIdx   = Math.Min(startIdx + MaxRows, _resultList.Count);
                SetLabel(PartsLST004, "LB_タイトル4", $"{startIdx + 1} ～ {endIdx}");
                SetVisible(PartsLST004, "LB_タイトル4", true);
                SetVisible(PartsLST004, "LB_タイトル3", false);
            }
        }

        /// <summary>全明細行をフェードアウトして非表示にする</summary>
        private void HideAllRows()
        {
            if (_partsMain != null)
            {
                var sb = new Storyboard();
                for (int row = 1; row <= MaxRows; row++)
                    FadeOutRow(row, sb, 0);
                sb.Completed += (_, _) => { for (int row = 1; row <= MaxRows; row++) HideRow(row); };
                sb.Begin();
            }
            else
            {
                for (int row = 1; row <= MaxRows; row++) HideRow(row);
            }
        }

        /// <summary>指定行の要素をフェードインするアニメーションをStoryboardに追加</summary>
        private void FadeInRow(int row, Storyboard sb, int delayMs)
        {
            var p = PartsLST004;
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

        /// <summary>指定行の要素をフェードアウトするアニメーションをStoryboardに追加</summary>
        private void FadeOutRow(int row, Storyboard sb, int delayMs)
        {
            var p = PartsLST004;
            foreach (var name in GetRowElementNames(row))
            {
                if (p.FindName(name) is UIElement el && el.Visibility == Visibility.Visible)
                    _partsMain!.フェードアウト(true, el, sb, delayMs);
            }
        }

        /// <summary>行を構成するUIElement名の一覧を返す</summary>
        private static IEnumerable<string> GetRowElementNames(int row)
        {
            yield return $"IM_明細{row}";
            yield return $"LB_結果{row}_順位";
            yield return $"LB_結果{row}_背番号";
            yield return $"LB_結果{row}_選手名";
            yield return $"LB_結果{row}_得点";
        }


        /// <summary>指定行の要素に選手データをセットする（表示状態は変えない）</summary>
        private void SetRowLabels(int playerIdx, int row)
        {
            var p = PartsLST004;
            var entry = _resultList[playerIdx];
            string rankText = DSDspDataHelper.Format順位テキスト(entry.順位番号, entry.順位表記);
            SetLabel(p, $"LB_結果{row}_順位", rankText);
            SetLabel(p, $"LB_結果{row}_背番号", entry.背番号);
            SetLabel(p, $"LB_結果{row}_選手名", entry.選手名);
            string 得点テキスト = _isAJS ? entry.得点.ToString("F3") : entry.所属;
            SetLabel(p, $"LB_結果{row}_得点", 得点テキスト);
        }

        /// <summary>指定行を即座に表示/非表示にする</summary>
        private void SetRowVisibleInst(int row, bool visible)
        {
            var p = PartsLST004;
            foreach (var name in GetRowElementNames(row))
                SetVisible(p, name, visible);
        }

        /// <summary>指定行を非表示にする</summary>
        private void HideRow(int row)
        {
            SetRowVisibleInst(row, false);
        }

        #endregion

        #region 初期化

        private void HideAllParts()
        {
            var p = PartsLST004;
            foreach (var name in new[] {
                "IM_タイトル1", "LB_タイトル1",
                "IM_タイトル2", "LB_タイトル2",
                "IM_タイトル3", "LB_タイトル3",
                "LB_タイトル4" })
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
