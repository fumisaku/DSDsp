using System.Windows.Media.Animation;
using DSDsp.Scenario;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_PRG_010_S_フロー進行表示_小
    ///
    /// 進行シナリオの RoundRules / DefaultRule(Flow) に従って画面遷移する進行表示画面。
    ///   TitleIntro  : 区分名紹介（下部バー）。Seconds 秒後に自動で次へ。
    ///   PlayerIntro : 出場選手紹介（右側リスト）。PageSize 組ずつ PageSeconds 秒で切り替え、
    ///                 最終ページも PageSeconds 秒表示後に次へ。
    ///   HeatIntro   : ヒート（または種目）紹介。左下=現在、右下=Next。
    ///                 HeatEnd 通知（NotifyHeatChanged）または再生ボタンで次の単位へ進む。
    ///
    /// 【進め方】
    ///   最初の Advance()（または NotifyHeatChanged()）でフローを開始する。
    ///   以降の Advance() は、TitleIntro/PlayerIntro 中ならその場面をスキップ、HeatIntro 中なら次の単位へ進む。
    ///   最後の単位が終了したら ScreenCompleted を発火する。
    /// </summary>
    public partial class DSP_PRG_010_S_フロー進行表示_小 : DSDspScreenBase
    {
        #region 定数・型
        private const string FontFamilyName = "Segoe UI Semibold";
        private const int FadeMs = 800;
        private const int MaxRows = 10;
        private static readonly Brush CurrentBrush = new SolidColorBrush(Color.FromRgb(204, 85, 0));
        private static readonly Brush WhiteBrush = Brushes.White;

        private enum Phase { None, Title, Player, Heat, Done }

        /// <summary>HeatIntro の1単位（ヒート単位なら HeatNo&gt;0、種目単位なら HeatNo=0）。</summary>
        private sealed class FlowUnit
        {
            public int DncNo;
            public int HeatNo;
            public string DncCd = string.Empty;
            public List<string> Players = new();
            public bool ShowPlayerIntro;
        }
        #endregion

        #region フィールド
        private ProgressFlowStep? _titleStep;
        private ProgressFlowStep? _playerStep;
        private ProgressFlowStep? _heatStep;

        private readonly List<FlowUnit> _units = new();
        private List<(int DncNo, string DncCd)> _danceList = new();
        private Dictionary<int, Dictionary<int, List<string>>> _heatMap = new();

        private bool _started = false;
        private Phase _phase = Phase.None;
        private int _unitIdx = 0;
        private int _token = 0;

        private List<(string 背番号, string 選手名, string 所属)> _pagePlayers = new();
        private int _pageSize = MaxRows;
        private int _pageCount = 1;

        /// <summary>フェード管理: 場面全体の要素 / 出場選手ページ毎の要素</summary>
        private readonly List<UIElement> _shown = new();
        private readonly List<UIElement> _pageEls = new();
        /// <summary>要素ごとの現在のフェードアニメーション（置き換えられた古い Completed を無視するため）</summary>
        private readonly Dictionary<UIElement, DoubleAnimation> _activeAnim = new();
        #endregion

        #region プロパティ
        /// <summary>適用するラウンドルール。MainWindow が設定する。</summary>
        public ProgressRoundRule? Rule
        {
            get => _rule;
            set
            {
                _rule = value;
                _titleStep = value?.Flow.FirstOrDefault(f => f.Type == "TitleIntro");
                _playerStep = value?.Flow.FirstOrDefault(f => f.Type == "PlayerIntro");
                _heatStep = value?.Flow.FirstOrDefault(f => f.Type == "HeatIntro");
            }
        }
        private ProgressRoundRule? _rule;

        /// <summary>所属表示方式。true=カップル所属名優先、false=L所属+"/"+P所属</summary>
        public bool カップル所属表示 { get; set; } = true;

        // フローは自分で完了を通知するため、基底クラスの自動完了が働かないよう十分大きな値にする
        protected override int TotalSteps => int.MaxValue;
        #endregion

        #region コンストラクタ
        public DSP_PRG_010_S_フロー進行表示_小()
        {
            InitializeComponent();
            // Loaded の遅延発火に依存せず、生成直後にダミー表示を消す
            HideAllParts();
        }
        #endregion

        #region 基底クラス連携
        protected override void ExecuteCurrentStep()
        {
            if (!_started) { StartFlow(skipTitle: false); return; }

            switch (_phase)
            {
                case Phase.Title:
                    StopAutoTimer();
                    {
                        int tk = ++_token;
                        FadeOutShown(() => { if (tk == _token) GoUnit(_unitIdx); });
                    }
                    break;
                case Phase.Player:
                    StopAutoTimer();
                    {
                        int tk = ++_token;
                        FadeOutShown(() => { if (tk == _token) GoHeat(_unitIdx); });
                    }
                    break;
                case Phase.Heat:
                    JumpToUnit(_unitIdx + 1);
                    break;
            }
        }

        /// <summary>
        /// HeatEnd 通知受信時に MainWindow から呼ばれる。
        /// MainWindow が設定した 種目番号/ヒート番号（次の対象）に対応する単位へ進む。
        /// </summary>
        public override void NotifyHeatChanged()
        {
            if (!_started) { StartFlow(skipTitle: true); return; }
            if (_phase == Phase.Done) return;

            int target = FindUnitIndex(種目番号, ヒート番号);
            if (target < 0) target = _unitIdx + 1;
            if (target <= _unitIdx) return;   // 同じ単位（同一種目の別ヒート等）または過去の通知は無視

            JumpToUnit(target);
        }
        #endregion

        #region フロー制御
        private void StartFlow(bool skipTitle)
        {
            _started = true;
            EnsurePartsMainInitialized();
            HideAllParts();
            BuildUnits();
            SetHeader();
            UpdateCOM003種目(0);

            _unitIdx = FindStartIndex();

            if (_units.Count == 0)
            {
                Finish();
                return;
            }

            if (!skipTitle && _titleStep != null)
                GoTitle();
            else
                GoUnit(_unitIdx);
        }

        private void GoTitle()
        {
            _token++;
            _phase = Phase.Title;
            int tk = _token;

            var p = PartsTIT003;
            string text = BuildTitleText();
            p.LB_種目紹介.Content = text;
            _partsMain?.フォントサイズ自動調整(p.LB_種目紹介, text, 400, 16, 8, FontFamilyName);
            p.LB_種目順.Visibility = Visibility.Collapsed;

            var els = new List<UIElement> { p.IM_種目2, p.IM_種目1, p.LB_種目紹介 };
            FadeIn(els);
            _shown.AddRange(els);
            CreateAndStartSlideAnimation(p.IM_種目1, 1000);
            CreateAndStartSlideAnimation(p.IM_種目2, -1000);

            int seconds = Math.Max(1, _titleStep?.Seconds ?? 5);
            AutoTimerSecondsOverride = seconds;
            StartAutoTimer(() =>
            {
                if (tk != _token) return;
                int tk2 = ++_token;
                FadeOutShown(() => { if (tk2 == _token) GoUnit(_unitIdx); });
            });
        }

        private void GoUnit(int idx)
        {
            if (idx >= _units.Count) { Finish(); return; }
            _unitIdx = idx;
            if (_units[idx].ShowPlayerIntro && _units[idx].Players.Count > 0)
                GoPlayer(idx);
            else
                GoHeat(idx);
        }

        private void GoPlayer(int idx)
        {
            _token++;
            _phase = Phase.Player;
            _unitIdx = idx;

            var unit = _units[idx];
            UpdateCOM003種目(unit.DncNo);

            _pagePlayers = BuildPlayerRows(unit.Players);
            _pageSize = Math.Clamp(_playerStep?.PageSize ?? MaxRows, 1, MaxRows);
            _pageCount = Math.Max(1, (int)Math.Ceiling(_pagePlayers.Count / (double)_pageSize));

            var p = PartsLST004;

            // タイトル（競技会名 / 進行番号+区分名... / 出場選手）
            string title1 = DSDspDataHelper.Get競技会名(DA_Master);
            SetLabel(p, "LB_タイトル1", title1, 181, 9, 6);
            SetLabel(p, "LB_タイトル2", BuildTitleText(), 160, 10, 4);
            string title3 = unit.HeatNo > 0
                ? $"出場選手　{unit.DncCd}　{unit.HeatNo}H"
                : $"出場選手　{unit.DncCd}";
            SetLabel(p, "LB_タイトル3", title3, 140, 9, 6);
            SetLabel(p, "LB_タイトル4", string.Empty, 0, 9, 9);

            var titleEls = new List<UIElement>();
            AddEl(titleEls, p, "IM_タイトル1"); AddEl(titleEls, p, "LB_タイトル1");
            AddEl(titleEls, p, "IM_タイトル2"); AddEl(titleEls, p, "LB_タイトル2");
            AddEl(titleEls, p, "IM_タイトル3"); AddEl(titleEls, p, "LB_タイトル3");
            if (_pageCount > 1) AddEl(titleEls, p, "LB_タイトル4");
            FadeIn(titleEls);
            _shown.AddRange(titleEls);

            ShowPage(0);
            ArmPageTimer(0);
        }

        private void ArmPageTimer(int page)
        {
            int tk = _token;
            AutoTimerSecondsOverride = Math.Max(1, _playerStep?.PageSeconds ?? 5);
            StartAutoTimer(() =>
            {
                if (tk != _token) return;
                if (page + 1 < _pageCount)
                {
                    int tk2 = ++_token;
                    FadeOutList(_pageEls, () =>
                    {
                        if (tk2 != _token) return;
                        ShowPage(page + 1);
                        ArmPageTimer(page + 1);
                    });
                }
                else
                {
                    int tk2 = ++_token;
                    FadeOutShown(() => { if (tk2 == _token) GoHeat(_unitIdx); });
                }
            });
        }

        private void GoHeat(int idx)
        {
            _token++;
            _phase = Phase.Heat;
            _unitIdx = idx;

            var unit = _units[idx];
            UpdateCOM003種目(unit.DncNo);

            var p = PartsPRG005;
            HideNames(p, HeatNames);

            int maxChars = Math.Max(1, _heatStep?.RowMaxChars ?? 30);
            var els = new List<UIElement>();

            string curText = unit.HeatNo > 0 ? $"現在　{unit.DncCd}　{unit.HeatNo}H" : $"現在　{unit.DncCd}";
            SetLabel(p, "LB_現", curText, 175, 11, 7);
            AddEl(els, p, "IM_現"); AddEl(els, p, "LB_現");
            SetPlayerRows(p, unit.Players, "現", maxChars, els);

            bool showNext = _heatStep?.ShowNext ?? true;
            if (showNext && idx + 1 < _units.Count)
            {
                var next = _units[idx + 1];
                string nextText = next.HeatNo > 0 ? $"Next　{next.DncCd}　{next.HeatNo}H" : $"Next　{next.DncCd}";
                SetLabel(p, "LB_次", nextText, 175, 11, 7);
                AddEl(els, p, "IM_次"); AddEl(els, p, "LB_次");
                SetPlayerRows(p, next.Players, "次", maxChars, els);
            }

            FadeIn(els);
            _shown.AddRange(els);
        }

        /// <summary>現在の場面をフェードアウトして指定の単位へ進む（範囲外なら終了）。</summary>
        private void JumpToUnit(int target)
        {
            StopAutoTimer();
            _unitIdx = target;
            int tk = ++_token;
            FadeOutShown(() =>
            {
                if (tk != _token) return;
                if (target >= _units.Count) Finish();
                else GoUnit(target);
            });
        }

        private void Finish()
        {
            _token++;
            _phase = Phase.Done;
            StopAutoTimer();
            int tk = _token;
            FadeOutShown(() =>
            {
                if (tk != _token) return;
                RaiseScreenCompleted();
            });
        }
        #endregion

        #region データ構築
        private void BuildUnits()
        {
            _units.Clear();
            _danceList = DSDspDataHelper.Get全種目リスト(DA_Master, 区分番号, ラウンド番号, DGrpNo);
            _heatMap = DSDspDataHelper.Get全種目全ヒート背番号マップ(DS_Status, 区分番号, ラウンド番号, DGrpNo);

            var present = _danceList.Where(d => _heatMap.ContainsKey(d.DncNo)).ToList();
            bool singleHeatAll = present.Count > 0 && present.All(d => _heatMap[d.DncNo].Count <= 1);
            bool byDance = string.Equals(_heatStep?.Unit, "Dance", StringComparison.OrdinalIgnoreCase);

            foreach (var (dncNo, dncCd) in present)
            {
                var heatDic = _heatMap[dncNo];
                if (byDance)
                {
                    var all = heatDic.Values.SelectMany(x => x).Distinct().ToList();
                    all.Sort(CompareBango);
                    _units.Add(new FlowUnit { DncNo = dncNo, HeatNo = 0, DncCd = dncCd, Players = all });
                }
                else
                {
                    foreach (var hNo in heatDic.Keys.OrderBy(n => n))
                        _units.Add(new FlowUnit { DncNo = dncNo, HeatNo = hNo, DncCd = dncCd, Players = heatDic[hNo] });
                }
            }

            for (int i = 0; i < _units.Count; i++)
            {
                _units[i].ShowPlayerIntro = _playerStep != null
                    && !(_playerStep.SingleHeatFirstDanceOnly && singleHeatAll && i > 0);
            }
        }

        private static int CompareBango(string a, string b)
        {
            if (int.TryParse(a, out var na) && int.TryParse(b, out var nb)) return na.CompareTo(nb);
            return string.Compare(a, b, StringComparison.Ordinal);
        }

        private int FindUnitIndex(int dncNo, int heatNo)
        {
            if (dncNo <= 0) return -1;
            int idx = _units.FindIndex(u => u.DncNo == dncNo && (u.HeatNo == 0 || u.HeatNo == heatNo));
            if (idx < 0) idx = _units.FindIndex(u => u.DncNo == dncNo);
            return idx;
        }

        private int FindStartIndex()
        {
            int idx = FindUnitIndex(GetCurrentDncNo(), GetCurrentHeatNo());
            return idx < 0 ? 0 : idx;
        }

        private List<(string 背番号, string 選手名, string 所属)> BuildPlayerRows(List<string> bangos)
        {
            var list = new List<(string, string, string)>();
            var sorted = bangos.ToList();
            sorted.Sort(CompareBango);
            foreach (var bango in sorted)
            {
                var info = DSDspDataHelper.Get選手情報(DA_Master, bango, 区分番号);
                string l苗字 = GetFamilyName(DSDspDataHelper.Get選手名L(info));
                string p苗字 = GetFamilyName(DSDspDataHelper.Get選手名P(info));
                string 選手名 = string.IsNullOrEmpty(p苗字) ? l苗字 : $"{l苗字}・{p苗字}";
                list.Add((bango, 選手名, Build所属テキスト(info)));
            }
            return list;
        }

        private static string GetFamilyName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return string.Empty;
            var parts = fullName.Split(new[] { '　', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : fullName;
        }

        private string Build所属テキスト(System.Text.Json.Nodes.JsonNode? info)
        {
            if (info == null) return string.Empty;
            if (カップル所属表示) return DSDspDataHelper.Get所属(info);
            string l = info["DM_Ctry"]?.ToString() ?? string.Empty;
            string pp = info["DM_PCtry"]?.ToString() ?? string.Empty;
            return string.IsNullOrEmpty(pp) ? l : $"{l}/{pp}";
        }

        /// <summary>「進行番号　区分名　ラウンド名　[種目GRP名]」</summary>
        private string BuildTitleText()
        {
            string prgNo = DSDspDataHelper.Get現在進行番号(DS_Status, 区分番号, ラウンド番号, DGrpNo);
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(prgNo)) parts.Add(prgNo);
            parts.Add(DSDspDataHelper.Get区分名(DA_Master, 区分番号));
            parts.Add(DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号));
            string grp = GetDGrpDisplayName();
            if (!string.IsNullOrEmpty(grp)) parts.Add(grp);
            return string.Join("　", parts);
        }

        /// <summary>種目GRPが2つ以上あるときだけ種目GRP名を返す（1つだけなら空）。</summary>
        private string GetDGrpDisplayName()
        {
            if (DA_Master != null
                && DSDspDataHelper.GetDGRP数(DA_Master, 区分番号, ラウンド番号) >= 2
                && !string.IsNullOrEmpty(DGrpNo))
                return DSDspDataHelper.GetDGRP名(DA_Master, 区分番号, ラウンド番号, DGrpNo);
            return string.Empty;
        }

        private int GetCurrentDncNo()
        {
            if (種目番号 > 0) return 種目番号;
            var prg = FindCurrentPrg();
            return prg != null && int.TryParse(prg["DS_CurDanNo"]?.ToString(), out var n) ? n : 0;
        }

        /// <summary>DS_CurHeatId（UUID）を DS_PRGHEATs[].DS_HeatId と照合してヒート番号を解決する。</summary>
        private int GetCurrentHeatNo()
        {
            if (ヒート番号 > 0) return ヒート番号;
            var prg = FindCurrentPrg();
            var curHeatId = prg?["DS_CurHeatId"]?.ToString();
            if (prg == null || string.IsNullOrEmpty(curHeatId)) return 0;
            var prgDances = prg["DS_PRGDANCEs"]?.AsArray();
            if (prgDances == null) return 0;
            foreach (var prgDance in prgDances)
            {
                var heats = prgDance?["DS_PRGHEATs"]?.AsArray();
                if (heats == null) continue;
                foreach (var heat in heats)
                {
                    if (heat?["DS_HeatId"]?.ToString() == curHeatId
                        && int.TryParse(heat?["DS_HeatNo"]?.ToString(), out var h))
                        return h;
                }
            }
            return 0;
        }

        private System.Text.Json.Nodes.JsonNode? FindCurrentPrg()
        {
            var floors = DS_Status?["DS_FLOORs"]?.AsArray();
            if (floors == null) return null;
            foreach (var floor in floors)
            {
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) continue;
                foreach (var prg in prgrs)
                {
                    if (prg?["DS_KbnNo"]?.ToString() != 区分番号 || prg?["DS_RndNo"]?.ToString() != ラウンド番号) continue;
                    if (!string.IsNullOrEmpty(DGrpNo) && prg?["DS_DGrpNo"]?.ToString() != DGrpNo) continue;
                    return prg;
                }
            }
            return null;
        }
        #endregion

        #region 表示（ヘッダ・出場選手ページ・行）
        private void SetHeader()
        {
            if (PartsCOM001.FindName("TB_左上1") is パーツ.OutlinedTextBlock otb1)
                otb1.Text = DSDspDataHelper.Get競技会名(DA_Master);
            if (PartsCOM001.FindName("TB_左上2") is パーツ.OutlinedTextBlock otb2)
            {
                string kbn = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
                string rnd = DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号);
                string grp = GetDGrpDisplayName();
                otb2.Text = string.IsNullOrEmpty(grp) ? $"{kbn}　{rnd}" : $"{kbn}　{rnd}　{grp}";
            }
            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
                lbRight.Content = $"現在時刻　{DateTime.Now:HH:mm}";
            StartClock();
        }

        /// <summary>COM003 の種目記号を更新（curDncNo の種目のみ赤、その他は白。0 なら全て白）</summary>
        private void UpdateCOM003種目(int curDncNo)
        {
            if (PartsCOM003.FindName("LB_右上") is not Label lb) return;
            var dances = _danceList.Count > 0
                ? _danceList
                : DSDspDataHelper.Get全種目リスト(DA_Master, 区分番号, ラウンド番号, DGrpNo);

            lb.ContentTemplate = null;
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            for (int i = 0; i < dances.Count; i++)
            {
                bool isCur = dances[i].DncNo == curDncNo;
                panel.Children.Add(new パーツ.OutlinedTextBlock
                {
                    Text = dances[i].DncCd,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = isCur ? CurrentBrush : WhiteBrush,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5,
                    Margin = new Thickness(0, 0, i < dances.Count - 1 ? 8 : 0, 0),
                });
            }
            lb.Content = panel;
        }

        /// <summary>出場選手の指定ページを表示する（行の内容設定とフェードイン）。</summary>
        private void ShowPage(int page)
        {
            var p = PartsLST004;
            _pageEls.Clear();

            int start = page * _pageSize;
            int end = Math.Min(start + _pageSize, _pagePlayers.Count);

            if (_pageCount > 1)
                SetLabel(p, "LB_タイトル4", $"{start + 1} ～ {end}", 0, 9, 9);

            for (int row = 1; row <= MaxRows; row++)
            {
                int idx = start + row - 1;
                bool has = row <= _pageSize && idx < _pagePlayers.Count;
                SetVisible(p, $"LB_結果{row}_順位", false);
                if (!has)
                {
                    foreach (var n in RowNames(row)) SetVisible(p, n, false);
                    continue;
                }

                var player = _pagePlayers[idx];
                if (p.FindName($"LB_結果{row}_背番号") is Label lbNo)
                {
                    Canvas.SetLeft(lbNo, 1);
                    lbNo.Width = 26;
                    lbNo.Content = player.背番号;
                }
                if (p.FindName($"LB_結果{row}_選手名") is Label lbName)
                {
                    Canvas.SetLeft(lbName, 23);
                    _partsMain?.フォントサイズ自動調整(lbName, player.選手名, 72, 10, 6, FontFamilyName);
                    lbName.Content = player.選手名;
                }
                if (p.FindName($"LB_結果{row}_得点") is Label lbAff)
                {
                    Canvas.SetLeft(lbAff, 98);
                    lbAff.Width = 79;
                    _partsMain?.フォントサイズ自動調整(lbAff, player.所属, 68, 10, 5, FontFamilyName);
                    lbAff.Content = player.所属;
                }
                foreach (var n in RowNames(row)) AddEl(_pageEls, p, n);
            }

            FadeIn(_pageEls);
        }

        private static string[] RowNames(int row) => new[]
        {
            $"IM_明細{row}", $"LB_結果{row}_背番号", $"LB_結果{row}_選手名", $"LB_結果{row}_得点",
        };

        /// <summary>背番号を「現」または「次」の1〜2行に設定し、表示対象要素を els に追加する。</summary>
        private void SetPlayerRows(パーツ.PRG005_ヒート表示_小 p, List<string> players, string key, int maxChars, List<UIElement> els)
        {
            if (players.Count == 0) return;

            var sorted = players.ToList();
            sorted.Sort(CompareBango);
            string allText = string.Join("  ", sorted);

            if (allText.Length <= maxChars)
            {
                SetRow(p, key, 1, allText, els);
            }
            else
            {
                int half = (sorted.Count + 1) / 2;
                SetRow(p, key, 1, string.Join("  ", sorted.Take(half)), els);
                SetRow(p, key, 2, string.Join("  ", sorted.Skip(half)), els);
            }
        }

        private void SetRow(パーツ.PRG005_ヒート表示_小 p, string key, int rowNo, string text, List<UIElement> els)
        {
            SetLabel(p, $"LB_明細_{key}{rowNo}", text, 222, 11, 7);
            AddEl(els, p, $"IM_明細_{key}{rowNo}");
            AddEl(els, p, $"LB_明細_{key}{rowNo}");
        }
        #endregion

        #region 要素操作・フェード
        private static readonly string[] HeatNames =
        {
            "IM_現", "LB_現", "IM_次", "LB_次",
            "IM_明細_現1", "LB_明細_現1", "IM_明細_現2", "LB_明細_現2",
            "IM_明細_次1", "LB_明細_次1", "IM_明細_次2", "LB_明細_次2",
        };

        private void HideAllParts()
        {
            var lst = PartsLST004;
            var names = new List<string>
            {
                "IM_タイトル1", "LB_タイトル1", "IM_タイトル2", "LB_タイトル2",
                "IM_タイトル3", "LB_タイトル3", "LB_タイトル4",
            };
            for (int row = 1; row <= MaxRows; row++)
            {
                names.Add($"LB_結果{row}_順位");
                names.AddRange(RowNames(row));
            }
            HideNames(lst, names);
            HideNames(PartsPRG005, HeatNames);

            PartsTIT003.IM_種目1.Visibility = Visibility.Collapsed;
            PartsTIT003.IM_種目2.Visibility = Visibility.Collapsed;
            PartsTIT003.LB_種目紹介.Visibility = Visibility.Collapsed;
            PartsTIT003.LB_種目順.Visibility = Visibility.Collapsed;

            _shown.Clear();
            _pageEls.Clear();
        }

        private static void HideNames(FrameworkElement part, IEnumerable<string> names)
        {
            foreach (var n in names)
            {
                if (part.FindName(n) is UIElement el)
                {
                    el.BeginAnimation(UIElement.OpacityProperty, null);
                    el.Opacity = 1;
                    el.Visibility = Visibility.Collapsed;
                    if (el is Label lb) lb.Content = string.Empty;
                }
            }
        }

        private static void AddEl(List<UIElement> list, FrameworkElement part, string name)
        {
            if (part.FindName(name) is UIElement el) list.Add(el);
        }

        private static void SetVisible(FrameworkElement part, string name, bool visible)
        {
            if (part.FindName(name) is UIElement el)
                el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetLabel(FrameworkElement part, string name, string text, double maxWidth, double maxFont, double minFont)
        {
            if (part.FindName(name) is not Label lb) return;
            lb.Content = text;
            if (maxWidth > 0)
                _partsMain?.フォントサイズ自動調整(lb, text, maxWidth, maxFont, minFont, FontFamilyName);
        }

        private void FadeIn(IEnumerable<UIElement> elements)
        {
            foreach (var el in elements.ToList())
            {
                el.BeginAnimation(UIElement.OpacityProperty, null);
                el.Opacity = 0;
                el.Visibility = Visibility.Visible;

                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(FadeMs)) { DecelerationRatio = 0.8 };
                var target = el;
                _activeAnim[target] = anim;
                anim.Completed += (s, e) =>
                {
                    if (!_activeAnim.TryGetValue(target, out var cur) || cur != anim) return;
                    _activeAnim.Remove(target);
                    target.BeginAnimation(UIElement.OpacityProperty, null);
                    target.Opacity = 1;
                };
                el.BeginAnimation(UIElement.OpacityProperty, anim);
            }
        }

        /// <summary>指定要素を現在の不透明度からフェードアウトして非表示にし、全て完了したら done を呼ぶ。list は空にする。</summary>
        private void FadeOutList(List<UIElement> list, Action done)
        {
            var targets = list.ToList();
            list.Clear();
            if (targets.Count == 0) { done(); return; }

            int remaining = targets.Count;
            foreach (var el in targets)
            {
                double from = el.Opacity;
                el.BeginAnimation(UIElement.OpacityProperty, null);
                el.Opacity = from;

                var anim = new DoubleAnimation(from, 0, TimeSpan.FromMilliseconds(FadeMs)) { DecelerationRatio = 0.8 };
                var target = el;
                _activeAnim[target] = anim;
                anim.Completed += (s, e) =>
                {
                    if (!_activeAnim.TryGetValue(target, out var cur) || cur != anim) return;
                    _activeAnim.Remove(target);
                    target.BeginAnimation(UIElement.OpacityProperty, null);
                    target.Opacity = 0;
                    target.Visibility = Visibility.Collapsed;
                    if (--remaining == 0) done();
                };
                el.BeginAnimation(UIElement.OpacityProperty, anim);
            }
        }

        /// <summary>表示中の全要素（場面＋出場選手ページ）をフェードアウトする。</summary>
        private void FadeOutShown(Action done)
        {
            var all = new List<UIElement>(_shown);
            all.AddRange(_pageEls.Where(e => !all.Contains(e)));
            _shown.Clear();
            _pageEls.Clear();
            FadeOutList(all, done);
        }
        #endregion
    }
}

// Made with Bob
