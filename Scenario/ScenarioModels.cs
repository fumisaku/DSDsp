using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DSDsp.Scenario
{
    /// <summary>
    /// シナリオタイプ
    /// </summary>
    public enum ScenarioType
    {
        Progress,  // 進行
        AJS,       // AJS
        Award      // 表彰式
    }

    /// <summary>
    /// 進行シナリオアイテム
    /// </summary>
    public class ProgressScenarioItem
    {
        [JsonPropertyName("ProgressNo")]
        public int ProgressNo { get; set; }

        [JsonPropertyName("ProgressSubNo")]
        public int ProgressSubNo { get; set; }

        [JsonPropertyName("CategoryNo")]
        public string CategoryNo { get; set; } = string.Empty;

        [JsonPropertyName("CategoryName")]
        public string CategoryName { get; set; } = string.Empty;

        [JsonPropertyName("RoundName")]
        public string RoundName { get; set; } = string.Empty;

        [JsonPropertyName("EventGroupName")]
        public string EventGroupName { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{ProgressNo}-{ProgressSubNo}: {CategoryName} {RoundName} {EventGroupName}";
        }
    }

    // -------------------------------------------------------------------------
    // AJSシナリオ定義モデル（画面進行一覧を動的生成するための定義ファイル用）
    // -------------------------------------------------------------------------

    /// <summary>
    /// 背景タイプ
    /// </summary>
    public enum AjsBackgroundType
    {
        /// <summary>背景なし（透明）</summary>
        None,
        /// <summary>イメージファイル</summary>
        Image,
        /// <summary>RGB色指定</summary>
        Color,
        /// <summary>AppSettings.ChromaKeySettings.BackgroundColor を使用するクロマキ背景</summary>
        ChromaKey,
    }

    /// <summary>
    /// シナリオの背景設定。
    /// Type に応じて ImageFile または R/G/B を使用する。
    /// </summary>
    public class AjsBackground
    {
        /// <summary>
        /// 背景タイプ。"None" / "Image" / "Color" / "ChromaKey" のいずれかを指定する。
        /// "ChromaKey" を指定すると DSDsp.json の ChromaKeySettings.BackgroundColor が使われる。
        /// </summary>
        [JsonPropertyName("Type")]
        public string Type { get; set; } = "None";

        /// <summary>
        /// Type="Image" のとき有効。イメージファイル名のみを指定する（例: "background.png"）。
        /// ファイルはプロジェクトの「イメージ」フォルダに &lt;Resource&gt; として追加すること。
        /// パーツの XAML で使用する画像（"../イメージ/xxx.png"）と同じ場所。
        /// </summary>
        [JsonPropertyName("ImageFile")]
        public string ImageFile { get; set; } = string.Empty;

        /// <summary>Type="Color" のとき有効。赤成分 0–255。</summary>
        [JsonPropertyName("R")]
        public byte R { get; set; } = 0;

        /// <summary>Type="Color" のとき有効。緑成分 0–255。</summary>
        [JsonPropertyName("G")]
        public byte G { get; set; } = 0;

        /// <summary>Type="Color" のとき有効。青成分 0–255。</summary>
        [JsonPropertyName("B")]
        public byte B { get; set; } = 0;

        /// <summary>
        /// 背景タイプを列挙型で取得する。
        /// </summary>
        public AjsBackgroundType GetBackgroundType()
        {
            return Type.ToUpperInvariant() switch
            {
                "IMAGE"    => AjsBackgroundType.Image,
                "COLOR"    => AjsBackgroundType.Color,
                "CHROMAKEY" => AjsBackgroundType.ChromaKey,
                _          => AjsBackgroundType.None,
            };
        }
    }

    /// <summary>
    /// AJSシナリオ定義ファイルの画面エントリ（1画面分の設定）
    /// </summary>
    public class AjsScreenEntry
    {
        [JsonPropertyName("ScreenId")]
        public string ScreenId { get; set; } = string.Empty;

        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// ソロ結果画面のみ使用。"GD" または "PD"。空文字の場合は採点方式によらず適用。
        /// </summary>
        [JsonPropertyName("ScrMtdType")]
        public string ScrMtdType { get; set; } = string.Empty;

        [JsonPropertyName("Enabled")]
        public bool Enabled { get; set; }

        /// <summary>
        /// ステップ動作モード。省略時は既定動作。
        /// "Hold" を指定すると、Step0で同時実行していたステップを分割し、
        /// _B（大画面）は中間に「何もしない」ステップを追加する。
        /// </summary>
        [JsonPropertyName("StepMode")]
        public string StepMode { get; set; } = string.Empty;

        /// <summary>
        /// true のとき、Auto タイマーによる自動フェードアウトを行わず、
        /// サーバーからの採点集計完了通知（DV_Result 受信）があるまで画面を表示し続ける。
        /// DSP_GRP_001_B の全画面モードで使用。
        /// </summary>
        [JsonPropertyName("WaitsForResult")]
        public bool WaitsForResult { get; set; } = false;

        /// <summary>
        /// Auto タイマーの表示保持秒数を上書きする。
        /// 省略（-1）のときは各画面クラスのデフォルト値（通常5秒）を使用する。
        /// 例: "AutoTimerSeconds": 10 → 10秒後に自動フェードアウト
        /// </summary>
        [JsonPropertyName("AutoTimerSeconds")]
        public int AutoTimerSeconds { get; set; } = -1;
    }

    /// <summary>
    /// AJSシナリオ定義ファイルの競技種別毎の画面グループ
    /// キーは画面ID（例: "DSP_TIT_002_B"）
    /// </summary>
    public class AjsScreenGroup : Dictionary<string, AjsScreenEntry>
    {
        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// AJSシナリオ定義ファイルの Screens セクション（1ラウンド区分用）
    /// </summary>
    public class AjsRoundScreens
    {
        [JsonPropertyName("Common")]
        public Dictionary<string, AjsScreenEntry> Common { get; set; } = new();

        [JsonPropertyName("Solo")]
        public Dictionary<string, AjsScreenEntry> Solo { get; set; } = new();

        [JsonPropertyName("Group")]
        public Dictionary<string, AjsScreenEntry> Group { get; set; } = new();

        [JsonPropertyName("Duel")]
        public Dictionary<string, AjsScreenEntry> Duel { get; set; } = new();
    }

    /// <summary>
    /// AJSシナリオ定義ファイルの Screens セクション。
    /// 決勝（Final）・準決勝（SemiFinal）を別々に定義できる。
    /// Final または SemiFinal が定義されている場合、ラウンド名に基づいて該当セクションを優先使用する。
    /// 定義がない場合は後方互換としてフラットな Common/Solo/Group/Duel を使用する。
    /// </summary>
    public class AjsScreens
    {
        // ── 後方互換用フラットセクション（Final/SemiFinal 未定義時に使用） ──
        [JsonPropertyName("Common")]
        public Dictionary<string, AjsScreenEntry> Common { get; set; } = new();

        [JsonPropertyName("Solo")]
        public Dictionary<string, AjsScreenEntry> Solo { get; set; } = new();

        [JsonPropertyName("Group")]
        public Dictionary<string, AjsScreenEntry> Group { get; set; } = new();

        [JsonPropertyName("Duel")]
        public Dictionary<string, AjsScreenEntry> Duel { get; set; } = new();

        // ── 決勝専用セクション（ラウンド名が「決勝」に合致する場合に使用） ──
        /// <summary>
        /// 決勝用画面定義。null または全セクションが空の場合はフラットセクションを使用する。
        /// </summary>
        [JsonPropertyName("Final")]
        public AjsRoundScreens? Final { get; set; }

        // ── 準決勝専用セクション（ラウンド名が「準決勝」に合致する場合に使用） ──
        /// <summary>
        /// 準決勝用画面定義。null または全セクションが空の場合はフラットセクションを使用する。
        /// </summary>
        [JsonPropertyName("SemiFinal")]
        public AjsRoundScreens? SemiFinal { get; set; }

        /// <summary>
        /// ラウンド名に基づいて使用する AjsRoundScreens を返す。
        /// ・ラウンド名に「準決勝」を含む → SemiFinal（定義があれば）
        /// ・ラウンド名に「決勝」を含み「準決勝」を含まない → Final（定義があれば）
        /// ・それ以外または該当セクションが空 → フラットセクション（後方互換）
        /// </summary>
        public AjsRoundScreens ResolveByRoundName(string roundName)
        {
            AjsRoundScreens? candidate = null;

            if (roundName.Contains("準決勝"))
                candidate = SemiFinal;
            else if (roundName.Contains("決勝"))
                candidate = Final;

            // candidate が null または全セクションが空ならフラットセクションにフォールバック
            if (candidate != null && (
                candidate.Common.Count > 0 ||
                candidate.Solo.Count   > 0 ||
                candidate.Group.Count  > 0 ||
                candidate.Duel.Count   > 0))
            {
                return candidate;
            }

            // フラットセクションを AjsRoundScreens として返す（共通フォールバック）
            return new AjsRoundScreens
            {
                Common = this.Common,
                Solo   = this.Solo,
                Group  = this.Group,
                Duel   = this.Duel,
            };
        }
    }

    /// <summary>
    /// AJS SUBシナリオ定義ファイル（メインに重ねて表示するオーバーレイ用）
    /// </summary>
    public class AjsSubScenarioDefinition
    {
        [JsonPropertyName("ScenarioName")]
        public string ScenarioName { get; set; } = string.Empty;

        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("Screens")]
        public AjsScreens Screens { get; set; } = new();
    }

    /// <summary>
    /// AJSシナリオ定義ファイル
    /// </summary>
    public class AjsScenarioDefinition
    {
        [JsonPropertyName("ScenarioName")]
        public string ScenarioName { get; set; } = string.Empty;

        [JsonPropertyName("ScenarioType")]
        public string ScenarioType { get; set; } = "AJS";

        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("Screens")]
        public AjsScreens Screens { get; set; } = new();

        /// <summary>
        /// シナリオの背景設定。null の場合はデフォルト（黒）。
        /// 将来的に画面ID別の上書き設定も追加予定。
        /// </summary>
        [JsonPropertyName("Background")]
        public AjsBackground? Background { get; set; }

        /// <summary>
        /// SUBシナリオ定義。メインの上に透明背景で重ねて表示される。
        /// null の場合はSUBシナリオなし。
        /// </summary>
        [JsonPropertyName("SubScenario")]
        public AjsSubScenarioDefinition? SubScenario { get; set; }

        /// <summary>
        /// クロマキーモードかどうか。
        /// true の場合、DSP_GRP_001/002 の Step4→Step5 で停止し、
        /// 次の再生ボタンで Step6（LST005+LST006フェードアウト）を実行する。
        /// false（デフォルト）の場合、Step4→Step5→Step6 を自動で進める（全画面モード）。
        /// </summary>
        [JsonPropertyName("ChromaKeyMode")]
        public bool ChromaKeyMode { get; set; } = false;
    }

    // -------------------------------------------------------------------------
    // 画面進行一覧アイテム（動的生成の結果）
    // -------------------------------------------------------------------------

    /// <summary>
    /// 画面進行一覧の1アイテム（AJSシナリオから動的生成される）
    /// </summary>
    public class AjsProgressItem
    {
        /// <summary>表示する画面ID</summary>
        public string ScreenId { get; set; } = string.Empty;

        /// <summary>種目番号（DS_DncNo）</summary>
        public int DanceNo { get; set; }

        /// <summary>ヒート番号（DS_HeatNo）</summary>
        public int HeatNo { get; set; }

        /// <summary>種目記号（DA_Master の DE_DncCd。例: "WL", "TG"）</summary>
        public string DanceCd { get; set; } = string.Empty;

        /// <summary>説明（表示用）</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// デュエルヒート表の一覧表示モード。
        /// true の場合、DSP_GRP_001 は種目内の全ヒート選手一覧を表示し、
        /// 順位列にヒート番号（"1H", "2H" など）を表示する。
        /// </summary>
        public bool IsOverviewMode { get; set; } = false;

        /// <summary>
        /// 種目内の最終ヒートかどうか。
        /// true の場合、このヒートの DSP_SOL_007 等のフェードアウト後に COM002 右上をクリアする。
        /// </summary>
        public bool IsLastHeatInDance { get; set; } = false;

        /// <summary>
        /// ステップ動作モード。AjsScreenEntry.StepMode から引き継がれる。
        /// "" (既定) または "Hold"。
        /// </summary>
        public string StepMode { get; set; } = string.Empty;

        /// <summary>
        /// true のとき、Auto タイマーによる自動フェードアウトを行わず、
        /// サーバーからの採点集計完了通知があるまで画面を表示し続ける。
        /// AjsScreenEntry.WaitsForResult から引き継がれる。
        /// </summary>
        public bool WaitsForResult { get; set; } = false;

        /// <summary>
        /// Auto タイマーの表示保持秒数を上書きする（-1 = 画面クラスのデフォルト値を使用）。
        /// AjsScreenEntry.AutoTimerSeconds から引き継がれる。
        /// </summary>
        public int AutoTimerSeconds { get; set; } = -1;

        public override string ToString()
        {
            var dncLabel = DanceNo == 0 ? "" : string.IsNullOrEmpty(DanceCd)
                ? $"  種目{DanceNo}"
                : $"  種目{DanceNo}({DanceCd})";
            var heatLabel = HeatNo == 0 ? "" : $"  ヒート{HeatNo}";
            return $"{ScreenId}{dncLabel}{heatLabel}  {Description}";
        }
    }

    // -------------------------------------------------------------------------
    // 旧モデル（表彰式・後方互換用に残す）
    // -------------------------------------------------------------------------

    /// <summary>
    /// AJS/表彰式シナリオアイテム（旧形式・表彰式で使用）
    /// </summary>
    public class ScreenScenarioItem
    {
        [JsonPropertyName("ScreenId")]
        public string ScreenId { get; set; } = string.Empty;

        [JsonPropertyName("EventNo")]
        public string EventNo { get; set; } = string.Empty;

        [JsonPropertyName("EventSymbol")]
        public string EventSymbol { get; set; } = string.Empty;

        [JsonPropertyName("HeatNo")]
        public string HeatNo { get; set; } = string.Empty;

        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{ScreenId} - {Description}";
        }
    }

    /// <summary>
    /// 進行シナリオのデフォルト画面サイズ ("Large" または "Small")
    /// </summary>
    public enum ProgressScenarioScreenSize { Large, Small }

    /// <summary>
    /// 進行シナリオのデフォルト表示モード ("ProgressOnly" / "Heat" / "Final")
    /// </summary>
    public enum ProgressScenarioMode { ProgressOnly, Heat, Final }

    /// <summary>
    /// 進行シナリオ
    /// </summary>
    public class ProgressScenario
    {
        [JsonPropertyName("ScenarioName")]
        public string ScenarioName { get; set; } = string.Empty;

        [JsonPropertyName("ScenarioType")]
        public string ScenarioType { get; set; } = "Progress";

        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// デフォルト画面サイズ。"Large"（大）または "Small"（小）。省略時は "Large"。
        /// 選択後でもUIのラジオボタンで切り替え可能。
        /// </summary>
        [JsonPropertyName("DefaultScreenSize")]
        public string DefaultScreenSize { get; set; } = "Large";

        /// <summary>
        /// デフォルト表示モード。"ProgressOnly"（進行のみ）/ "Heat"（ヒートも）/ "Final"（決勝）。
        /// 省略時は "ProgressOnly"。選択後でもUIのラジオボタンで切り替え可能。
        /// </summary>
        [JsonPropertyName("DefaultMode")]
        public string DefaultMode { get; set; } = "ProgressOnly";

        /// <summary>
        /// 決勝時の表示モード。ラウンド名に「決勝」を含む進行を選択したとき、このモードに自動切り替えする。
        /// "ProgressOnly" / "Heat" / "Final" のいずれか。省略（null）時は DefaultMode をそのまま使用する。
        /// </summary>
        [JsonPropertyName("FinalMode")]
        public string? FinalMode { get; set; }

        /// <summary>
        /// 使用する進行画面ID（例: "DSP_PRG_011_B"）。
        /// 指定した場合、大/小・表示モードの設定に関わらずこの画面を使用する。省略時は従来の画面選択ロジック。
        /// </summary>
        [JsonPropertyName("ScreenId")]
        public string ScreenId { get; set; } = string.Empty;

        /// <summary>
        /// 背景設定。null の場合はデフォルト（黒）。
        /// AjsBackground と同じ型を使用するため "None" / "Image" / "Color" / "ChromaKey" を指定できる。
        /// </summary>
        [JsonPropertyName("Background")]
        public AjsBackground? Background { get; set; }

        [JsonPropertyName("Items")]
        public List<ProgressScenarioItem> Items { get; set; } = new();

        /// <summary>
        /// ラウンド番号ごとの進行フロー定義。RoundNos に一致するラウンドはその Flow で進行する。
        /// 省略可。RoundRules / DefaultRule のどちらかが定義されていれば「フロー進行画面(DSP_PRG_010)」を使用する。
        /// </summary>
        [JsonPropertyName("RoundRules")]
        public List<ProgressRoundRule> RoundRules { get; set; } = new();

        /// <summary>
        /// RoundRules のどれにも一致しないラウンドに適用するフロー定義（例: 予選）。
        /// </summary>
        [JsonPropertyName("DefaultRule")]
        public ProgressRoundRule? DefaultRule { get; set; }

        /// <summary>ラウンド別フロー定義を持つシナリオかどうか。</summary>
        [JsonIgnore]
        public bool HasFlow => DefaultRule != null || RoundRules.Count > 0;

        /// <summary>
        /// ラウンド番号に対応するルールを返す。RoundNos が一致するものを優先し、
        /// 一致しなければ DefaultRule を返す。どちらも無ければ null。
        /// </summary>
        public ProgressRoundRule? GetRuleForRound(string rndNo)
        {
            foreach (var rule in RoundRules)
            {
                if (rule.RoundNos.Contains(rndNo)) return rule;
            }
            return DefaultRule;
        }

        /// <summary>
        /// DefaultScreenSize を ProgressScenarioScreenSize 列挙型に変換する。
        /// </summary>
        public ProgressScenarioScreenSize GetScreenSize()
        {
            return DefaultScreenSize.Equals("Small", System.StringComparison.OrdinalIgnoreCase)
                ? ProgressScenarioScreenSize.Small
                : ProgressScenarioScreenSize.Large;
        }

        /// <summary>
        /// DefaultMode を ProgressScenarioMode 列挙型に変換する。
        /// </summary>
        public ProgressScenarioMode GetMode()
            => ParseMode(DefaultMode);

        /// <summary>
        /// ラウンド名に応じたモードを返す。
        /// ラウンド名に「決勝」を含み、かつ FinalMode が設定されている場合は FinalMode を使用する。
        /// それ以外は DefaultMode を使用する。
        /// </summary>
        public ProgressScenarioMode GetModeForRound(string roundName)
        {
            if (!string.IsNullOrEmpty(FinalMode) && roundName.Contains("決勝"))
                return ParseMode(FinalMode);
            return ParseMode(DefaultMode);
        }

        private static ProgressScenarioMode ParseMode(string? mode)
        {
            return (mode ?? string.Empty).ToUpperInvariant() switch
            {
                "HEAT"  => ProgressScenarioMode.Heat,
                "FINAL" => ProgressScenarioMode.Final,
                _       => ProgressScenarioMode.ProgressOnly,
            };
        }
    }

    /// <summary>
    /// ラウンド別の進行フロー定義。
    /// </summary>
    public class ProgressRoundRule
    {
        /// <summary>このルールを適用するラウンド番号（例: "200"）。DefaultRule では省略する。</summary>
        [JsonPropertyName("RoundNos")]
        public List<string> RoundNos { get; set; } = new();

        /// <summary>
        /// フロー要素。Type は次のとおり（Title → Player → Heat の順に1つずつ定義する）。
        ///   TitleIntro  : ラウンド開始時に1回だけ区分名紹介を表示する。
        ///   PlayerIntro : 各 HeatIntro の直前に出場選手紹介を挿入する。
        ///   HeatIntro   : ヒート（または種目）紹介。繰り返しの単位。
        /// </summary>
        [JsonPropertyName("Flow")]
        public List<ProgressFlowStep> Flow { get; set; } = new();
    }

    /// <summary>
    /// 進行フローの1要素。Type により使用するプロパティが異なる。
    /// </summary>
    public class ProgressFlowStep
    {
        /// <summary>"TitleIntro" / "PlayerIntro" / "HeatIntro"</summary>
        [JsonPropertyName("Type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>[TitleIntro] 表示秒数。経過後に次へ進む。</summary>
        [JsonPropertyName("Seconds")]
        public int Seconds { get; set; } = 5;

        /// <summary>
        /// [PlayerIntro] true のとき、全種目のヒート数が1つ（＝全員が同じ組）なら最初の種目でのみ表示し、
        /// 2種目目以降は表示しない（準決勝・決勝用）。
        /// </summary>
        [JsonPropertyName("SingleHeatFirstDanceOnly")]
        public bool SingleHeatFirstDanceOnly { get; set; } = false;

        /// <summary>[PlayerIntro] 1ページに表示する組数。</summary>
        [JsonPropertyName("PageSize")]
        public int PageSize { get; set; } = 10;

        /// <summary>[PlayerIntro] 1ページの表示秒数。最終ページ表示後もこの秒数で次へ進む。</summary>
        [JsonPropertyName("PageSeconds")]
        public int PageSeconds { get; set; } = 5;

        /// <summary>[HeatIntro] "Heat"（ヒート単位。タイトルは「現在 S 1H」）または "Dance"（種目単位。「現在 W」）。</summary>
        [JsonPropertyName("Unit")]
        public string Unit { get; set; } = "Heat";

        /// <summary>[HeatIntro] 右下に Next（次のヒート/種目）を表示するか。</summary>
        [JsonPropertyName("ShowNext")]
        public bool ShowNext { get; set; } = true;

        /// <summary>[HeatIntro] 背番号が1行に収まる最大文字数。超えると2行に分けて表示する。</summary>
        [JsonPropertyName("RowMaxChars")]
        public int RowMaxChars { get; set; } = 30;
    }

    /// <summary>
    /// 表彰式シナリオ（旧形式）
    /// </summary>
    public class ScreenScenario
    {
        [JsonPropertyName("ScenarioName")]
        public string ScenarioName { get; set; } = string.Empty;

        [JsonPropertyName("ScenarioType")]
        public string ScenarioType { get; set; } = "AJS";

        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("Items")]
        public List<ScreenScenarioItem> Items { get; set; } = new();
    }
}

// Made with Bob
