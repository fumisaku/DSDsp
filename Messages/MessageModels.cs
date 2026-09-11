using System;
using System.Collections.Generic;

namespace DSDsp.Messages
{
    /// <summary>
    /// 競技会情報
    /// </summary>
    public class CompetitionInfo
    {
        public string CmpNo { get; set; } = string.Empty;
        public string CmpName { get; set; } = string.Empty;
        public string CmpDate { get; set; } = string.Empty;
    }

    /// <summary>
    /// 競技会リスト応答
    /// </summary>
    public class DP_ANS_CMP_LIST
    {
        public List<CompetitionInfo> Competitions { get; set; } = new List<CompetitionInfo>();
    }

    /// <summary>
    /// 競技会選択要求
    /// </summary>
    public class DP_SEL_CMP
    {
        public string CmpNo { get; set; } = string.Empty;
    }

    /// <summary>
    /// DA_Master要求
    /// </summary>
    public class DP_ASK_DA
    {
        public string OrgCd { get; set; } = string.Empty;
    }

    /// <summary>
    /// DV_Result要求
    /// </summary>
    public class DP_ASK_DV_RESULT
    {
        public string OrgCd { get; set; } = string.Empty;
        public string CmpNo { get; set; } = string.Empty;
        public string KbnNo { get; set; } = string.Empty;
        public string RndNo { get; set; } = string.Empty;
    }

    /// <summary>
    /// DS_Status差分更新
    /// </summary>
    public class DP_UPD_DS
    {
        public int Version { get; set; }
        public List<DsStatusUpdate> Updates { get; set; } = new List<DsStatusUpdate>();
    }

    /// <summary>
    /// DS_Status更新項目
    /// </summary>
    public class DsStatusUpdate
    {
        /// <summary>
        /// JSONパス（例: "DS_Floors[0].DS_CurPrgNo"）
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 新しい値
        /// </summary>
        public object? Value { get; set; }
    }

    /// <summary>
    /// AJSステップ進行通知（DSDsp → サーバー → 全DSDspブロードキャスト）
    /// </summary>
    public class DP_AJS_ADVANCE
    {
        /// <summary>現在のAJSインデックス（進行一覧上の位置）</summary>
        public int AjsIndex { get; set; }

        /// <summary>現在表示中の画面ID（例: "DSP_SOL_001_B"）</summary>
        public string ScreenId { get; set; } = string.Empty;

        /// <summary>
        /// ScreenGroup：大/小ペアの共通識別子。
        /// ScreenIdの末尾 "_B" または "_S" を除いた前半部分（例: "DSP_SOL_001"）。
        /// サフィックスなし画面の場合はScreenIdそのまま。
        /// </summary>
        public string ScreenGroup { get; set; } = string.Empty;

        /// <summary>区分番号</summary>
        public string KbnNo { get; set; } = string.Empty;

        /// <summary>ラウンド番号</summary>
        public string RndNo { get; set; } = string.Empty;

        /// <summary>種目番号（DanceNo）</summary>
        public int DncNo { get; set; }

        /// <summary>ヒート番号（HeatNo）</summary>
        public int HeatNo { get; set; }

        /// <summary>現在のステップ番号（Advance()呼び出し前のステップ）</summary>
        public int Step { get; set; }

        /// <summary>
        /// ScreenGroupを画面IDから計算するユーティリティメソッド。
        /// "_B" または "_S" で終わる場合はそれを除いた前半を返す。
        /// それ以外はscreenIdそのまま。
        /// </summary>
        public static string ComputeScreenGroup(string screenId)
        {
            if (screenId.EndsWith("_B", StringComparison.Ordinal) ||
                screenId.EndsWith("_S", StringComparison.Ordinal))
                return screenId[..^2];
            return screenId;
        }
    }

    /// <summary>
    /// エラー応答
    /// </summary>
    public class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}

// Made with Bob
