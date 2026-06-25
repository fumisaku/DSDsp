using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DSDsp.Scenario
{
    /// <summary>
    /// シナリオ管理クラス
    /// </summary>
    public class ScenarioManager
    {
        private readonly LOG_C _log;
        private readonly string _scenarioPath;

        public ScenarioManager(LOG_C log, string scenarioPath)
        {
            _log = log;
            _scenarioPath = scenarioPath;
        }

        /// <summary>
        /// 指定されたタイプのシナリオファイル一覧を取得
        /// </summary>
        public List<string> GetScenarioFiles(ScenarioType type)
        {
            try
            {
                if (!Directory.Exists(_scenarioPath))
                {
                    _log.LogAdd($"シナリオフォルダが存在しません: {_scenarioPath}", _log.WARNING);
                    return new List<string>();
                }

                var prefix = type switch
                {
                    ScenarioType.Progress => "進行_",
                    ScenarioType.AJS => "AJS_",
                    ScenarioType.Award => "表彰式_",
                    _ => ""
                };

                var files = Directory.GetFiles(_scenarioPath, $"{prefix}*.json")
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .Cast<string>()
                    .ToList();

                _log.LogAdd($"{type}シナリオファイル: {files.Count}件", _log.DEBUG);
                return files;
            }
            catch (Exception ex)
            {
                _log.LogAdd($"シナリオファイル一覧取得エラー: {ex.Message}", _log.ERR);
                return new List<string>();
            }
        }

        /// <summary>
        /// 進行シナリオを読み込み
        /// </summary>
        public ProgressScenario? LoadProgressScenario(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_scenarioPath, fileName);
                if (!File.Exists(filePath))
                {
                    _log.LogAdd($"シナリオファイルが存在しません: {filePath}", _log.ERR);
                    return null;
                }

                var json = File.ReadAllText(filePath);
                var scenario = JsonSerializer.Deserialize<ProgressScenario>(json);

                if (scenario == null)
                {
                    _log.LogAdd($"シナリオファイルの読み込みに失敗: {fileName}", _log.ERR);
                    return null;
                }

                _log.LogAdd($"進行シナリオ読み込み: {scenario.ScenarioName} ({scenario.Items.Count}項目)", _log.INFO);
                return scenario;
            }
            catch (Exception ex)
            {
                _log.LogAdd($"シナリオ読み込みエラー [{fileName}]: {ex.Message}", _log.ERR);
                return null;
            }
        }

        /// <summary>
        /// AJS/表彰式シナリオを読み込み
        /// </summary>
        public ScreenScenario? LoadScreenScenario(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_scenarioPath, fileName);
                if (!File.Exists(filePath))
                {
                    _log.LogAdd($"シナリオファイルが存在しません: {filePath}", _log.ERR);
                    return null;
                }

                var json = File.ReadAllText(filePath);
                var scenario = JsonSerializer.Deserialize<ScreenScenario>(json);

                if (scenario == null)
                {
                    _log.LogAdd($"シナリオファイルの読み込みに失敗: {fileName}", _log.ERR);
                    return null;
                }

                _log.LogAdd($"画面シナリオ読み込み: {scenario.ScenarioName} ({scenario.Items.Count}項目)", _log.INFO);
                return scenario;
            }
            catch (Exception ex)
            {
                _log.LogAdd($"シナリオ読み込みエラー [{fileName}]: {ex.Message}", _log.ERR);
                return null;
            }
        }

        /// <summary>
        /// シナリオから区分一覧を取得（AJS/表彰式用）
        /// </summary>
        public List<string> GetCategories(ScreenScenario scenario)
        {
            try
            {
                var categories = scenario.Items
                    .Select(item => $"{item.EventNo}-{item.EventSymbol}")
                    .Distinct()
                    .ToList();

                return categories;
            }
            catch (Exception ex)
            {
                _log.LogAdd($"区分一覧取得エラー: {ex.Message}", _log.ERR);
                return new List<string>();
            }
        }

        /// <summary>
        /// 指定された区分の画面進行アイテムを取得
        /// </summary>
        public List<ScreenScenarioItem> GetScreenItems(ScreenScenario scenario, string eventNo, string eventSymbol)
        {
            try
            {
                var items = scenario.Items
                    .Where(item => item.EventNo == eventNo && item.EventSymbol == eventSymbol)
                    .ToList();

                return items;
            }
            catch (Exception ex)
            {
                _log.LogAdd($"画面進行アイテム取得エラー: {ex.Message}", _log.ERR);
                return new List<ScreenScenarioItem>();
            }
        }

        /// <summary>
        /// DA_MasterからAJS採点方式の区分・ラウンドを取得
        /// </summary>
        /// <param name="daMaster">DA_Masterデータ</param>
        /// <returns>区分情報のリスト（表示形式: "区分番号 区分名 ラウンド名"、内部キー: "区分No-ラウンドNo"）</returns>
        public List<string> GetAjsCategoriesFromDaMaster(System.Text.Json.Nodes.JsonNode daMaster)
        {
            var categories = new List<string>();
            var addedKeys = new HashSet<string>(); // 重複チェック用
            
            try
            {
                // JsonNodeをJsonElementに変換
                var jsonString = daMaster.ToJsonString();
                var jsonDoc = JsonDocument.Parse(jsonString);
                var root = jsonDoc.RootElement;

                // DB_KUBUNs配列を取得
                if (!root.TryGetProperty("DB_KUBUNs", out var kubuns) || kubuns.ValueKind != JsonValueKind.Array)
                {
                    _log.LogAdd("DB_KUBUNsが見つかりません", _log.WARNING);
                    return categories;
                }

                foreach (var kubun in kubuns.EnumerateArray())
                {
                    var kbnNo = kubun.GetProperty("DB_KbnNo").GetString() ?? "";
                    var kbnName = kubun.GetProperty("DB_KbnName").GetString() ?? "";

                    // DC_ROUNDs配列を取得
                    if (!kubun.TryGetProperty("DC_ROUNDs", out var rounds) || rounds.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var round in rounds.EnumerateArray())
                    {
                        var scrMtd = round.GetProperty("DC_RndScrMtd").GetString() ?? "";
                        
                        // AJSで始まる採点方式のみをフィルタリング
                        if (!scrMtd.StartsWith("AJS", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var roundName = round.GetProperty("DC_RndName_J").GetString() ?? "";
                        var roundNo = round.GetProperty("DC_RndNo").GetString() ?? "";

                        // 内部識別用のキー（区分No-ラウンドNo）
                        var key = $"{kbnNo}-{roundNo}";
                        
                        // 重複チェック（同じ区分・ラウンドは1回のみ追加）
                        if (addedKeys.Contains(key))
                            continue;
                        
                        addedKeys.Add(key);

                        // 表示形式: "区分番号 区分名 ラウンド名"
                        var displayText = $"{kbnNo} {kbnName} {roundName}";
                        categories.Add($"{key}|{displayText}");
                        
                        _log.LogAdd($"AJS区分追加: {displayText} (採点方式: {scrMtd})", _log.DEBUG);
                    }
                }

                _log.LogAdd($"AJS区分取得完了: {categories.Count}件", _log.INFO);
            }
            catch (Exception ex)
            {
                _log.LogAdd($"AJS区分取得エラー: {ex.Message}", _log.ERR);
            }

            return categories;
        }
    }
}

// Made with Bob
