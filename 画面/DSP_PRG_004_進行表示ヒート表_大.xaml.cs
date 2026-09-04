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
    /// DSP_PRG_004_進行表示ヒート表_大.xaml の相互作用ロジック
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): COM001, COM002 を表示。ヒートデータを事前計算。
    ///   STEP2 (case 1): LB_タイトル1/2, LB_種目, IM各種、LB_次, LB_次_明細, LB_次_時刻 を表示
    ///   STEP3 (case 2〜): ヒート表示
    ///     一括モード: 全ヒートを8行以内で一度に表示（1回のみ）
    ///     ヒート毎更新モード: 現在ヒートから最大8行を一括表示（1回のみ）
    ///   STEP4: STEP3 で表示したものを非表示
    ///   STEP5: STEP2 で表示したものを非表示 → RaiseScreenCompleted
    ///
    /// 【区分・ラウンドが進む場合】
    ///   STEP4 → STEP2（新区分・ラウンド表示）→ STEP3（ヒート表示）→ STEP4 → …
    ///
    /// 【同一区分・ラウンド内でヒートだけ更新する場合】
    ///   STEP4 → STEP3 → STEP4 → …
    ///
    /// 本実装では単一区分・ラウンドを対象とし、STEP4→STEP2遷移は行わない。
    /// 全ヒート終了後に STEP4→STEP5（STEP2非表示）→完了。
    /// </summary>
    public partial class DSP_PRG_004_進行表示ヒート表_大 : DSDspScreenBase
    {
        #region 定数
        private const int MaxRows = 8;
        private static readonly Brush CurrentBrush  = new SolidColorBrush(Color.FromRgb(204, 85, 0));  // 濃いオレンジ
        private static readonly Brush DefaultBrush  = new SolidColorBrush(Color.FromRgb(0, 0, 139));   // ダークブルー
        private const string FontFamilyName = "Segoe UI Semibold";
        #endregion

        #region フィールド
        /// <summary>表示するヒートの順序リスト (種目番号, ヒート番号)。一括モードは(-1,-1)の1エントリ。</summary>
        private List<(int DncNo, int HeatNo)> _heatSequence = new();
        /// <summary>種目ごとにヒート配置が異なるか（ヒートシャッフルあり）</summary>
        private bool _isShuffled = false;
        /// <summary>非シャッフルかつ8行以内 → 全ヒート一括表示モード</summary>
        private bool _isBulk = false;
        /// <summary>全種目ヒート背番号マップ: 種目番号 → (ヒート番号 → 背番号リスト)</summary>
        private Dictionary<int, Dictionary<int, List<string>>> _heatMap = new();
        /// <summary>全種目リスト (DncNo, DncCd)</summary>
        private List<(int DncNo, string DncCd)> _danceList = new();
        /// <summary>STEP2 を表示中かどうか</summary>
        private bool _step2Visible = false;
        /// <summary>ヒートシーケンスのカーソル位置</summary>
        private int _heatCursor = 0;
        /// <summary>一括表示モード専用: 現在表示中のヒートキー配列インデックス（0始まり）。-1=未設定。</summary>
        private int _bulkHeatIdx = -1;
        /// <summary>内部ステートマシンのフェーズ: 0=未開始, 1=STEP1完了, 2=STEP2表示中, 3=STEP3表示中, 4=完了</summary>
        private int _phase = 0;
        /// <summary>FadeOutAllHeatRows が実行中かどうか（重複実行防止フラグ）</summary>
        private bool _isFadingOut = false;
        /// <summary>フェードアウト完了後に実行する保留コールバック（_isFadingOut=true 中に来た要求を退避）</summary>
        private Action? _pendingFadeOutCallback = null;
        #endregion

        #region プロパティ
        // TotalSteps は 100（動的ステップ）。RaiseScreenCompleted で終了管理。
        protected override int TotalSteps => 100;
        #endregion

        #region コンストラクタ
        public DSP_PRG_004_進行表示ヒート表_大()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }
        #endregion

        #region イベントハンドラ
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // _partsMain の初期化のみ行う。
            // HideAllParts() は DoStep1() の先頭で呼ぶことにしたため、ここでは呼ばない。
            // Loaded イベントが Advance()/NotifyHeatChanged() より後に遅延発火した場合に
            // タイトル・ヒート行を消去してしまう問題を根本から防ぐため。
            EnsurePartsMainInitialized();
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

        /// <summary>
        /// HeatEnd 通知受信時に MainWindow から呼ばれる。
        /// フェーズが不完全（ヒート未表示）な場合は補完してからヒートを切り替える。
        ///   _phase == 0 → DoStep1 → DoStep2 → DoShowHeat(_phase=3) → 次ヒートFadeOut再表示
        ///   _phase == 1 → DoStep2 → DoShowHeat(_phase=3) → 次ヒートFadeOut再表示
        ///   _phase == 2 → DoShowHeat(_phase=3) → 次ヒートFadeOut再表示
        ///   _phase == 3 → 通常通り FadeOut → 再表示（既存動作と同じ）
        ///   _phase == 4 → DoStep5 完了後（全ヒート終了済み）でも再度 Step1/Step2 からやり直す
        /// </summary>
        public override void NotifyHeatChanged()
        {
            // フェーズを 3（ヒート表示完了）まで補完する
            if (_phase == 0 || _phase == 4)
            {
                // _phase == 4 は DoStep5 実行後（画面完了通知済み）の状態。
                // 次のヒートが来た場合は STEP1/STEP2 からやり直す。
                DoStep1();
                DoStep2();
                // 一括モードのみ即時表示（ヒート毎更新モードはFadeOut後に表示）
                if (_isBulk) ShowBulkHeatsImmediate();
                _phase = 3;
            }
            else if (_phase == 1)
            {
                DoStep2();
                if (_isBulk) ShowBulkHeatsImmediate();
                _phase = 3;
            }
            else if (_phase == 2)
            {
                if (_isBulk) ShowBulkHeatsImmediate();
                _phase = 3;
            }

            // _phase == 3 の場合（通常ケース）: FadeOut → 再表示
            FadeOutAllHeatRows(() =>
            {
                PrepareHeatData();
                // FadeOutAllHeatRows が保留（_pendingFadeOutCallback）されている間に
                // DoStep3OrLater → DoStep5() が実行されてタイトル部が非表示になっている場合、
                // _step2Visible=false になる。この場合は DoStep2() でタイトルを復元する。
                if (!_step2Visible)
                {
                    DoStep1();
                    DoStep2();
                }
                UpdateLB種目();
                // _heatCursor は PrepareHeatData() 内で現在ヒートに合わせて設定済み。
                // 一括モードのみ 0 に固定（PrepareHeatData の初期値が 0 なので変更不要）。
                // ヒート毎更新モードでは上書きしない。
                if (_isBulk) _heatCursor = 0;
                DoShowHeat();
                _phase = 3;
            });
        }

        /// <summary>
        /// 指定の種目番号・ヒート番号にジャンプして表示する。
        /// マニュアルでコンボを変更してから再生ボタンを押した場合に使用する。
        ///
        /// - _isBulk=true（単一種目一括表示）の場合は _bulkHeatIdx をジャンプ先に合わせて再表示する。
        /// - _isBulk=false（ヒート毎更新モード）の場合は _heatSequence からジャンプ先を探して
        ///   _heatCursor をセットし再表示する。
        /// - _phase が未完了（0/1/2/4）の場合はフェーズを補完してから表示する。
        /// - 指定の種目・ヒートが _heatSequence に存在しない場合は通常の Advance() にフォールバックする。
        /// </summary>
        public void JumpToHeat(int dncNo, int heatNo)
        {
            // フェーズを最低限 STEP2 完了（_phase>=2）まで補完する
            if (_phase == 0 || _phase == 4)
            {
                PrepareHeatData();
                DoStep1();
                DoStep2();
                _phase = 2;
            }
            else if (_phase == 1)
            {
                PrepareHeatData();
                DoStep2();
                _phase = 2;
            }

            if (_isBulk)
            {
                // 一括表示モード: _heatMap の代表種目でジャンプ先ヒートを探す
                var firstDnc = _danceList.FirstOrDefault();
                if (firstDnc.DncNo > 0 && _heatMap.TryGetValue(firstDnc.DncNo, out var hd))
                {
                    var heatKeys = hd.Keys.OrderBy(n => n).ToList();
                    int jumpIdx  = heatKeys.IndexOf(heatNo);
                    if (jumpIdx >= 0)
                    {
                        _bulkHeatIdx = jumpIdx;
                        ヒート番号   = heatNo;
                        FadeOutAllHeatRows(() =>
                        {
                            UpdateLB種目();
                            _heatCursor = 0;
                            DoShowHeat();
                            _phase = 3;
                        });
                        return;
                    }
                }
            }
            else
            {
                // ヒート毎更新モード: _heatSequence からジャンプ先を探す
                int jumpCursor = _heatSequence.FindIndex(e => e.DncNo == dncNo && e.HeatNo == heatNo);
                if (jumpCursor >= 0)
                {
                    _heatCursor = jumpCursor;
                    種目番号    = dncNo;
                    ヒート番号  = heatNo;
                    FadeOutAllHeatRows(() =>
                    {
                        UpdateLB種目();
                        DoShowHeat();
                        _phase = 3;
                    });
                    return;
                }
            }

            // 指定位置が見つからない場合は通常の Advance() にフォールバック
            Advance();
        }
        #endregion

        #region ステップ実装

        /// <summary>STEP1: COM001, COM002 を表示。ヒートデータ事前計算。</summary>
        private void DoStep1()
        {
            // 前回画面のゴミデータをクリアする。
            // Loaded イベントではなくここで呼ぶことで、Loaded の遅延発火タイミングに依存しない。
            EnsurePartsMainInitialized();
            HideAllParts();

            // COM001: 競技会名
            if (DA_Master != null)
            {
                if (PartsCOM001.FindName("TB_左上1") is System.Windows.Controls.TextBlock tb1)
                    tb1.Text = DSDspDataHelper.Get競技会名(DA_Master);
                if (PartsCOM001.FindName("TB_左上2") is System.Windows.Controls.TextBlock tb2)
                    tb2.Text = string.Empty;
            }

            // COM002: 現在時刻
            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
                lbRight.Content = $"現在時刻　{DateTime.Now:HH:mm}";
            StartClock();

            // COM003: クリア（前の画面のゴミを消す）
            PartsCOM003.LB_右上.Content = string.Empty;

            // ヒートデータ事前計算
            PrepareHeatData();
            _phase = 1;
        }

        /// <summary>STEP2: タイトル・種目・次の競技を表示</summary>
        private void DoStep2()
        {
            EnsurePartsMainInitialized();
            var p = PartsPRG004;

            // LB_タイトル1 と背景イメージ
            SetLabelContent(p, "LB_タイトル1", "現在の競技");
            SetVisible(p, "IM_タイトル1", true);
            SetVisible(p, "LB_タイトル1", true);

            // LB_タイトル2: 進行番号 + 区分名 [+ 種目GRP名] + ラウンド名
            // 同一区分・ラウンドに種目GRPが複数ある場合は種目GRP名を挿入する
            string prgNo   = DSDspDataHelper.Get現在進行番号(DS_Status, 区分番号, ラウンド番号, DGrpNo);
            string kbnName = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : string.Empty;
            string rndName = DA_Master != null ? DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号) : string.Empty;
            int dgrpCount  = DA_Master != null ? DSDspDataHelper.GetDGRP数(DA_Master, 区分番号, ラウンド番号) : 0;
            string dgrpName = (dgrpCount > 1 && DA_Master != null)
                ? DSDspDataHelper.GetDGRP名(DA_Master, 区分番号, ラウンド番号, DGrpNo)
                : string.Empty;
            string title2Text = string.IsNullOrEmpty(dgrpName)
                ? $"{prgNo}　{kbnName}　{rndName}"
                : $"{prgNo}　{kbnName}　{dgrpName}　{rndName}";
            SetLabelContent(p, "LB_タイトル2", title2Text);
            // フォントサイズ自動調整（区分名が長い場合に縮小）
            // LB_タイトル2(Canvas.Left=7, Width=412)の右隣にLB_種目(Canvas.Left=403)があるため
            // 実効幅は 403-7=396px に制限して確実に収める
            if (p.FindName("LB_タイトル2") is Label lbTitle2)
                _partsMain?.フォントサイズ自動調整(lbTitle2, title2Text, maxWidth: 396, maxFontSize: 16, minFontSize: 8, fontFamilyName: FontFamilyName);
            SetVisible(p, "IM_タイトル2", true);
            SetVisible(p, "LB_タイトル2", true);

            // LB_種目: 全種目記号を半角スペース区切り（現在種目=赤、その他=白）
            UpdateLB種目();
            SetVisible(p, "LB_種目", true);

            // 次の競技ヘッダ
            SetLabelContent(p, "LB_次", "次の競技");
            SetVisible(p, "IM_次", true);
            SetVisible(p, "LB_次", true);
            SetVisible(p, "IM_次_明細", true);

            // 次の競技明細
            var next = DSDspDataHelper.Get次進行情報(DS_Status, 区分番号, ラウンド番号, DGrpNo);
            if (next.HasValue && DA_Master != null)
            {
                string nextKbnName = DSDspDataHelper.Get区分名(DA_Master, next.Value.KbnNo);
                string nextRndName = DSDspDataHelper.Getラウンド名(DA_Master, next.Value.KbnNo, next.Value.RndNo);
                int nextDgrpCount  = DSDspDataHelper.GetDGRP数(DA_Master, next.Value.KbnNo, next.Value.RndNo);
                string nextDgrpName = string.Empty;
                if (nextDgrpCount > 1)
                {
                    string nextDgrpNo = DSDspDataHelper.GetDGrpNoFromPrgNo(DS_Status, next.Value.PrgNo);
                    nextDgrpName = DSDspDataHelper.GetDGRP名(DA_Master, next.Value.KbnNo, next.Value.RndNo, nextDgrpNo);
                }
                string next明細Text = string.IsNullOrEmpty(nextDgrpName)
                    ? $"{next.Value.PrgNo}　{nextKbnName}　{nextRndName}"
                    : $"{next.Value.PrgNo}　{nextKbnName}　{nextDgrpName}　{nextRndName}";
                SetLabelContent(p, "LB_次_明細", next明細Text);
                // フォントサイズ自動調整（LB_次_明細: Canvas.Left=20, LB_次_時刻: Canvas.Left=372 → 実効幅 372-20=352px）
                // LB_次_時刻 と重ならないよう幅を制限。最小6pt まで縮小し、それでも収まらない場合は後ろをカット。
                if (p.FindName("LB_次_明細") is Label lbNext明細)
                {
                    _partsMain?.フォントサイズ自動調整(lbNext明細, next明細Text, maxWidth: 352, maxFontSize: 16, minFontSize: 6, fontFamilyName: FontFamilyName);
                    // 最小フォントサイズでも収まらない場合はテキストを後ろからカット
                    if (lbNext明細.FontSize <= 6)
                    {
                        string truncated = next明細Text;
                        while (truncated.Length > 0)
                        {
                            double w = _partsMain?.テキスト幅取得(truncated, FontFamilyName, 6, lbNext明細) ?? 0;
                            if (w <= 352) break;
                            truncated = truncated[..^1];
                        }
                        lbNext明細.Content = truncated;
                    }
                }
                SetVisible(p, "LB_次_明細", true);

                // 次の競技開始予定時刻（データがない場合はブランク・非表示）
                if (!string.IsNullOrEmpty(next.Value.PStaTM))
                {
                    SetLabelContent(p, "LB_次_時刻", $"開始予定　{DSDspDataHelper.ExtractTimeOnly(next.Value.PStaTM)}");
                    SetVisible(p, "LB_次_時刻", true);
                }
                else
                {
                    SetLabelContent(p, "LB_次_時刻", string.Empty);
                    SetVisible(p, "LB_次_時刻", false);
                }
            }
            else
            {
                SetLabelContent(p, "LB_次_明細", string.Empty);
                SetLabelContent(p, "LB_次_時刻", string.Empty);
                SetVisible(p, "LB_次_明細", false);
                SetVisible(p, "LB_次_時刻", false);
            }

            _step2Visible = true;
            _phase = 2;
        }

        /// <summary>STEP3以降: フェーズに応じて表示/非表示/完了を制御</summary>
        private void DoStep3OrLater()
        {
            if (_phase == 2)
            {
                // STEP3: ヒートを表示する（フェードイン開始）
                DoShowHeat();
                _phase = 3;
            }
            else if (_phase == 3)
            {
                // STEP4: ヒートをフェードアウトして非表示にする
                // 完了後に次フェーズへ進む
                FadeOutAllHeatRows(() =>
                {
                    if (_isBulk)
                    {
                        // 一括表示モード: 手動再生ボタンで次ヒートへ進める。
                        // _bulkHeatIdx（ShowBulkHeats/ShowBulkHeatsImmediateで設定）を使って
                        // 現在表示中のヒートを確実に把握し、次のインデックスへ進む。
                        // DS_Status や ヒート番号プロパティに依存しないため誤動作を防げる。
                        var firstDnc = _danceList.FirstOrDefault();
                        if (firstDnc.DncNo > 0 && _heatMap.TryGetValue(firstDnc.DncNo, out var hd))
                        {
                            var heatKeys = hd.Keys.OrderBy(n => n).ToList();
                            int nextIdx = _bulkHeatIdx + 1;
                            if (_bulkHeatIdx >= 0 && nextIdx < heatKeys.Count)
                            {
                                // 次ヒートへ: ヒート番号を更新して再表示
                                ヒート番号 = heatKeys[nextIdx];
                                _bulkHeatIdx = nextIdx;
                                UpdateLB種目();
                                _heatCursor = 0;
                                DoShowHeat();
                                _phase = 3;
                            }
                            else
                            {
                                // 現在ヒートが最後（または未初期化） → 完了
                                DoStep5();
                            }
                        }
                        else
                        {
                            // データ取得失敗 → 完了
                            DoStep5();
                        }
                    }
                    else
                    {
                        _heatCursor++;
                        if (_heatCursor < _heatSequence.Count)
                        {
                            // ヒート毎更新モード: 次ヒートをフェードアウト後即座に表示する。
                            // _phase=2 のままにすると次の Advance() まで何も表示されず
                            // 操作者が再生ボタンを余分に1回押す必要が生じるため、
                            // ここで直接 DoShowHeat() を呼んで表示まで完了させる。
                            UpdateLB種目();
                            DoShowHeat();
                            _phase = 3;
                        }
                        else
                            DoStep5();
                    }
                });
            }
        }

        /// <summary>現在のヒートカーソルに対応するヒートを表示</summary>
        private void DoShowHeat()
        {
            if (_isBulk)
            {
                // 一括表示モード: 全ヒートを最大8行で表示
                ShowBulkHeats();
            }
            else
            {
                // ヒート毎更新モード: 現在ヒートから最大8行一括表示
                if (_heatCursor < _heatSequence.Count)
                {
                    ShowHeatsFromCursor();
                    // 種目ラベルの現在状態更新
                    UpdateLB種目();
                }
            }
        }

        /// <summary>STEP5: STEP2 の全表示要素を非表示にして完了通知</summary>
        private void DoStep5()
        {
            if (!_step2Visible) return;
            var p = PartsPRG004;

            foreach (var name in new[] {
                "LB_タイトル1", "IM_タイトル1",
                "LB_タイトル2", "IM_タイトル2",
                "LB_種目",
                "LB_次", "IM_次",
                "LB_次_明細", "IM_次_明細",
                "LB_次_時刻" })
            {
                SetVisible(p, name, false);
            }

            _step2Visible = false;
            _phase = 4;
            RaiseScreenCompleted();
        }

        #endregion

        #region ヒートデータ準備

        private void PrepareHeatData()
        {
            _heatSequence.Clear();
            _heatMap.Clear();
            _danceList.Clear();
            _heatCursor = 0;
            _bulkHeatIdx = -1;

            if (DS_Status == null || DA_Master == null) return;

            _danceList = DSDspDataHelper.Get全種目リスト(DA_Master, 区分番号, ラウンド番号, DGrpNo);
            _heatMap   = DSDspDataHelper.Get全種目全ヒート背番号マップ(DS_Status, 区分番号, ラウンド番号, DGrpNo);

            // シャッフル判定（情報保持のみ。_heatSequence 構築には使わない）
            _isShuffled = CheckIsShuffled();

            // _heatSequence を構築する。
            // 各種目のヒート一覧はそれぞれ _heatMap から個別に取得する。
            // （種目ごとにヒート数が異なる場合があるため、代表種目だけで判断しない）
            //
            // 一括表示モード（_isBulk=true）の条件:
            //   種目が1つのみ かつ そのヒート数が MaxRows 以内
            //   → 全ヒートを1画面に一括表示してハイライトのみ変化させる方式。
            //
            // ヒート毎更新モード（_isBulk=false）:
            //   上記以外（複数種目 / シャッフルあり / 1種目でもヒート数超過）
            //   → 種目ごと・ヒートごとに S 1H → S 2H → C 1H → C 2H → ... の順で順次表示する。

            if (_danceList.Count == 1)
            {
                // 単一種目: 一括表示モードを検討する
                var only = _danceList[0];
                if (_heatMap.TryGetValue(only.DncNo, out var onlyHeats) && onlyHeats.Count <= MaxRows)
                {
                    // 単一種目かつ MaxRows 以内 → 一括表示モード
                    _isBulk = true;
                    _heatSequence.Add((only.DncNo, -1));
                }
                else
                {
                    // 単一種目だがヒート数超過 → ヒート毎更新モード
                    _isBulk = false;
                    if (_heatMap.TryGetValue(only.DncNo, out var d))
                        foreach (var hNo in d.Keys.OrderBy(n => n))
                            _heatSequence.Add((only.DncNo, hNo));
                }
            }
            else
            {
                // 複数種目: 種目ごとにヒート一覧を個別取得して順次列挙
                // シャッフルの有無に関わらず S 1H → S 2H → C 1H → C 2H → ... の順にする
                _isBulk = false;
                foreach (var (dncNo, _) in _danceList)
                {
                    if (!_heatMap.TryGetValue(dncNo, out var dncHeats)) continue;
                    foreach (var hNo in dncHeats.Keys.OrderBy(n => n))
                        _heatSequence.Add((dncNo, hNo));
                }
            }

            // _heatCursor を現在ヒートの位置に合わせる（現在ヒートが先頭行になるよう）
            // 一括モードは _heatCursor=0 固定でよい
            if (!_isBulk && _heatSequence.Count > 0)
            {
                int curDncNo  = GetCurrentDncNo();
                int curHeatNo = GetCurrentHeatNo();
                int found = _heatSequence.FindIndex(e => e.DncNo == curDncNo && e.HeatNo == curHeatNo);
                if (found >= 0)
                    _heatCursor = found;
            }
        }

        /// <summary>
        /// 全種目で出場選手のヒート配置が同一かを確認する。
        /// 種目ごとにヒート数・各ヒートの選手セットが全て一致する場合はシャッフルなし（false）。
        /// </summary>
        private bool CheckIsShuffled()
        {
            if (_danceList.Count <= 1) return false;

            var firstDnc = _danceList[0];
            if (!_heatMap.TryGetValue(firstDnc.DncNo, out var firstHeatDic)) return false;

            foreach (var (dncNo, _) in _danceList)
            {
                if (dncNo == firstDnc.DncNo) continue;
                if (!_heatMap.TryGetValue(dncNo, out var heatDic)) return true;
                if (heatDic.Count != firstHeatDic.Count) return true;

                foreach (var hNo in firstHeatDic.Keys)
                {
                    if (!heatDic.TryGetValue(hNo, out var players)) return true;
                    if (!new HashSet<string>(firstHeatDic[hNo]).SetEquals(new HashSet<string>(players)))
                        return true;
                }
            }
            return false;
        }

        #endregion

        #region ヒート表示ロジック

        /// <summary>
        /// 一括表示モード: 全ヒートを最大8行に即時表示（アニメーションなし）。
        /// フェーズ補完時（NotifyHeatChanged 内）に使用する。
        /// </summary>
        private void ShowBulkHeatsImmediate()
        {
            var p = PartsPRG004;
            var firstDnc = _danceList.FirstOrDefault();
            if (firstDnc.DncNo <= 0 || !_heatMap.TryGetValue(firstDnc.DncNo, out var heatDic)) return;

            int curHeatNo = GetCurrentHeatNo();
            var heatKeys = heatDic.Keys.OrderBy(n => n).ToList();

            for (int rowIdx = 0; rowIdx < heatKeys.Count && rowIdx < MaxRows; rowIdx++)
            {
                int hNo = heatKeys[rowIdx];
                int row = rowIdx + 1;

                bool isCur = (hNo == curHeatNo);
                SetLabelContent(p, $"LB_ヒート{row}", $"{hNo}H");
                SetLabelColor(p, $"LB_ヒート{row}", isCur ? CurrentBrush : DefaultBrush);
                SetOpacity(p, $"IM_ヒート{row}", 1); SetVisible(p, $"IM_ヒート{row}", true);
                SetOpacity(p, $"LB_ヒート{row}", 1); SetVisible(p, $"LB_ヒート{row}", true);

                var players = heatDic[hNo];
                SetLabelContent(p, $"LB_背番号{row}", FormatPlayers(players));
                SetLabelColor(p, $"LB_背番号{row}", isCur ? CurrentBrush : DefaultBrush);
                SetOpacity(p, $"IM_明細{row}", 1); SetVisible(p, $"IM_明細{row}", true);
                SetOpacity(p, $"LB_背番号{row}", 1); SetVisible(p, $"LB_背番号{row}", true);

                // 現在ヒートのインデックスを記録
                if (isCur) _bulkHeatIdx = rowIdx;
            }
            // curHeatNoが見つからなかった場合は最初のヒートを現在とみなす
            if (_bulkHeatIdx < 0 && heatKeys.Count > 0) _bulkHeatIdx = 0;
        }

        /// <summary>
        /// 一括表示モード: 全ヒートを最大8行にフェードインで表示。
        /// IM_ヒート/IM_明細 を行ごとに遅延フェードイン → 完了後に LB をフェードイン。
        /// </summary>
        private void ShowBulkHeats()
        {
            var p = PartsPRG004;
            var firstDnc = _danceList.FirstOrDefault();
            // _heatMap にデータが無い場合（DS_Status 更新後など）は再計算する
            if (!_heatMap.TryGetValue(firstDnc.DncNo, out var heatDic))
            {
                PrepareHeatData();
                firstDnc = _danceList.FirstOrDefault();
                if (!_heatMap.TryGetValue(firstDnc.DncNo, out heatDic)) return;
            }

            int curHeatNo = GetCurrentHeatNo();
            var heatKeys  = heatDic.Keys.OrderBy(n => n).ToList();

            // 表示データを先にラベルへセット（Opacity=0 で非表示）
            var usedRows = new List<int>();
            for (int rowIdx = 0; rowIdx < heatKeys.Count && rowIdx < MaxRows; rowIdx++)
            {
                int hNo = heatKeys[rowIdx];
                int row = rowIdx + 1;
                usedRows.Add(row);

                bool isCur = (hNo == curHeatNo);
                SetLabelContent(p, $"LB_ヒート{row}", $"{hNo}H");
                SetOpacity(p, $"IM_ヒート{row}", 0); SetVisible(p, $"IM_ヒート{row}", true);
                SetOpacity(p, $"LB_ヒート{row}", 0); SetVisible(p, $"LB_ヒート{row}", true);
                SetLabelColor(p, $"LB_ヒート{row}", isCur ? CurrentBrush : DefaultBrush);

                var players = heatDic[hNo];
                SetLabelContent(p, $"LB_背番号{row}", FormatPlayers(players));
                SetLabelColor(p, $"LB_背番号{row}", isCur ? CurrentBrush : DefaultBrush);
                SetOpacity(p, $"IM_明細{row}", 0); SetVisible(p, $"IM_明細{row}", true);
                SetOpacity(p, $"LB_背番号{row}", 0); SetVisible(p, $"LB_背番号{row}", true);

                // 現在ヒートのインデックスを記録
                if (isCur) _bulkHeatIdx = rowIdx;
            }
            // curHeatNoが見つからなかった場合は最初のヒートを現在とみなす
            if (_bulkHeatIdx < 0 && heatKeys.Count > 0) _bulkHeatIdx = 0;

            FadeInHeatRows(p, usedRows);
        }

        /// <summary>
        /// ヒート毎更新モード: 現在ヒートカーソルから最大8行をフェードインで表示。
        /// </summary>
        private void ShowHeatsFromCursor()
        {
            var p = PartsPRG004;
            int curDncNo  = GetCurrentDncNo();
            int curHeatNo = GetCurrentHeatNo();

            int rowIdx = 0;
            var usedRows = new List<int>();
            for (int i = _heatCursor; i < _heatSequence.Count && rowIdx < MaxRows; i++)
            {
                var (dncNo, heatNo) = _heatSequence[i];
                if (!_heatMap.TryGetValue(dncNo, out var heatDic)) continue;
                if (!heatDic.TryGetValue(heatNo, out var players)) continue;

                var dance = _danceList.FirstOrDefault(d => d.DncNo == dncNo);
                string dncCd = dance.DncCd ?? string.Empty;
                bool isCurrent = (dncNo == curDncNo && heatNo == curHeatNo);

                int row = rowIdx + 1;
                usedRows.Add(row);

                SetLabelContent(p, $"LB_ヒート{row}", $"{dncCd} {heatNo}H");
                SetOpacity(p, $"IM_ヒート{row}", 0); SetVisible(p, $"IM_ヒート{row}", true);
                SetOpacity(p, $"LB_ヒート{row}", 0); SetVisible(p, $"LB_ヒート{row}", true);
                SetLabelColor(p, $"LB_ヒート{row}", isCurrent ? CurrentBrush : DefaultBrush);

                SetLabelContent(p, $"LB_背番号{row}", FormatPlayers(players));
                SetLabelColor(p, $"LB_背番号{row}", isCurrent ? CurrentBrush : DefaultBrush);
                SetOpacity(p, $"IM_明細{row}", 0); SetVisible(p, $"IM_明細{row}", true);
                SetOpacity(p, $"LB_背番号{row}", 0); SetVisible(p, $"LB_背番号{row}", true);

                rowIdx++;
            }

            FadeInHeatRows(p, usedRows);
        }

        /// <summary>
        /// ヒート行リストをフェードインする。
        /// DSP_GRP_001 と同じパターン：
        ///   IM_ヒート と IM_明細 を行ごとに 100ms 遅延でフェードイン
        ///   → 完了後に LB_ヒート と LB_背番号 を一斉フェードイン
        /// </summary>
        private void FadeInHeatRows(パーツ.PRG004_ヒート表示_大 p, List<int> rows)
        {
            if (_partsMain == null || rows.Count == 0) return;

            int 間隔ms = rows.Count > 1 ? Math.Max(1000 / rows.Count, 100) : 0;
            var imSb = new Storyboard();
            for (int i = 0; i < rows.Count; i++)
            {
                int row = rows[i];
                if (p.FindName($"IM_ヒート{row}") is UIElement imH)
                    _partsMain.フェードイン(true, imH, imSb, i * 間隔ms);
                if (p.FindName($"IM_明細{row}") is UIElement imM)
                    _partsMain.フェードイン(true, imM, imSb, i * 間隔ms);
            }

            imSb.Completed += (s, e) =>
            {
                var lbSb = new Storyboard();
                foreach (int row in rows)
                {
                    if (p.FindName($"LB_ヒート{row}") is UIElement lbH)
                        _partsMain?.フェードイン(true, lbH, lbSb, 0);
                    if (p.FindName($"LB_背番号{row}") is UIElement lbB)
                        _partsMain?.フェードイン(true, lbB, lbSb, 0);
                }
                lbSb.Begin();
            };
            imSb.Begin();
        }

        /// <summary>
        /// 背番号テキストを指定行から設定する。
        /// 1行に収まらない場合は (row+1) 行目を使用し、2行目の IM_ヒート/LB_ヒート は非表示。
        /// </summary>
        private void SetBgNumbers(
            パーツ.PRG004_ヒート表示_大 p,
            List<string> players,
            int row,
            bool isCurrent)
        {
            // LB_背番号 の幅は 431px、フォントサイズ 16px Segoe UI Semibold で
            // 半角3文字（"999"）＋区切り2文字（"  "）= 5文字 ≈ 約40px/選手
            // 431/40 ≈ 10.7 → 最大10〜11選手で1行収まる目安
            // 文字数での近似判定: "999  " = 5文字 × 選手数
            string allText = FormatPlayers(players);
            // 1行の実用的な文字数上限 (半角換算で約50)
            bool needsSecondRow = allText.Length > 50;

            if (!needsSecondRow || row >= MaxRows)
            {
                // 1行で表示
                SetLabelContent(p, $"LB_背番号{row}", allText);
                SetLabelColor(p, $"LB_背番号{row}", isCurrent ? CurrentBrush : DefaultBrush);
                SetVisible(p, $"IM_明細{row}", true);
                SetVisible(p, $"LB_背番号{row}", true);
            }
            else
            {
                // 2行に分割
                int half  = (players.Count + 1) / 2;
                string ln1 = FormatPlayers(players.Take(half).ToList());
                string ln2 = FormatPlayers(players.Skip(half).ToList());

                // 1行目
                SetLabelContent(p, $"LB_背番号{row}", ln1);
                SetLabelColor(p, $"LB_背番号{row}", isCurrent ? CurrentBrush : DefaultBrush);
                SetVisible(p, $"IM_明細{row}", true);
                SetVisible(p, $"LB_背番号{row}", true);

                // 2行目: IM_ヒート, LB_ヒート は非表示
                int row2 = row + 1;
                SetVisible(p, $"IM_ヒート{row2}", false);
                SetVisible(p, $"LB_ヒート{row2}", false);
                SetLabelContent(p, $"LB_背番号{row2}", ln2);
                SetLabelColor(p, $"LB_背番号{row2}", isCurrent ? CurrentBrush : DefaultBrush);
                SetVisible(p, $"IM_明細{row2}", true);
                SetVisible(p, $"LB_背番号{row2}", true);
            }
        }

        /// <summary>全ヒート行 (1〜MaxRows) をフェードアウトして非表示。完了後に onCompleted を呼ぶ。
        /// フェードアウト中に再度呼ばれた場合は新しいコールバックを保留し、現在のフェードアウト完了後に実行する。
        /// これにより HeatEnd 通知と再生ボタンが競合して DoStep5() が誤呼び出しされるのを防ぐ。
        /// </summary>
        private void FadeOutAllHeatRows(Action? onCompleted = null)
        {
            // フェードアウト中に再度呼ばれた場合: 新しいコールバックを保留する。
            // 現在実行中のフェードアウトが完了した後に保留コールバックが実行される。
            if (_isFadingOut)
            {
                _pendingFadeOutCallback = onCompleted;
                return;
            }

            if (_partsMain == null)
            {
                // _partsMain 未初期化時は即時非表示
                HideAllHeatRowsImmediate();
                onCompleted?.Invoke();
                return;
            }

            var p = PartsPRG004;
            var sb = new Storyboard();
            bool anyVisible = false;

            for (int row = 1; row <= MaxRows; row++)
            {
                foreach (var name in new[] { $"IM_ヒート{row}", $"LB_ヒート{row}", $"IM_明細{row}", $"LB_背番号{row}" })
                {
                    if (p.FindName(name) is UIElement el && el.Visibility == Visibility.Visible && el.Opacity > 0)
                    {
                        _partsMain.フェードアウト(true, el, sb, 0);
                        anyVisible = true;
                    }
                }
            }

            if (!anyVisible)
            {
                HideAllHeatRowsImmediate();
                onCompleted?.Invoke();
                return;
            }

            _isFadingOut = true;
            sb.Completed += (s, e) =>
            {
                _isFadingOut = false;
                HideAllHeatRowsImmediate();
                onCompleted?.Invoke();

                // 保留中のコールバックがあれば連続して実行する
                var pending = _pendingFadeOutCallback;
                _pendingFadeOutCallback = null;
                if (pending != null)
                    FadeOutAllHeatRows(pending);
            };
            sb.Begin();
        }

        /// <summary>全ヒート行を即時 Collapsed に（アニメーションなし）</summary>
        private void HideAllHeatRowsImmediate()
        {
            var p = PartsPRG004;
            for (int row = 1; row <= MaxRows; row++)
            {
                SetVisible(p, $"IM_ヒート{row}", false);
                SetVisible(p, $"LB_ヒート{row}", false);
                SetVisible(p, $"IM_明細{row}", false);
                SetVisible(p, $"LB_背番号{row}", false);
            }
        }

        /// <summary>初期化時・更新前に全パーツを非表示にしてラベル内容もクリア</summary>
        private void HideAllParts()
        {
            var p = PartsPRG004;
            // ラベルコンテンツをクリア（前回表示のゴミを消す）
            foreach (var name in new[] {
                "LB_タイトル1", "LB_タイトル2", "LB_次", "LB_次_明細", "LB_次_時刻" })
            {
                SetLabelContent(p, name, string.Empty);
            }
            for (int row = 1; row <= MaxRows; row++)
            {
                SetLabelContent(p, $"LB_ヒート{row}", string.Empty);
                SetLabelContent(p, $"LB_背番号{row}", string.Empty);
            }
            // 全要素を非表示
            foreach (var name in new[] {
                "LB_タイトル1", "IM_タイトル1",
                "LB_タイトル2", "IM_タイトル2",
                "LB_種目",
                "LB_次", "IM_次",
                "LB_次_明細", "IM_次_明細",
                "LB_次_時刻" })
            {
                SetVisible(p, name, false);
            }
            HideAllHeatRowsImmediate();
        }

        /// <summary>
        /// LB_種目 の表示更新。
        /// 仕様: 現在種目は赤色（CurrentBrush）、それ以外は白色。
        /// TextBlock + Run を使って種目ごとに色を変える。
        /// </summary>
        private void UpdateLB種目()
        {
            if (DA_Master == null) return;
            var p = PartsPRG004;

            var dances = _danceList.Count > 0
                ? _danceList
                : DSDspDataHelper.Get全種目リスト(DA_Master, 区分番号, ラウンド番号);

            int curDncNo = GetCurrentDncNo();

            if (p.FindName("LB_種目") is System.Windows.Controls.TextBlock tb種目)
            {
                tb種目.Inlines.Clear();
                for (int i = 0; i < dances.Count; i++)
                {
                    if (i > 0)
                        tb種目.Inlines.Add(new Run("  ") { Foreground = Brushes.White });

                    bool isCur = (dances[i].DncNo == curDncNo);
                    tb種目.Inlines.Add(new Run(dances[i].DncCd)
                    {
                        Foreground = isCur ? CurrentBrush : Brushes.White
                    });
                }
            }
        }

        #endregion

        #region DS_Status ヘルパー

        /// <summary>
        /// DS_Status から現在種目番号（DS_CurDanNo）を取得する。
        /// 種目番号プロパティが明示指定されている場合はそちらを優先する。
        /// </summary>
        private int GetCurrentDncNo()
        {
            // MainWindow から明示的に種目番号が指定されている場合はそれを使う
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
                    if (prg?["DS_KbnNo"]?.ToString() != 区分番号 || prg?["DS_RndNo"]?.ToString() != ラウンド番号)
                        continue;
                    if (int.TryParse(prg?["DS_CurDanNo"]?.ToString(), out int dncNo))
                        return dncNo;
                }
            }
            return 0;
        }

        /// <summary>
        /// DS_Status から現在ヒート番号を取得する。
        /// ヒート番号プロパティが明示指定されている場合はそちらを優先する。
        /// DS_CurHeatId（UUID）を DS_PRGDANCEs[].DS_PRGHEATs[].DS_HeatId と照合してヒート番号を解決する。
        /// </summary>
        private int GetCurrentHeatNo()
        {
            // MainWindow から明示的にヒート番号が指定されている場合はそれを使う
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
                    if (prg?["DS_KbnNo"]?.ToString() != 区分番号 || prg?["DS_RndNo"]?.ToString() != ラウンド番号)
                        continue;

                    // DS_CurHeatId（UUID）を取得
                    var curHeatId = prg?["DS_CurHeatId"]?.ToString();
                    if (string.IsNullOrEmpty(curHeatId)) return 0;

                    // DS_PRGDANCEs の DS_PRGHEATs からヒート番号を解決
                    var prgDances = prg?["DS_PRGDANCEs"]?.AsArray();
                    if (prgDances == null) return 0;
                    foreach (var prgDance in prgDances)
                    {
                        var heats = prgDance?["DS_PRGHEATs"]?.AsArray();
                        if (heats == null) continue;
                        foreach (var heat in heats)
                        {
                            if (heat?["DS_HeatId"]?.ToString() == curHeatId)
                            {
                                if (int.TryParse(heat?["DS_HeatNo"]?.ToString(), out int heatNo))
                                    return heatNo;
                            }
                        }
                    }
                    return 0;
                }
            }
            return 0;
        }

        private static string FormatPlayers(List<string> players)
            => string.Join("  ", players);

        #endregion

        #region UI ヘルパー

        private static void SetVisible(System.Windows.FrameworkElement p, string name, bool visible)
        {
            if (p.FindName(name) is UIElement el)
                el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void SetOpacity(System.Windows.FrameworkElement p, string name, double opacity)
        {
            if (p.FindName(name) is UIElement el)
                el.Opacity = opacity;
        }

        private static void SetLabelContent(System.Windows.FrameworkElement p, string name, string text)
        {
            if (p.FindName(name) is Label lb)
                lb.Content = text;
        }

        private static void SetLabelColor(System.Windows.FrameworkElement p, string name, Brush brush)
        {
            if (p.FindName(name) is Label lb)
                lb.Foreground = brush;
        }

        #endregion
    }
}
