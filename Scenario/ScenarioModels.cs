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

    /// <summary>
    /// AJS/表彰式シナリオアイテム
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
    /// AJS/表彰式シナリオ
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
