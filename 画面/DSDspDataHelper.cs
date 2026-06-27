using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSDsp.画面
{
    /// <summary>
    /// DA_MasterとDS_Statusからデータを取得するヘルパークラス
    /// </summary>
    public static class DSDspDataHelper
    {
        /// <summary>
        /// 競技会名を取得
        /// </summary>
        public static string Get競技会名(JsonNode? daMaster)
        {
            if (daMaster == null) return "データなし";
            return daMaster["DA_CompName"]?.ToString() ?? "競技会名不明";
        }

        /// <summary>
        /// 区分情報を取得
        /// </summary>
        public static JsonNode? Get区分(JsonNode? daMaster, string kbnNo)
        {
            if (daMaster == null) return null;
            
            var kubuns = daMaster["DB_KUBUNs"]?.AsArray();
            if (kubuns == null) return null;
            
            return kubuns.FirstOrDefault(k => k?["DB_KbnNo"]?.ToString() == kbnNo);
        }

        /// <summary>
        /// 区分名を取得
        /// </summary>
        public static string Get区分名(JsonNode? daMaster, string kbnNo)
        {
            var kubun = Get区分(daMaster, kbnNo);
            if (kubun == null) return "区分情報なし";
            
            return kubun["DB_KbnName"]?.ToString() ?? "区分不明";
        }

        /// <summary>
        /// ラウンド情報を取得
        /// </summary>
        public static JsonNode? Getラウンド(JsonNode? daMaster, string kbnNo, string rndNo)
        {
            var kubun = Get区分(daMaster, kbnNo);
            if (kubun == null) return null;
            
            var rounds = kubun["DC_ROUNDs"]?.AsArray();
            if (rounds == null) return null;
            
            return rounds.FirstOrDefault(r => r?["DC_RndNo"]?.ToString() == rndNo);
        }

        /// <summary>
        /// ラウンド名を取得
        /// </summary>
        public static string Getラウンド名(JsonNode? daMaster, string kbnNo, string rndNo)
        {
            var round = Getラウンド(daMaster, kbnNo, rndNo);
            if (round == null) return "ラウンド情報なし";
            
            return round["DC_RndName_J"]?.ToString() ?? "ラウンド不明";
        }

        /// <summary>
        /// 種目情報を取得
        /// </summary>
        public static JsonNode? Get種目(JsonNode? daMaster, string kbnNo, string rndNo, int dncNo)
        {
            var round = Getラウンド(daMaster, kbnNo, rndNo);
            if (round == null) return null;
            
            var dgrps = round["DD_DGRPs"]?.AsArray();
            if (dgrps == null || dgrps.Count == 0) return null;
            
            var dgrp = dgrps[0]; // 通常は1つ目のDGrpを使用
            var dances = dgrp?["DE_DANCEs"]?.AsArray();
            if (dances == null) return null;
            
            return dances.FirstOrDefault(d => d?["DE_DncNo"]?.GetValue<int>() == dncNo);
        }

        /// <summary>
        /// 種目名を取得
        /// </summary>
        public static string Get種目名(JsonNode? daMaster, string kbnNo, string rndNo, int dncNo)
        {
            var dance = Get種目(daMaster, kbnNo, rndNo, dncNo);
            if (dance == null) return "種目情報なし";
            
            return dance["DE_DncNm_J"]?.ToString() ?? "種目不明";
        }

        /// <summary>
        /// 種目カテゴリを取得（ソロ/デュエット/グループ）
        /// </summary>
        public static string Get種目カテゴリ(JsonNode? daMaster, string kbnNo, string rndNo, int dncNo)
        {
            var dance = Get種目(daMaster, kbnNo, rndNo, dncNo);
            if (dance == null) return "";
            
            string 種目SG = dance["DE_DncSG"]?.ToString() ?? "";
            return 種目SG switch
            {
                "Solo" => "ソロ競技",
                "Duel" => "デュエル競技",
                "Group" => "グループ競技",
                _ => ""
            };
        }

        /// <summary>
        /// 採点方式IDを取得（DC_RndScrMtdID）
        /// </summary>
        public static string Get採点方式ID(JsonNode? daMaster, string kbnNo, string rndNo)
        {
            var round = Getラウンド(daMaster, kbnNo, rndNo);
            if (round == null) return string.Empty;
            return round["DC_RndScrMtdID"]?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 種目の表示テキストを取得 — 例: "1種目目 ソロ競技 ワルツ"
        /// 種目が存在しない場合は "種目情報なし" を返す
        /// </summary>
        public static string Get種目表示テキスト(JsonNode? daMaster, string kbnNo, string rndNo, int dncNo)
        {
            var dance = Get種目(daMaster, kbnNo, rndNo, dncNo);
            if (dance == null) return "種目情報なし";

            string 種目カテゴリ = Get種目カテゴリ(daMaster, kbnNo, rndNo, dncNo);
            string 種目名 = Get種目名(daMaster, kbnNo, rndNo, dncNo);
            return $"{dncNo}種目目 {種目カテゴリ} {種目名}";
        }

        /// <summary>
        /// 選手情報を取得（背番号から検索）
        /// </summary>
        public static JsonNode? Get選手情報(JsonNode? daMaster, string 背番号)
        {
            if (daMaster == null) return null;
            
            var members = daMaster["DM_MEMBERs"]?.AsArray();
            if (members == null) return null;
            
            foreach (var member in members)
            {
                var masters = member?["DM_MASTERs"]?.AsArray();
                if (masters != null)
                {
                    foreach (var master in masters)
                    {
                        if (master?["DM_No"]?.ToString() == 背番号)
                        {
                            return master;
                        }
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// 選手名（リーダー）を取得
        /// </summary>
        public static string Get選手名L(JsonNode? 選手情報)
        {
            if (選手情報 == null) return "名前不明";
            return 選手情報["DM_LDispName"]?.ToString() ?? 選手情報["DM_LName"]?.ToString() ?? "名前不明";
        }

        /// <summary>
        /// 選手名（パートナー）を取得
        /// </summary>
        public static string Get選手名P(JsonNode? 選手情報)
        {
            if (選手情報 == null) return "";
            return 選手情報["DM_PDispName"]?.ToString() ?? 選手情報["DM_PName"]?.ToString() ?? "";
        }

        /// <summary>
        /// 所属を取得
        /// </summary>
        public static string Get所属(JsonNode? 選手情報)
        {
            if (選手情報 == null) return "";
            return 選手情報["DM_Ctry"]?.ToString() ?? "";
        }

        /// <summary>
        /// DS_Statusからヒート内の背番号を取得
        /// </summary>
        public static string Get背番号FromHeat(JsonNode? dsStatus, string kbnNo, string rndNo, int dncNo, int heatNo)
        {
            if (dsStatus == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] dsStatus is null");
                return "???";
            }
            
            System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] 検索: 区分={kbnNo}, ラウンド={rndNo}, 種目={dncNo}, ヒート={heatNo}");
            
            // DS_FLOORs（大文字）で検索
            var floors = dsStatus["DS_FLOORs"]?.AsArray();
            if (floors == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] DS_FLOORs is null");
                return "???";
            }
            
            System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] Floors count: {floors.Count}");
            
            foreach (var floor in floors)
            {
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] PRGRSs count: {prgrs.Count}");
                    
                    foreach (var prg in prgrs)
                    {
                        var prgKbnNo = prg?["DS_KbnNo"]?.ToString();
                        var prgRndNo = prg?["DS_RndNo"]?.ToString();
                        
                        System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] PRGRS: 区分={prgKbnNo}, ラウンド={prgRndNo}");
                        
                        if (prgKbnNo == kbnNo && prgRndNo == rndNo)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] 区分・ラウンド一致");
                            
                            var prgDances = prg?["DS_PRGDANCEs"]?.AsArray();
                            if (prgDances != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] PRGDANCEs count: {prgDances.Count}");
                                
                                foreach (var prgDance in prgDances)
                                {
                                    var prgDncNo = prgDance?["DS_DncNo"]?.GetValue<int>();
                                    System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] PRGDANCE: 種目={prgDncNo}");
                                    
                                    if (prgDncNo == dncNo)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] 種目一致");
                                        
                                        var heats = prgDance?["DS_PRGHEATs"]?.AsArray();
                                        if (heats != null)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] PRGHEATs count: {heats.Count}, 検索ヒート: {heatNo}");
                                            
                                            if (heatNo > 0 && heatNo <= heats.Count)
                                            {
                                                var heat = heats[heatNo - 1];
                                                var heatId = heat?["DS_HeatId"]?.ToString();
                                                
                                                if (!string.IsNullOrEmpty(heatId))
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] HeatId: {heatId}");
                                                    
                                                    // PlayerAssignmentsから該当のHeatIdを持つPlayerNoを検索
                                                    var playerAssignments = prg?["PlayerAssignments"]?.AsArray();
                                                    if (playerAssignments != null)
                                                    {
                                                        System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] PlayerAssignments count: {playerAssignments.Count}");
                                                        
                                                        foreach (var assignment in playerAssignments)
                                                        {
                                                            var assignedHeatIds = assignment?["AssignedHeatIds"]?.AsArray();
                                                            if (assignedHeatIds != null)
                                                            {
                                                                foreach (var assignedHeatId in assignedHeatIds)
                                                                {
                                                                    if (assignedHeatId?.ToString() == heatId)
                                                                    {
                                                                        var 背番号 = assignment?["PlayerNo"]?.ToString() ?? "???";
                                                                        System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] 背番号取得: {背番号}");
                                                                        return 背番号;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        
                                                        System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] HeatIdに一致するPlayerNoが見つかりませんでした");
                                                    }
                                                    else
                                                    {
                                                        System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] PlayerAssignments is null");
                                                    }
                                                }
                                                else
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] HeatId is null or empty");
                                                }
                                            }
                                            else
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] ヒート番号が範囲外");
                                            }
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] PRGHEATs is null");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] PRGDANCEs is null");
                            }
                        }
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[Get背番号FromHeat] 背番号が見つかりませんでした");
            return "???";
        }
        /// <summary>
        /// DS_Statusから指定ヒートに出場する背番号リストを取得
        /// </summary>
        public static List<string> Get背番号リストFromHeat(JsonNode? dsStatus, string kbnNo, string rndNo, int dncNo, int heatNo)
        {
            var result = new List<string>();
            if (dsStatus == null) return result;

            var floors = dsStatus["DS_FLOORs"]?.AsArray();
            if (floors == null) return result;

            foreach (var floor in floors)
            {
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) continue;

                foreach (var prg in prgrs)
                {
                    if (prg?["DS_KbnNo"]?.ToString() != kbnNo || prg?["DS_RndNo"]?.ToString() != rndNo)
                        continue;

                    var prgDances = prg?["DS_PRGDANCEs"]?.AsArray();
                    if (prgDances == null) continue;

                    foreach (var prgDance in prgDances)
                    {
                        if (prgDance?["DS_DncNo"]?.GetValue<int>() != dncNo) continue;

                        var heats = prgDance?["DS_PRGHEATs"]?.AsArray();
                        if (heats == null || heatNo < 1 || heatNo > heats.Count) continue;

                        var heat = heats[heatNo - 1];
                        var heatId = heat?["DS_HeatId"]?.ToString();
                        if (string.IsNullOrEmpty(heatId)) continue;

                        var playerAssignments = prg?["PlayerAssignments"]?.AsArray();
                        if (playerAssignments == null) continue;

                        foreach (var assignment in playerAssignments)
                        {
                            var assignedHeatIds = assignment?["AssignedHeatIds"]?.AsArray();
                            if (assignedHeatIds == null) continue;

                            foreach (var id in assignedHeatIds)
                            {
                                if (id?.ToString() == heatId)
                                {
                                    var no = assignment?["PlayerNo"]?.ToString();
                                    if (!string.IsNullOrEmpty(no))
                                        result.Add(no!);
                                    break;
                                }
                            }
                        }
                        return result;
                    }
                }
            }
            return result;
        }
    }
}

// Made with Bob
