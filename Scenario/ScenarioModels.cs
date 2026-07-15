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
    }

    /// <summary>
    /// シナリオの背景設定。
    /// Type に応じて ImageFile または R/G/B を使用する。
    /// </summary>
    public class AjsBackground
    {
        /// <summary>
        /// 背景タイプ。"None" / "Image" / "Color" のいずれかを指定する。
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
                "IMAGE" => AjsBackgroundType.Image,
                "COLOR" => AjsBackgroundType.Color,
                _       => AjsBackgroundType.None,
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
    }

    /// <summary>
    /// AJSシナリオ定義ファイルの競技種別毎の画面グループ
    /// キーは画面ID（例: "DSP_TIT_002"）
    /// </summary>
    public class AjsScreenGroup : Dictionary<string, AjsScreenEntry>
    {
        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// AJSシナリオ定義ファイルの Screens セクション
    /// </summary>
    public class AjsScreens
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

        /// <summary>説明（表示用）</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// デュエルヒート表の一覧表示モード。
        /// true の場合、DSP_GRP_001 は種目内の全ヒート選手一覧を表示し、
        /// 順位列にヒート番号（"1H", "2H" など）を表示する。
        /// </summary>
        public bool IsOverviewMode { get; set; } = false;

        public override string ToString()
        {
            if (DanceNo == 0 && HeatNo == 0)
                return $"{ScreenId}  {Description}";
            if (HeatNo == 0)
                return $"{ScreenId}  種目{DanceNo}  {Description}";
            return $"{ScreenId}  種目{DanceNo}  ヒート{HeatNo}  {Description}";
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

        [JsonPropertyName("Items")]
        public List<ProgressScenarioItem> Items { get; set; } = new();
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
