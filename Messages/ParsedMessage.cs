using System;

namespace DSDsp.Messages
{
    /// <summary>
    /// パース済み電文
    /// </summary>
    public class ParsedMessage
    {
        /// <summary>
        /// 団体コード
        /// </summary>
        public string OrgCd { get; set; } = string.Empty;

        /// <summary>
        /// 競技会番号
        /// </summary>
        public string CmpNo { get; set; } = string.Empty;

        /// <summary>
        /// 送信元（DSP/SVR）
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// コマンド名
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// メッセージ詳細（JSON文字列）
        /// </summary>
        public string MsgDetail { get; set; } = string.Empty;

        /// <summary>
        /// 元の電文
        /// </summary>
        public string RawMessage { get; set; } = string.Empty;

        /// <summary>
        /// 電文をパース
        /// </summary>
        /// <param name="message">電文文字列</param>
        /// <returns>パース済み電文、失敗時はnull</returns>
        public static ParsedMessage? Parse(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            try
            {
                // フォーマット: OrgCd,CmpNo,From,Command,MsgDetail
                var parts = message.Split(',', 5);
                
                if (parts.Length < 4)
                    return null;

                return new ParsedMessage
                {
                    OrgCd = parts[0],
                    CmpNo = parts[1],
                    From = parts[2],
                    Command = parts[3],
                    MsgDetail = parts.Length > 4 ? parts[4] : string.Empty,
                    RawMessage = message
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 電文を生成
        /// </summary>
        /// <param name="orgCd">団体コード</param>
        /// <param name="cmpNo">競技会番号</param>
        /// <param name="command">コマンド名</param>
        /// <param name="msgDetail">メッセージ詳細（JSON文字列）</param>
        /// <returns>電文文字列</returns>
        public static string Build(string orgCd, string cmpNo, string command, string msgDetail = "")
        {
            return $"{orgCd},{cmpNo},DSP,{command},{msgDetail}";
        }

        /// <summary>
        /// エラー応答かどうか
        /// </summary>
        public bool IsError => Command.EndsWith("_NG");

        /// <summary>
        /// 文字列表現
        /// </summary>
        public override string ToString()
        {
            return $"[{Command}] OrgCd={OrgCd}, CmpNo={CmpNo}, From={From}";
        }
    }
}

// Made with Bob
