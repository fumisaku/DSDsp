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
    /// エラー応答
    /// </summary>
    public class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}

// Made with Bob
