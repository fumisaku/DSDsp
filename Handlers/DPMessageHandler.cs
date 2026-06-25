using System;
using System.Text.Json;
using System.Threading.Tasks;
using DSDsp.Data;
using DSDsp.Messages;

namespace DSDsp.Handlers
{
    /// <summary>
    /// DSDsp電文ハンドラー
    /// </summary>
    public class DPMessageHandler
    {
        private readonly LOG_C _log;
        private readonly DataManager _dataManager;
        private readonly WebSocketClient _wsClient;

        // イベント
        public event EventHandler<CompetitionListReceivedEventArgs>? CompetitionListReceived;
        public event EventHandler? DA_MasterReceived;
        public event EventHandler? DS_StatusReceived;
        public event EventHandler? DV_ResultReceived;
        public event EventHandler<ErrorReceivedEventArgs>? ErrorReceived;

        public DPMessageHandler(LOG_C log, DataManager dataManager, WebSocketClient wsClient)
        {
            _log = log;
            _dataManager = dataManager;
            _wsClient = wsClient;
        }

        /// <summary>
        /// 受信メッセージを処理
        /// </summary>
        public async Task HandleMessageAsync(string message)
        {
            var parsed = ParsedMessage.Parse(message);
            if (parsed == null)
            {
                _log.LogAdd($"電文パースエラー: {message}", _log.ERR);
                return;
            }

            _log.LogAdd($"電文処理: {parsed.Command}", _log.DEBUG);

            try
            {
                switch (parsed.Command)
                {
                    case "DP_ANS_DA":
                        await Handle_DP_ANS_DA(parsed);
                        break;

                    case "DP_ANS_CMP_LIST":
                        await Handle_DP_ANS_CMP_LIST(parsed);
                        break;

                    case "DP_ANS_DS":
                        await Handle_DP_ANS_DS(parsed);
                        break;

                    case "DP_UPD_DS":
                        await Handle_DP_UPD_DS(parsed);
                        break;

                    case "DP_ANS_DV_RESULT":
                        await Handle_DP_ANS_DV_RESULT(parsed);
                        break;

                    case "DP_UPD_DA":
                        await Handle_DP_UPD_DA(parsed);
                        break;

                    case "DP_UPD_DV_RESULT":
                        await Handle_DP_UPD_DV_RESULT(parsed);
                        break;

                    default:
                        if (parsed.IsError)
                        {
                            await HandleError(parsed);
                        }
                        else
                        {
                            _log.LogAdd($"未対応の電文: {parsed.Command}", _log.WARNING);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.LogAdd($"電文処理エラー [{parsed.Command}]: {ex.Message}", _log.ERR);
            }
        }

        /// <summary>
        /// DA_Master応答処理
        /// </summary>
        private async Task Handle_DP_ANS_DA(ParsedMessage msg)
        {
            if (string.IsNullOrEmpty(msg.MsgDetail))
            {
                _log.LogAdd("DP_ANS_DA: MsgDetailが空です", _log.WARNING);
                return;
            }

            _dataManager.SetDA_Master(msg.MsgDetail);
            DA_MasterReceived?.Invoke(this, EventArgs.Empty);
            
            _log.LogAdd("DA_Master受信完了", _log.INFO);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 競技会リスト応答処理
        /// </summary>
        private async Task Handle_DP_ANS_CMP_LIST(ParsedMessage msg)
        {
            try
            {
                var cmpList = JsonSerializer.Deserialize<DP_ANS_CMP_LIST>(msg.MsgDetail);
                if (cmpList != null)
                {
                    _log.LogAdd($"競技会リスト受信: {cmpList.Competitions.Count}件", _log.INFO);
                    CompetitionListReceived?.Invoke(this, new CompetitionListReceivedEventArgs(cmpList.Competitions));
                }
            }
            catch (Exception ex)
            {
                _log.LogAdd($"競技会リストパースエラー: {ex.Message}", _log.ERR);
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// DS_Status応答処理（全体）
        /// </summary>
        private async Task Handle_DP_ANS_DS(ParsedMessage msg)
        {
            if (string.IsNullOrEmpty(msg.MsgDetail))
            {
                _log.LogAdd("DP_ANS_DS: MsgDetailが空です", _log.WARNING);
                return;
            }

            _dataManager.SetDS_Status(msg.MsgDetail);
            DS_StatusReceived?.Invoke(this, EventArgs.Empty);
            
            _log.LogAdd("DS_Status受信完了", _log.INFO);
            await Task.CompletedTask;
        }

        /// <summary>
        /// DS_Status差分更新処理
        /// </summary>
        private async Task Handle_DP_UPD_DS(ParsedMessage msg)
        {
            try
            {
                var update = JsonSerializer.Deserialize<DP_UPD_DS>(msg.MsgDetail);
                if (update != null)
                {
                    _dataManager.UpdateDS_Status(update.Version, update.Updates);
                    _log.LogAdd($"DS_Status差分更新: Version={update.Version}, 更新数={update.Updates.Count}", _log.INFO);
                }
            }
            catch (Exception ex)
            {
                _log.LogAdd($"DS_Status差分更新エラー: {ex.Message}", _log.ERR);
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// DV_Result応答処理
        /// </summary>
        private async Task Handle_DP_ANS_DV_RESULT(ParsedMessage msg)
        {
            if (string.IsNullOrEmpty(msg.MsgDetail))
            {
                _log.LogAdd("DP_ANS_DV_RESULT: MsgDetailが空です", _log.WARNING);
                return;
            }

            _dataManager.SetDV_Result(msg.MsgDetail);
            DV_ResultReceived?.Invoke(this, EventArgs.Empty);
            
            _log.LogAdd("DV_Result受信完了", _log.INFO);
            await Task.CompletedTask;
        }

        /// <summary>
        /// DA_Master更新通知処理
        /// </summary>
        private async Task Handle_DP_UPD_DA(ParsedMessage msg)
        {
            if (string.IsNullOrEmpty(msg.MsgDetail))
            {
                _log.LogAdd("DP_UPD_DA: MsgDetailが空です", _log.WARNING);
                return;
            }

            _dataManager.SetDA_Master(msg.MsgDetail);
            DA_MasterReceived?.Invoke(this, EventArgs.Empty);
            
            _log.LogAdd("DA_Master更新通知受信", _log.INFO);
            await Task.CompletedTask;
        }

        /// <summary>
        /// DV_Result更新通知処理
        /// </summary>
        private async Task Handle_DP_UPD_DV_RESULT(ParsedMessage msg)
        {
            if (string.IsNullOrEmpty(msg.MsgDetail))
            {
                _log.LogAdd("DP_UPD_DV_RESULT: MsgDetailが空です", _log.WARNING);
                return;
            }

            _dataManager.SetDV_Result(msg.MsgDetail);
            DV_ResultReceived?.Invoke(this, EventArgs.Empty);
            
            _log.LogAdd("DV_Result更新通知受信", _log.INFO);
            await Task.CompletedTask;
        }

        /// <summary>
        /// エラー応答処理
        /// </summary>
        private async Task HandleError(ParsedMessage msg)
        {
            try
            {
                var error = JsonSerializer.Deserialize<ErrorResponse>(msg.MsgDetail);
                var errorMsg = error?.Error ?? msg.MsgDetail;
                
                _log.LogAdd($"サーバーエラー [{msg.Command}]: {errorMsg}", _log.ERR);
                ErrorReceived?.Invoke(this, new ErrorReceivedEventArgs(msg.Command, errorMsg));
            }
            catch
            {
                _log.LogAdd($"サーバーエラー [{msg.Command}]: {msg.MsgDetail}", _log.ERR);
                ErrorReceived?.Invoke(this, new ErrorReceivedEventArgs(msg.Command, msg.MsgDetail));
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// DA_Master要求を送信
        /// </summary>
        public async Task<bool> RequestDA_MasterAsync(string orgCd)
        {
            var request = new DP_ASK_DA { OrgCd = orgCd };
            var json = JsonSerializer.Serialize(request);
            var message = ParsedMessage.Build(orgCd, "", "DP_ASK_DA", json);
            
            return await _wsClient.SendMessageAsync(message);
        }

        /// <summary>
        /// 競技会選択を送信
        /// </summary>
        public async Task<bool> SelectCompetitionAsync(string orgCd, string cmpNo)
        {
            var request = new DP_SEL_CMP { CmpNo = cmpNo };
            var json = JsonSerializer.Serialize(request);
            var message = ParsedMessage.Build(orgCd, cmpNo, "DP_SEL_CMP", json);
            
            return await _wsClient.SendMessageAsync(message);
        }

        /// <summary>
        /// DS_Status要求を送信
        /// </summary>
        public async Task<bool> RequestDS_StatusAsync(string orgCd, string cmpNo)
        {
            var message = ParsedMessage.Build(orgCd, cmpNo, "DP_ASK_DS", "{}");
            return await _wsClient.SendMessageAsync(message);
        }

        /// <summary>
        /// DV_Result要求を送信
        /// </summary>
        public async Task<bool> RequestDV_ResultAsync(string orgCd, string cmpNo, string kbnNo, string rndNo)
        {
            var request = new DP_ASK_DV_RESULT
            {
                OrgCd = orgCd,
                CmpNo = cmpNo,
                KbnNo = kbnNo,
                RndNo = rndNo
            };
            var json = JsonSerializer.Serialize(request);
            var message = ParsedMessage.Build(orgCd, cmpNo, "DP_ASK_DV_RESULT", json);
            
            return await _wsClient.SendMessageAsync(message);
        }
    }

    /// <summary>
    /// 競技会リスト受信イベント引数
    /// </summary>
    public class CompetitionListReceivedEventArgs : EventArgs
    {
        public System.Collections.Generic.List<CompetitionInfo> Competitions { get; }

        public CompetitionListReceivedEventArgs(System.Collections.Generic.List<CompetitionInfo> competitions)
        {
            Competitions = competitions;
        }
    }

    /// <summary>
    /// エラー受信イベント引数
    /// </summary>
    public class ErrorReceivedEventArgs : EventArgs
    {
        public string Command { get; }
        public string ErrorMessage { get; }

        public ErrorReceivedEventArgs(string command, string errorMessage)
        {
            Command = command;
            ErrorMessage = errorMessage;
        }
    }
}

// Made with Bob
