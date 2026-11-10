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
    /// サーバーは {"DS_Version":N, "updatedProgress":{...}} 形式で送信する。
    /// </summary>
    public class DP_UPD_DS
    {
        [System.Text.Json.Serialization.JsonPropertyName("DS_Version")]
        public int Version { get; set; }

        /// <summary>
        /// 更新された進行1件。サーバーから送られてくる現在の形式。
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("updatedProgress")]
        public System.Text.Json.Nodes.JsonNode? UpdatedProgress { get; set; }

        /// <summary>
        /// 旧形式（path/value 配列）。後方互換用。
        /// </summary>
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
    /// 表彰式／オナーダンスタブの再生同期通知（DSDsp → サーバー → 他DSDspブロードキャスト）
    /// </summary>
    public class DP_TAB_ADVANCE
    {
        /// <summary>対象タブ："AWARD"（表彰式） / "HONOR"（オナーダンス）</summary>
        public string Tab { get; set; } = string.Empty;

        /// <summary>区分リスト（LstAwardCategories / LstHonorCategories）の選択インデックス</summary>
        public int CategoryIndex { get; set; }

        /// <summary>入賞者リスト（LstHonorPlayers）の選択インデックス。該当なしは -1</summary>
        public int PlayerIndex { get; set; } = -1;

        /// <summary>
        /// 表彰式(AWARD)の区分画面で、送信元が再生後に最後に表示した順位。
        /// 受信側は再生ステップ数ではなくこの順位に合わせて表示する（表示モードごとにステップ数が異なるため）。
        ///   &gt;=1 : その順位まで表示する
        ///    0    : まだ順位を表示していない（通常の1ステップ実行）
        ///   -1    : 送信元の画面が完了した（受信側も閉じる）
        /// </summary>
        public int Rank { get; set; } = 0;
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
