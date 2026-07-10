using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DSDsp.Scenario
{
    /// <summary>
    /// シナリオ管理クラス
    /// </summary>
    public class ScenarioManager
    {
        private readonly LOG_C _log;
        private readonly string _scenarioPath;

        // 大/小ペア定義（同一ロール内でどちらか一方のみ Enabled:true であるべき画面IDペア）
        // キー: 競技種別, 値: (大の画面ID, 小の画面ID) のリスト
        private static readonly Dictionary<string, List<(string large, string small)>> _screenPairs = new()
        {
            ["Common"] = new()
            {
                ("DSP_COM_001", "DSP_COM_002"),
            },
            ["Solo"] = new()
            {
                ("DSP_TIT_002", "DSP_TIT_003"),
                ("DSP_SOL_001", "DSP_SOL_002"),
                ("DSP_SOL_003", "DSP_SOL_004"),
                ("DSP_SOL_005", "DSP_SOL_006"),
                ("DSP_SOL_007", "DSP_SOL_008"),
            },
            ["Group"] = new()
            {
                ("DSP_TIT_002", "DSP_TIT_003"),
                ("DSP_GRP_001", "DSP_GRP_002"),
                ("DSP_GRP_003", "DSP_GRP_004"),
            },
            ["Duel"] = new()
            {
                ("DSP_TIT_002", "DSP_TIT_003"),
                ("DSP_TIT_006", "DSP_TIT_007"),
                ("DSP_DUE_001", "DSP_DUE_002"),
                ("DSP_SOL_007", "DSP_SOL_008"),
            },
        };

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

        // -------------------------------------------------------------------------
        // AJSシナリオ定義ファイル読み込み・バリデーション・画面進行一覧生成
        // -------------------------------------------------------------------------

        /// <summary>
        /// AJSシナリオ定義ファイルを読み込む
        /// </summary>
        /// <returns>読み込み成功時は AjsScenarioDefinition、失敗時は null</returns>
        public AjsScenarioDefinition? LoadAjsScenario(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_scenarioPath, fileName);
                if (!File.Exists(filePath))
                {
                    _log.LogAdd($"AJSシナリオファイルが存在しません: {filePath}", _log.ERR);
                    return null;
                }

                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var scenario = JsonSerializer.Deserialize<AjsScenarioDefinition>(json, options);

                if (scenario == null)
                {
                    _log.LogAdd($"AJSシナリオファイルの読み込みに失敗: {fileName}", _log.ERR);
                    return null;
                }

                // バリデーション
                var errors = ValidateAjsScenario(scenario);
                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                        _log.LogAdd($"AJSシナリオ定義エラー [{fileName}]: {error}", _log.ERR);
                    return null;
                }

                _log.LogAdd($"AJSシナリオ読み込み完了: {scenario.ScenarioName}", _log.INFO);
                return scenario;
            }
            catch (Exception ex)
            {
                _log.LogAdd($"AJSシナリオ読み込みエラー [{fileName}]: {ex.Message}", _log.ERR);
                return null;
            }
        }

        /// <summary>
        /// AJSシナリオ定義ファイルのバリデーション。
        /// 同一ロールの大/小ペアが両方 Enabled:true の場合はエラー。
        /// </summary>
        /// <returns>エラーメッセージのリスト。空の場合は正常。</returns>
        private List<string> ValidateAjsScenario(AjsScenarioDefinition scenario)
        {
            var errors = new List<string>();

            var groups = new Dictionary<string, Dictionary<string, AjsScreenEntry>>
            {
                ["Common"] = scenario.Screens.Common,
                ["Solo"]   = scenario.Screens.Solo,
                ["Group"]  = scenario.Screens.Group,
                ["Duel"]   = scenario.Screens.Duel,
            };

            foreach (var (groupName, pairs) in _screenPairs)
            {
                if (!groups.TryGetValue(groupName, out var group)) continue;

                foreach (var (large, small) in pairs)
                {
                    bool largeEnabled = group.TryGetValue(large, out var largeEntry) && largeEntry.Enabled;
                    bool smallEnabled = group.TryGetValue(small, out var smallEntry) && smallEntry.Enabled;

                    if (largeEnabled && smallEnabled)
                    {
                        errors.Add($"{groupName} セクション: {large} と {small} が両方 Enabled:true です。どちらか一方のみ有効にしてください。");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// AJSシナリオ定義とDS_Statusから、指定された区分・ラウンドの画面進行一覧を生成する。
        /// </summary>
        /// <param name="scenario">AJSシナリオ定義</param>
        /// <param name="dsStatus">DS_Status の JsonNode</param>
        /// <param name="daMaster">DA_Master の JsonNode</param>
        /// <param name="kbnNo">区分番号</param>
        /// <param name="roundNo">ラウンド番号</param>
        /// <returns>
        /// 画面進行一覧。エラー時は null を返す。
        /// エラー内容はログに出力する。
        /// </returns>
        public List<AjsProgressItem>? BuildProgressList(
            AjsScenarioDefinition scenario,
            JsonNode dsStatus,
            JsonNode daMaster,
            string kbnNo,
            string roundNo)
        {
            try
            {
                // DS_Status から該当進行（DS_PRGRS_J）を取得
                var prgrs = FindPrgrs(dsStatus, kbnNo, roundNo);
                if (prgrs == null)
                {
                    _log.LogAdd($"DS_Statusに指定の区分・ラウンドが見つかりません: 区分={kbnNo}, ラウンド={roundNo}", _log.ERR);
                    return null;
                }

                // DA_Master から採点方式（DC_RndScrMtd）を取得
                var scrMtd = GetScrMtd(daMaster, kbnNo, roundNo);
                if (string.IsNullOrEmpty(scrMtd))
                {
                    _log.LogAdd($"DA_Masterから採点方式が取得できません: 区分={kbnNo}, ラウンド={roundNo}", _log.ERR);
                    return null;
                }

                // GD/PD を判定
                bool isGd = scrMtd.IndexOf("for GD", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isPd = scrMtd.IndexOf("for PD", StringComparison.OrdinalIgnoreCase) >= 0;

                // DA_Master から種目情報（DE_DncSG）マップを取得
                // キー: DS_DncNo（DS_Status上の種目順）→ DE_DncSG 値
                var dncSgMap = BuildDncSgMap(daMaster, kbnNo, roundNo);

                // DS_Status から種目リストを取得
                var dances = prgrs["DS_PRGDANCEs"]?.AsArray();
                if (dances == null || dances.Count == 0)
                {
                    _log.LogAdd($"DS_Statusに種目情報がありません: 区分={kbnNo}, ラウンド={roundNo}", _log.ERR);
                    return null;
                }

                // 種目リストを DS_DncNo 昇順でソート
                var sortedDances = dances
                    .Where(d => d != null)
                    .OrderBy(d => d!["DS_DncNo"]?.GetValue<int>() ?? 0)
                    .ToList();

                int totalDances = sortedDances.Count;
                var result = new List<AjsProgressItem>();

                // 1. TIT_001（最初に1回）
                AddIfEnabled(result, scenario.Screens.Common, "DSP_TIT_001", 0, 0, "区分ラウンド紹介");

                for (int danceIdx = 0; danceIdx < sortedDances.Count; danceIdx++)
                {
                    var dance = sortedDances[danceIdx]!;
                    int danceNo = dance["DS_DncNo"]?.GetValue<int>() ?? 0;

                    // 種目の SG 種別を取得
                    if (!dncSgMap.TryGetValue(danceNo, out var dncSg) || string.IsNullOrEmpty(dncSg))
                    {
                        _log.LogAdd($"種目{danceNo}の DE_DncSG が取得できません", _log.ERR);
                        return null;
                    }

                    var groupScreens = dncSg switch
                    {
                        "S" or "Solo" => scenario.Screens.Solo,
                        "G" or "Group" => scenario.Screens.Group,
                        "D" or "Duel" => scenario.Screens.Duel,
                        _ => null
                    };

                    if (groupScreens == null)
                    {
                        _log.LogAdd($"種目{danceNo}: DE_DncSG='{dncSg}' は未対応の値です（S/Solo/G/Group/D/Duel のいずれかである必要があります）", _log.ERR);
                        return null;
                    }

                    // 3. 種目先頭で種目紹介（TIT_002 or TIT_003）
                    AddFirstEnabled(result, groupScreens,
                        new[] { "DSP_TIT_002", "DSP_TIT_003" }, danceNo, 0, "種目紹介");

                    // 4. ヒートリストを取得して昇順ソート
                    var heats = dance["DS_PRGHEATs"]?.AsArray();
                    if (heats == null || heats.Count == 0)
                    {
                        _log.LogAdd($"種目{danceNo}: DS_PRGHEATsが空です", _log.WARNING);
                        continue;
                    }

                    var sortedHeats = heats
                        .Where(h => h != null)
                        .OrderBy(h => h!["DS_HeatNo"]?.GetValue<int>() ?? 0)
                        .ToList();

                    foreach (var heat in sortedHeats)
                    {
                        int heatNo = heat!["DS_HeatNo"]?.GetValue<int>() ?? 0;

                        if (dncSg is "S" or "Solo")
                        {
                            // ソロ競技
                            AddFirstEnabled(result, groupScreens,
                                new[] { "DSP_SOL_001", "DSP_SOL_002" }, danceNo, heatNo, "ソロ選手紹介");

                            if (isGd)
                                AddFirstEnabled(result, groupScreens,
                                    new[] { "DSP_SOL_003", "DSP_SOL_004" }, danceNo, heatNo, "ソロ選手結果GD");
                            else if (isPd)
                                AddFirstEnabled(result, groupScreens,
                                    new[] { "DSP_SOL_005", "DSP_SOL_006" }, danceNo, heatNo, "ソロ選手結果PD");

                            AddFirstEnabled(result, groupScreens,
                                new[] { "DSP_SOL_007", "DSP_SOL_008" }, danceNo, heatNo, "ソロ途中結果");
                        }
                        else if (dncSg is "G" or "Group")
                        {
                            // グループ競技
                            AddFirstEnabled(result, groupScreens,
                                new[] { "DSP_GRP_001", "DSP_GRP_002" }, danceNo, heatNo, "グループ出場選手");

                            AddFirstEnabled(result, groupScreens,
                                new[] { "DSP_GRP_003", "DSP_GRP_004" }, danceNo, heatNo, "グループ結果");
                        }
                        else // "D"
                        {
                            // デュエル競技
                            AddFirstEnabled(result, groupScreens,
                                new[] { "DSP_TIT_006", "DSP_TIT_007" }, danceNo, heatNo, "デュエル選手紹介");

                            AddFirstEnabled(result, groupScreens,
                                new[] { "DSP_DUE_001", "DSP_DUE_002" }, danceNo, heatNo, "デュエル選手結果");

                            AddFirstEnabled(result, groupScreens,
                                new[] { "DSP_SOL_007", "DSP_SOL_008" }, danceNo, heatNo, "途中結果");
                        }
                    }

                    // 5. 種目終了後の途中総合結果
                    // 1種目目終了後 および 最終種目終了後 は表示しない
                    bool isFirst = (danceIdx == 0);
                    bool isLast  = (danceIdx == totalDances - 1);

                    if (!isFirst && !isLast)
                    {
                        AddFirstEnabled(result, scenario.Screens.Common,
                            new[] { "DSP_COM_001", "DSP_COM_002" }, danceNo, 0, "途中総合結果");
                    }
                }

                _log.LogAdd($"AJS画面進行一覧生成完了: {result.Count}件 (区分={kbnNo}, ラウンド={roundNo})", _log.INFO);
                return result;
            }
            catch (Exception ex)
            {
                _log.LogAdd($"AJS画面進行一覧生成エラー: {ex.Message}", _log.ERR);
                return null;
            }
        }

        // -------------------------------------------------------------------------
        // 内部ヘルパー
        // -------------------------------------------------------------------------

        /// <summary>
        /// DS_Status から指定の区分・ラウンドに対応する DS_PRGRS_J ノードを取得する
        /// </summary>
        private JsonNode? FindPrgrs(JsonNode dsStatus, string kbnNo, string roundNo)
        {
            var floors = dsStatus["DS_FLOORs"]?.AsArray();
            if (floors == null) return null;

            foreach (var floor in floors)
            {
                var prgrsArray = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrsArray == null) continue;

                foreach (var prgrs in prgrsArray)
                {
                    var pk = prgrs?["DS_KbnNo"]?.GetValue<string>() ?? "";
                    var pr = prgrs?["DS_RndNo"]?.GetValue<string>() ?? "";
                    if (pk == kbnNo && pr == roundNo)
                        return prgrs;
                }
            }

            return null;
        }

        /// <summary>
        /// DA_Master から指定の区分・ラウンドの採点方式名（DC_RndScrMtd）を取得する
        /// </summary>
        private string GetScrMtd(JsonNode daMaster, string kbnNo, string roundNo)
        {
            var kubuns = daMaster["DB_KUBUNs"]?.AsArray();
            if (kubuns == null) return string.Empty;

            foreach (var kubun in kubuns)
            {
                if ((kubun?["DB_KbnNo"]?.GetValue<string>() ?? "") != kbnNo) continue;

                var rounds = kubun?["DC_ROUNDs"]?.AsArray();
                if (rounds == null) continue;

                foreach (var round in rounds)
                {
                    if ((round?["DC_RndNo"]?.GetValue<string>() ?? "") == roundNo)
                        return round?["DC_RndScrMtd"]?.GetValue<string>() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// DA_Master から指定の区分・ラウンドの種目順 → DE_DncSG マップを構築する。
        /// DS_Status の DS_DncNo（1,2,3…）は DA_Master の DE_DncNo（種目グループ内の連番）と対応する。
        /// </summary>
        private Dictionary<int, string> BuildDncSgMap(JsonNode daMaster, string kbnNo, string roundNo)
        {
            var map = new Dictionary<int, string>();

            var kubuns = daMaster["DB_KUBUNs"]?.AsArray();
            if (kubuns == null) return map;

            foreach (var kubun in kubuns)
            {
                if ((kubun?["DB_KbnNo"]?.GetValue<string>() ?? "") != kbnNo) continue;

                var rounds = kubun?["DC_ROUNDs"]?.AsArray();
                if (rounds == null) continue;

                foreach (var round in rounds)
                {
                    if ((round?["DC_RndNo"]?.GetValue<string>() ?? "") != roundNo) continue;

                    var dgrps = round?["DD_DGRPs"]?.AsArray();
                    if (dgrps == null) continue;

                    foreach (var dgrp in dgrps)
                    {
                        var dances = dgrp?["DE_DANCEs"]?.AsArray();
                        if (dances == null) continue;

                        foreach (var dance in dances)
                        {
                            int dncNo = dance?["DE_DncNo"]?.GetValue<int>() ?? 0;
                            string sg  = dance?["DE_DncSG"]?.GetValue<string>() ?? string.Empty;
                            if (dncNo > 0)
                                map[dncNo] = sg;
                        }
                    }
                }
            }

            return map;
        }

        /// <summary>
        /// 指定グループ内の1つの画面エントリが Enabled:true の場合に result へ追加する（単一ID用）
        /// </summary>
        private static void AddIfEnabled(
            List<AjsProgressItem> result,
            Dictionary<string, AjsScreenEntry> group,
            string screenId,
            int danceNo,
            int heatNo,
            string description)
        {
            if (group.TryGetValue(screenId, out var entry) && entry.Enabled)
            {
                result.Add(new AjsProgressItem
                {
                    ScreenId    = screenId,
                    DanceNo     = danceNo,
                    HeatNo      = heatNo,
                    Description = description,
                });
            }
        }

        /// <summary>
        /// 候補画面IDリストの中から Enabled:true の最初の1件を result へ追加する（大/小ペア用）
        /// </summary>
        private static void AddFirstEnabled(
            List<AjsProgressItem> result,
            Dictionary<string, AjsScreenEntry> group,
            string[] candidates,
            int danceNo,
            int heatNo,
            string description)
        {
            foreach (var screenId in candidates)
            {
                if (group.TryGetValue(screenId, out var entry) && entry.Enabled)
                {
                    result.Add(new AjsProgressItem
                    {
                        ScreenId    = screenId,
                        DanceNo     = danceNo,
                        HeatNo      = heatNo,
                        Description = description,
                    });
                    return; // 最初の1件だけ追加
                }
            }
        }

        // -------------------------------------------------------------------------
        // DA_Masterから区分一覧取得（既存メソッド）
        // -------------------------------------------------------------------------

        /// <summary>
        /// DA_MasterからAJS採点方式の区分・ラウンドを取得
        /// </summary>
        public List<string> GetAjsCategoriesFromDaMaster(JsonNode daMaster)
        {
            var categories = new List<string>();
            var addedKeys = new HashSet<string>();

            try
            {
                var jsonString = daMaster.ToJsonString();
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonString);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("DB_KUBUNs", out var kubuns) || kubuns.ValueKind != JsonValueKind.Array)
                {
                    _log.LogAdd("DB_KUBUNsが見つかりません", _log.WARNING);
                    return categories;
                }

                foreach (var kubun in kubuns.EnumerateArray())
                {
                    var kbnNo   = kubun.GetProperty("DB_KbnNo").GetString() ?? "";
                    var kbnName = kubun.GetProperty("DB_KbnName").GetString() ?? "";

                    if (!kubun.TryGetProperty("DC_ROUNDs", out var rounds) || rounds.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var round in rounds.EnumerateArray())
                    {
                        var scrMtd = round.GetProperty("DC_RndScrMtd").GetString() ?? "";

                        if (!scrMtd.StartsWith("AJS", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var roundName = round.GetProperty("DC_RndName_J").GetString() ?? "";
                        var roundNo   = round.GetProperty("DC_RndNo").GetString() ?? "";

                        var key = $"{kbnNo}-{roundNo}";
                        if (addedKeys.Contains(key)) continue;
                        addedKeys.Add(key);

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

        // -------------------------------------------------------------------------
        // 表彰式用（既存メソッド）
        // -------------------------------------------------------------------------

        /// <summary>
        /// 表彰式シナリオを読み込み
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
        /// シナリオから区分一覧を取得（表彰式用）
        /// </summary>
        public List<string> GetCategories(ScreenScenario scenario)
        {
            try
            {
                return scenario.Items
                    .Select(item => $"{item.EventNo}-{item.EventSymbol}")
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                _log.LogAdd($"区分一覧取得エラー: {ex.Message}", _log.ERR);
                return new List<string>();
            }
        }

        /// <summary>
        /// 指定された区分の画面進行アイテムを取得（表彰式用）
        /// </summary>
        public List<ScreenScenarioItem> GetScreenItems(ScreenScenario scenario, string eventNo, string eventSymbol)
        {
            try
            {
                return scenario.Items
                    .Where(item => item.EventNo == eventNo && item.EventSymbol == eventSymbol)
                    .ToList();
            }
            catch (Exception ex)
            {
                _log.LogAdd($"画面進行アイテム取得エラー: {ex.Message}", _log.ERR);
                return new List<ScreenScenarioItem>();
            }
        }
    }
}

// Made with Bob
