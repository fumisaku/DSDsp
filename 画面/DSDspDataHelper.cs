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
        /// DS_PrgPStaTM などの日時文字列から時刻部分 (HH:mm) のみを返す。
        /// 解析に失敗した場合は元の文字列をそのまま返す。
        /// </summary>
        public static string ExtractTimeOnly(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (DateTime.TryParse(value, out var dt))
                return dt.ToString("HH:mm");
            // "HH:mm" だけの文字列は長さ5以下なのでそのまま返す
            return value;
        }

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
            return $"{dncNo}種目目　{種目カテゴリ}　{種目名}";
        }

        /// <summary>
        /// 選手情報を取得（区分番号 + 背番号で検索）。
        /// 区分の DB_KbnSenM（選手マスターID）と一致する DM_MasNo を持つ DM_MASTER の中から
        /// DM_No（背番号）が一致するものを返す。
        /// kbnNo が空またはマスターIDが未設定の場合は背番号のみで全件検索する（後方互換）。
        /// </summary>
        public static JsonNode? Get選手情報(JsonNode? daMaster, string 背番号, string kbnNo = "")
        {
            if (daMaster == null) return null;

            var members = daMaster["DM_MEMBERs"]?.AsArray();
            if (members == null) return null;

            // 区分番号が指定されている場合は DB_KbnSenM（選手マスターID）を取得して絞り込む
            string? senM = null;
            if (!string.IsNullOrEmpty(kbnNo))
            {
                var kubun = Get区分(daMaster, kbnNo);
                senM = kubun?["DB_KbnSenM"]?.ToString();
            }

            foreach (var member in members)
            {
                var masters = member?["DM_MASTERs"]?.AsArray();
                if (masters == null) continue;

                foreach (var master in masters)
                {
                    // 選手マスターIDによる絞り込み（senM が取得できた場合のみ適用）
                    if (!string.IsNullOrEmpty(senM))
                    {
                        var masNo = master?["DM_MasNo"]?.ToString();
                        if (masNo != senM) continue;
                    }

                    if (master?["DM_No"]?.ToString() == 背番号)
                        return master;
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
        /// <summary>
        /// DS_Statusから指定種目の全ヒート数を取得する。
        /// ヒート情報が存在しない場合は 0 を返す。
        /// </summary>
        public static int Getヒート数(JsonNode? dsStatus, string kbnNo, string rndNo, int dncNo)
        {
            if (dsStatus == null) return 0;

            var floors = dsStatus["DS_FLOORs"]?.AsArray();
            if (floors == null) return 0;

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

                        return prgDance?["DS_PRGHEATs"]?.AsArray()?.Count ?? 0;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// DS_Statusから指定種目の全ヒート選手一覧を取得する（デュエルヒート表用）。
        /// 戻り値: (ヒート番号, 背番号) のリスト。ヒート番号昇順にソート済み。
        /// 同一ヒートに複数選手がいる場合、それぞれ個別エントリで返す。
        /// </summary>
        public static List<(int HeatNo, string PlayerNo)> Get全ヒート選手リスト(
            JsonNode? dsStatus, string kbnNo, string rndNo, int dncNo)
        {
            var result = new List<(int, string)>();
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
                        if (heats == null) return result;

                        // ヒートID → ヒート番号 の対応表を作成
                        var heatIdToNo = new System.Collections.Generic.Dictionary<string, int>();
                        foreach (var heat in heats)
                        {
                            var hId = heat?["DS_HeatId"]?.ToString();
                            var hNo = heat?["DS_HeatNo"]?.GetValue<int>() ?? 0;
                            if (!string.IsNullOrEmpty(hId))
                                heatIdToNo[hId!] = hNo;
                        }

                        // PlayerAssignments から各選手のヒート番号を収集
                        // 各選手は複数ヒートに出場するが、ここでは全選手×全ヒートの組み合わせを返す
                        // デュエルヒート表では「1ヒートに何名出場するか」ではなく
                        // 「全ヒートに誰が出るか」を一覧表示するため、各ヒートへの出場をフラットに展開する
                        var playerAssignments = prg?["PlayerAssignments"]?.AsArray();
                        if (playerAssignments == null) return result;

                        // ヒート番号 → 出場選手リスト のマップを構築
                        var heatPlayerMap = new System.Collections.Generic.Dictionary<int, List<string>>();
                        foreach (var assignment in playerAssignments)
                        {
                            var playerNo = assignment?["PlayerNo"]?.ToString();
                            if (string.IsNullOrEmpty(playerNo)) continue;

                            var assignedHeatIds = assignment?["AssignedHeatIds"]?.AsArray();
                            if (assignedHeatIds == null) continue;

                            foreach (var id in assignedHeatIds)
                            {
                                var idStr = id?.ToString();
                                if (string.IsNullOrEmpty(idStr) || !heatIdToNo.TryGetValue(idStr!, out var hNo))
                                    continue;
                                if (!heatPlayerMap.ContainsKey(hNo))
                                    heatPlayerMap[hNo] = new List<string>();
                                heatPlayerMap[hNo].Add(playerNo!);
                            }
                        }

                        // ヒート番号昇順に展開
                        foreach (var kvp in heatPlayerMap.OrderBy(x => x.Key))
                        {
                            foreach (var p in kvp.Value)
                                result.Add((kvp.Key, p));
                        }

                        return result;
                    }
                }
            }
            return result;
        }
        /// <summary>
        /// DA_Master から指定種目の種目記号（DE_DncCd）を取得する。
        /// </summary>
        public static string Get種目記号(JsonNode? daMaster, string kbnNo, string rndNo, int dncNo)
        {
            var dance = Get種目(daMaster, kbnNo, rndNo, dncNo);
            if (dance == null) return string.Empty;
            return dance["DE_DncCd"]?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// DA_Master から指定区分・ラウンドの全種目（DncNo, DncCd）リストを種目番号順に返す。
        /// dGrpNo が空でない場合は一致する DGrp のみを対象とする。
        /// </summary>
        public static List<(int DncNo, string DncCd)> Get全種目リスト(
            JsonNode? daMaster, string kbnNo, string rndNo, string dGrpNo = "")
        {
            var result = new List<(int, string)>();
            var round = Getラウンド(daMaster, kbnNo, rndNo);
            if (round == null) return result;

            var dgrps = round["DD_DGRPs"]?.AsArray();
            if (dgrps == null) return result;

            foreach (var dgrp in dgrps)
            {
                // dGrpNo が指定されている場合は一致する DGrp のみ処理する
                if (!string.IsNullOrEmpty(dGrpNo))
                {
                    var grpNo = dgrp?["DD_DGrpNo"]?.ToString() ?? string.Empty;
                    if (grpNo != dGrpNo) continue;
                }

                var dances = dgrp?["DE_DANCEs"]?.AsArray();
                if (dances == null) continue;
                foreach (var dance in dances)
                {
                    var no = dance?["DE_DncNo"]?.GetValue<int>() ?? 0;
                    var cd = dance?["DE_DncCd"]?.ToString() ?? string.Empty;
                    if (no > 0) result.Add((no, cd));
                }
            }
            result.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return result;
        }

        /// <summary>
        /// DS_Status から指定区分・ラウンドの次の進行番号情報を取得する。
        /// 現在の区分・ラウンドの最後の PRGRS より SortOrder が大きい最初の PRGRS を返す。
        /// 戻り値: (PrgNo, KbnNo, RndNo, DS_PrgPStaTM) または null。
        /// </summary>
        public static (string PrgNo, string KbnNo, string RndNo, string? PStaTM)? Get次進行情報(
            JsonNode? dsStatus, string currentKbnNo, string currentRndNo)
        {
            var list = Get次進行情報リスト(dsStatus, currentKbnNo, currentRndNo, 1);
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// DS_Status から指定区分・ラウンドの次の進行番号情報を最大 maxCount 件取得する。
        /// SortOrder 昇順で、現在の区分・ラウンドのすべてのエントリ（複数 DGrp 対応）より後のものを返す。
        /// 同一区分・ラウンドが複数 DGrp で複数 PRGRS を持つ場合でも、それら全体をスキップして
        /// 次の異なる区分・ラウンドの最初のエントリを返す。
        /// </summary>
        public static List<(string PrgNo, string KbnNo, string RndNo, string? PStaTM)> Get次進行情報リスト(
            JsonNode? dsStatus, string currentKbnNo, string currentRndNo, int maxCount = 3)
        {
            var result = new List<(string, string, string, string?)>();
            if (dsStatus == null) return result;

            var floors = dsStatus["DS_FLOORs"]?.AsArray();
            if (floors == null) return result;

            // 全 PRGRS を収集して SortOrder 昇順にソート
            var allPrgrs = new List<(int SortOrder, string PrgNo, string KbnNo, string RndNo, string? PStaTM)>();
            foreach (var floor in floors)
            {
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) continue;
                foreach (var prg in prgrs)
                {
                    var sortOrder = prg?["DS_SortOrder"]?.GetValue<int>() ?? 0;
                    var prgNo = prg?["DS_PrgNo"]?.ToString() ?? "";
                    var kbnNo = prg?["DS_KbnNo"]?.ToString() ?? "";
                    var rndNo = prg?["DS_RndNo"]?.ToString() ?? "";
                    var pStaTM = prg?["DS_PrgPStaTM"]?.ToString();
                    allPrgrs.Add((sortOrder, prgNo, kbnNo, rndNo, pStaTM));
                }
            }

            allPrgrs.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

            // 現在の区分・ラウンドを「一度でも通過した」フラグを立て、
            // その後に出現する異なる区分・ラウンドのエントリを maxCount 件収集する。
            // 同一区分・ラウンドが複数 DGrp（複数 SortOrder）を持つ場合も全てスキップされる。
            bool seenCurrent = false;
            var seenKbnRnd = new HashSet<string>();
            foreach (var p in allPrgrs)
            {
                bool isCurrent = (p.KbnNo == currentKbnNo && p.RndNo == currentRndNo);
                if (isCurrent)
                {
                    seenCurrent = true;
                    continue;
                }
                if (!seenCurrent) continue;

                // 現在の区分・ラウンドを通過した後の別区分・ラウンド
                // 同一 KbnNo+RndNo の最初のエントリのみを結果に追加（DGrp複数対応）
                var key = $"{p.KbnNo}-{p.RndNo}";
                if (seenKbnRnd.Add(key))
                {
                    result.Add((p.PrgNo, p.KbnNo, p.RndNo, p.PStaTM));
                    if (result.Count >= maxCount) break;
                }
            }

            return result;
        }

        /// <summary>
        /// DS_Status から指定区分・ラウンドの現在の進行番号を取得する。
        /// </summary>
        public static string Get現在進行番号(JsonNode? dsStatus, string kbnNo, string rndNo)
        {
            if (dsStatus == null) return string.Empty;
            var floors = dsStatus["DS_FLOORs"]?.AsArray();
            if (floors == null) return string.Empty;
            foreach (var floor in floors)
            {
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) continue;
                foreach (var prg in prgrs)
                {
                    if (prg?["DS_KbnNo"]?.ToString() == kbnNo &&
                        prg?["DS_RndNo"]?.ToString() == rndNo)
                        return prg?["DS_PrgNo"]?.ToString() ?? string.Empty;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// DS_Status の指定フロア（DS_FlrCd）の現在の進行情報を返す。
        /// 戻り値: (DS_PrgNo, DS_KbnNo, DS_RndNo) または null。
        /// </summary>
        public static (string PrgNo, string KbnNo, string RndNo)? Getフロア現在進行情報(
            JsonNode? dsStatus, JsonNode? daMaster, string flrCd)
        {
            if (dsStatus == null) return null;

            var floors = dsStatus["DS_FLOORs"]?.AsArray();
            if (floors == null) return null;

            foreach (var floor in floors)
            {
                if (floor?["DS_FlrCd"]?.ToString() != flrCd) continue;

                var curPrgNo = floor?["DS_CurPrgNo"]?.ToString();
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) continue;

                // DS_CurPrgNo と一致する PRGRS を探す
                foreach (var prg in prgrs)
                {
                    if (prg?["DS_PrgNo"]?.ToString() == curPrgNo)
                        return (curPrgNo!, prg?["DS_KbnNo"]?.ToString() ?? "", prg?["DS_RndNo"]?.ToString() ?? "");
                }
                // CurPrgNo が設定されていない場合は最初の PRGRS を返す
                var first = prgrs.FirstOrDefault();
                if (first != null)
                    return (first?["DS_PrgNo"]?.ToString() ?? "", first?["DS_KbnNo"]?.ToString() ?? "", first?["DS_RndNo"]?.ToString() ?? "");
            }
            return null;
        }

        /// <summary>
        /// DS_Status の指定フロアの次の進行番号情報を最大 maxCount 件取得する。
        /// 現在の進行番号より SortOrder が大きいものを返す。
        /// </summary>
        public static List<(string PrgNo, string KbnNo, string RndNo, string? PStaTM)> Getフロア次進行情報リスト(
            JsonNode? dsStatus, string flrCd, int maxCount = 3)
        {
            var result = new List<(string, string, string, string?)>();
            if (dsStatus == null) return result;

            var floors = dsStatus["DS_FLOORs"]?.AsArray();
            if (floors == null) return result;

            foreach (var floor in floors)
            {
                if (floor?["DS_FlrCd"]?.ToString() != flrCd) continue;

                var curPrgNo = floor?["DS_CurPrgNo"]?.ToString();
                var prgrs = floor?["DS_PRGRSs"]?.AsArray();
                if (prgrs == null) return result;

                // 全 PRGRS を SortOrder 昇順にソート
                var allPrgrs = new List<(int SortOrder, string PrgNo, string KbnNo, string RndNo, string? PStaTM)>();
                foreach (var prg in prgrs)
                {
                    var sortOrder = prg?["DS_SortOrder"]?.GetValue<int>() ?? 0;
                    var prgNo = prg?["DS_PrgNo"]?.ToString() ?? "";
                    var kbnNo = prg?["DS_KbnNo"]?.ToString() ?? "";
                    var rndNo = prg?["DS_RndNo"]?.ToString() ?? "";
                    var pStaTM = prg?["DS_PrgPStaTM"]?.ToString();
                    allPrgrs.Add((sortOrder, prgNo, kbnNo, rndNo, pStaTM));
                }
                allPrgrs.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

                // 現在の進行番号に対応する SortOrder を探す
                int curSortOrder = int.MinValue;
                if (!string.IsNullOrEmpty(curPrgNo))
                {
                    foreach (var p in allPrgrs)
                    {
                        if (p.PrgNo == curPrgNo) { curSortOrder = p.SortOrder; break; }
                    }
                }

                // 現在より後のものを maxCount 件返す
                foreach (var p in allPrgrs)
                {
                    if (p.SortOrder > curSortOrder)
                    {
                        result.Add((p.PrgNo, p.KbnNo, p.RndNo, p.PStaTM));
                        if (result.Count >= maxCount) break;
                    }
                }
                return result;
            }
            return result;
        }

        /// <summary>
        /// DS_Status から指定区分・ラウンドの全種目の全ヒート背番号マップを取得する。
        /// 戻り値: 種目番号 → (ヒート番号 → 背番号リスト) のディクショナリ。
        /// </summary>
        public static Dictionary<int, Dictionary<int, List<string>>> Get全種目全ヒート背番号マップ(
            JsonNode? dsStatus, string kbnNo, string rndNo)
        {
            var result = new Dictionary<int, Dictionary<int, List<string>>>();
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

                    var playerAssignments = prg?["PlayerAssignments"]?.AsArray();

                    foreach (var prgDance in prgDances)
                    {
                        var dncNo = prgDance?["DS_DncNo"]?.GetValue<int>() ?? 0;
                        if (dncNo <= 0) continue;

                        var heats = prgDance?["DS_PRGHEATs"]?.AsArray();
                        if (heats == null) continue;

                        // ヒートID → ヒート番号 の対応表を作成
                        var heatIdToNo = new Dictionary<string, int>();
                        foreach (var heat in heats)
                        {
                            var hId = heat?["DS_HeatId"]?.ToString();
                            var hNo = heat?["DS_HeatNo"]?.GetValue<int>() ?? 0;
                            if (!string.IsNullOrEmpty(hId)) heatIdToNo[hId!] = hNo;
                        }

                        var heatPlayerMap = new Dictionary<int, List<string>>();
                        foreach (var hNo in heatIdToNo.Values.OrderBy(n => n))
                            heatPlayerMap[hNo] = new List<string>();

                        if (playerAssignments != null)
                        {
                            foreach (var assignment in playerAssignments)
                            {
                                var playerNo = assignment?["PlayerNo"]?.ToString();
                                if (string.IsNullOrEmpty(playerNo)) continue;

                                var assignedHeatIds = assignment?["AssignedHeatIds"]?.AsArray();
                                if (assignedHeatIds == null) continue;

                                foreach (var id in assignedHeatIds)
                                {
                                    var idStr = id?.ToString();
                                    if (!string.IsNullOrEmpty(idStr) && heatIdToNo.TryGetValue(idStr!, out var hNo))
                                    {
                                        if (!heatPlayerMap.ContainsKey(hNo))
                                            heatPlayerMap[hNo] = new List<string>();
                                        heatPlayerMap[hNo].Add(playerNo!);
                                    }
                                }
                            }
                        }

                        result[dncNo] = heatPlayerMap;
                    }
                    return result;
                }
            }
            return result;
        }

        /// <summary>
        /// DS_Status から指定の区分・ラウンドの種目番号リストを昇順で返す。
        /// </summary>
        public static List<int> Get種目番号リスト(JsonNode? dsStatus, string kbnNo, string rndNo)
        {
            var result = new List<int>();
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
                    if (prgDances == null) return result;

                    foreach (var d in prgDances)
                    {
                        var no = d?["DS_DncNo"]?.GetValue<int>() ?? 0;
                        if (no > 0) result.Add(no);
                    }
                    result.Sort();
                    return result;
                }
            }
            return result;
        }

        /// <summary>
        /// 「次のヒート」情報を返す。
        /// 現在の種目・ヒートの次ヒートを同一種目内で探し、なければ次の種目の1Hを返す。
        /// その区分・ラウンドの最後のヒートの場合は null を返す。
        /// 戻り値: (次種目番号, 次ヒート番号, 次種目記号) のタプル、または null。
        /// </summary>
        public static (int DncNo, int HeatNo, string DncCd)? Get次ヒート情報(
            JsonNode? dsStatus, JsonNode? daMaster, string kbnNo, string rndNo, int currentDncNo, int currentHeatNo)
        {
            // 現在種目の全ヒート数
            int currentHeatCount = Getヒート数(dsStatus, kbnNo, rndNo, currentDncNo);

            if (currentHeatNo < currentHeatCount)
            {
                // 同一種目内の次ヒート
                string cd = Get種目記号(daMaster, kbnNo, rndNo, currentDncNo);
                return (currentDncNo, currentHeatNo + 1, cd);
            }

            // 次の種目を探す
            var dncList = Get種目番号リスト(dsStatus, kbnNo, rndNo);
            int idx = dncList.IndexOf(currentDncNo);
            if (idx < 0 || idx + 1 >= dncList.Count)
            {
                // 区分・ラウンドの最後のヒート → null
                return null;
            }

            int nextDncNo = dncList[idx + 1];
            string nextCd = Get種目記号(daMaster, kbnNo, rndNo, nextDncNo);
            return (nextDncNo, 1, nextCd);
        }

        // ────────────────────────────────────────────────
        // DV_Result ヘルパー
        // ────────────────────────────────────────────────

        /// <summary>
        /// DV_Result の採点方式ID を取得する。
        /// </summary>
        public static string Get採点方式ID_DV(JsonNode? dvResult)
            => dvResult?["採点方式ID"]?.ToString() ?? string.Empty;

        /// <summary>
        /// DV_Result が AJS 採点（採点方式ID が "AJS" で始まる）かどうかを返す。
        /// </summary>
        public static bool IsAJS採点(JsonNode? dvResult)
        {
            var id = Get採点方式ID_DV(dvResult);
            return id.StartsWith("AJS", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// DV_Result の 総合結果[] から選手結果リストを総合順位番号昇順で返す。
        /// 戻り値: (総合順位番号, 背番号, 総合得点, 総合順位表記) のリスト。
        /// </summary>
        public static List<(int 順位番号, string 背番号, decimal 得点, string 順位表記)> Get総合結果リスト(
            JsonNode? dvResult)
        {
            var result = new List<(int 順位番号, string 背番号, decimal 得点, string 順位表記)>();
            if (dvResult == null) return result;

            var 総合結果 = dvResult["総合結果"]?.AsArray();
            if (総合結果 == null) return result;

            foreach (var item in 総合結果)
            {
                int rankNo = item?["総合順位番号"]?.GetValue<int>() ?? 0;
                string bango = item?["背番号"]?.ToString() ?? string.Empty;
                decimal score = 0m;
                if (item?["総合得点"] != null)
                    decimal.TryParse(item["総合得点"]!.ToString(), out score);
                string rankStr = item?["総合順位表記"]?.ToString() ?? string.Empty;
                result.Add((rankNo, bango, score, rankStr));
            }

            result.Sort((a, b) => a.順位番号.CompareTo(b.順位番号));
            return result;
        }

        /// <summary>
        /// 順位番号から表示用順位テキストを生成する。
        /// 1→「優勝」、2→「準優勝」、3以降→「N位」
        /// ただし DV_Result の 総合順位表記 が取得できる場合はそちらを優先する。
        /// </summary>
        public static string Format順位テキスト(int rankNo, string rankStr = "")
        {
            // DV_Result の順位表記が設定されていればそれを使う
            if (!string.IsNullOrEmpty(rankStr)) return rankStr;

            return rankNo switch
            {
                1 => "優勝",
                2 => "準優勝",
                _ => $"{rankNo}位"
            };
        }

        // ────────────────────────────────────────────────
        // ジャッジ情報ヘルパー
        // ────────────────────────────────────────────────

        /// <summary>
        /// DA_Master の DJ_JUDGEs からジャッジリストを取得する。
        /// 戻り値: (ジャッジ記号, ジャッジ表記名, ジャッジ所属, ジャッジグループIDリスト) のリスト。
        /// </summary>
        public static List<(string JdgCd, string JdgDispName, string JdgCtry, List<string> JdgGrps)>
            Getジャッジリスト(JsonNode? daMaster)
        {
            var result = new List<(string, string, string, List<string>)>();
            if (daMaster == null) return result;

            var judges = daMaster["DJ_JUDGEs"]?.AsArray();
            if (judges == null) return result;

            foreach (var j in judges)
            {
                if (j == null) continue;
                var jdgCd       = j["DJ_JdgCd"]?.ToString()       ?? string.Empty;
                var jdgDispName = j["DJ_JdgDispName"]?.ToString()  ?? string.Empty;
                var jdgCtry     = j["DJ_JdgCtry"]?.ToString()      ?? string.Empty;

                var grpList = new List<string>();
                var grpArr = j["DJ_JdgGrps"]?.AsArray();
                if (grpArr != null)
                {
                    foreach (var g in grpArr)
                    {
                        var grp = g?["DJ_JdgGrp"]?.ToString();
                        if (!string.IsNullOrEmpty(grp))
                            grpList.Add(grp!);
                    }
                }

                result.Add((jdgCd, jdgDispName, jdgCtry, grpList));
            }

            return result;
        }

        /// <summary>
        /// DA_Master の DJ_JUDGEs から使用されているジャッジグループIDの一覧を重複なしで返す。
        /// 返却順は出現順（最初に登場したグループ順）。
        /// </summary>
        public static List<string> Getジャッジグループリスト(JsonNode? daMaster)
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            var result = new List<string>();

            var judges = daMaster?["DJ_JUDGEs"]?.AsArray();
            if (judges == null) return result;

            foreach (var j in judges)
            {
                var grpArr = j?["DJ_JdgGrps"]?.AsArray();
                if (grpArr == null) continue;
                foreach (var g in grpArr)
                {
                    var grp = g?["DJ_JdgGrp"]?.ToString();
                    if (!string.IsNullOrEmpty(grp) && seen.Add(grp!))
                        result.Add(grp!);
                }
            }

            return result;
        }

        /// <summary>
        /// ジャッジリストを指定グループでフィルタする。
        /// grpId が null または空の場合は全件を返す。
        /// </summary>
        public static List<(string JdgCd, string JdgDispName, string JdgCtry, List<string> JdgGrps)>
            Getジャッジリスト_ByGroup(JsonNode? daMaster, string? grpId)
        {
            var all = Getジャッジリスト(daMaster);
            if (string.IsNullOrEmpty(grpId)) return all;
            return all.Where(j => j.JdgGrps.Contains(grpId!)).ToList();
        }
    }
}
