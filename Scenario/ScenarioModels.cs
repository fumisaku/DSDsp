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
