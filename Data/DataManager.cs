using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DSDsp.Data
{
    /// <summary>
    /// DSDspのデータ管理クラス
    /// DA_Master、DS_Status、DV_Resultを管理
    /// </summary>
    public class DataManager
    {
        private readonly LOG_C _log;
        private JsonNode? _daMaster;
        private JsonNode? _dsStatus;
        private JsonNode? _dvResult;
        private int _dsStatusVersion;

        /// <summary>
        /// DA_Master（競技会マスタ）
        /// </summary>
        public JsonNode? DA_Master
        {
            get => _daMaster;
            private set
            {
                _daMaster = value;
                DA_MasterUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// DS_Status（競技会進行状況）
        /// </summary>
        public JsonNode? DS_Status
        {
            get => _dsStatus;
            private set
            {
                _dsStatus = value;
                DS_StatusUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// DV_Result（採点結果）
        /// </summary>
        public JsonNode? DV_Result
        {
            get => _dvResult;
            private set
            {
                _dvResult = value;
                DV_ResultUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// DS_Statusのバージョン番号
        /// </summary>
        public int DS_StatusVersion => _dsStatusVersion;

        /// <summary>
        /// 競技会番号
        /// </summary>
        public string? CmpNo { get; private set; }

        /// <summary>
        /// 団体コード
        /// </summary>
        public string? OrgCd { get; private set; }

        // イベント
        public event EventHandler? DA_MasterUpdated;
        public event EventHandler? DS_StatusUpdated;
        public event EventHandler? DV_ResultUpdated;

        public DataManager(LOG_C log)
        {
            _log = log;
            _dsStatusVersion = 0;
        }

        /// <summary>
        /// DA_Masterを設定
        /// </summary>
        public void SetDA_Master(string jsonString)
        {
            try
            {
                // デバッグ: 受信したJSON文字列の長さと最初の100文字をログ出力
                _log.LogAdd($"DA_Master受信: 長さ={jsonString.Length}, 先頭100文字={jsonString.Substring(0, Math.Min(100, jsonString.Length))}", _log.DEBUG);
                
                var json = JsonNode.Parse(jsonString);
                if (json == null)
                {
                    _log.LogAdd("DA_Master JSONパースエラー: パース結果がnull", _log.ERR);
                    return;
                }

                DA_Master = json;

                // 競技会情報を抽出
                if (json["DA_OrgCD"] != null)
                    OrgCd = json["DA_OrgCD"]?.ToString();
                if (json["DA_CompNo"] != null)
                    CmpNo = json["DA_CompNo"]?.ToString();

                _log.LogAdd($"DA_Master設定完了: OrgCd={OrgCd}, CmpNo={CmpNo}", _log.INFO);
            }
            catch (JsonException ex)
            {
                _log.LogAdd($"DA_Master設定エラー: {ex.Message}", _log.ERR);
                // JSONの問題箇所付近を出力
                if (ex.LineNumber.HasValue && ex.BytePositionInLine.HasValue)
                {
                    int lineNum = (int)ex.LineNumber.Value;
                    int pos = (int)ex.BytePositionInLine.Value;
                    var lines = jsonString.Split('\n');
                    if (lineNum > 0 && lineNum <= lines.Length)
                    {
                        string problemLine = lines[lineNum - 1];
                        int start = Math.Max(0, pos - 50);
                        int length = Math.Min(100, problemLine.Length - start);
                        string context = problemLine.Substring(start, length);
                        _log.LogAdd($"問題箇所(行{lineNum}, 位置{pos}): ...{context}...", _log.ERR);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogAdd($"DA_Master設定エラー(予期しないエラー): {ex.Message}", _log.ERR);
            }
        }

        /// <summary>
        /// DS_Statusを設定（全体）
        /// </summary>
        public void SetDS_Status(string jsonString)
        {
            try
            {
                var json = JsonNode.Parse(jsonString);
                if (json == null)
                {
                    _log.LogAdd("DS_Status JSONパースエラー", _log.ERR);
                    return;
                }

                DS_Status = json;

                // バージョン番号を取得
                if (json["DS_Version"] != null)
                {
                    _dsStatusVersion = json["DS_Version"]?.GetValue<int>() ?? 0;
                }

                _log.LogAdd($"DS_Status設定完了: Version={_dsStatusVersion}", _log.INFO);
            }
            catch (Exception ex)
            {
                _log.LogAdd($"DS_Status設定エラー: {ex.Message}", _log.ERR);
            }
        }

        /// <summary>
        /// DS_Statusを差分更新
        /// </summary>
        /// <param name="version">新しいバージョン番号</param>
        /// <param name="updates">更新内容のリスト</param>
        public void UpdateDS_Status(int version, System.Collections.Generic.List<Messages.DsStatusUpdate> updates)
        {
            if (_dsStatus == null)
            {
                _log.LogAdd("DS_Statusが未設定のため差分更新できません", _log.WARNING);
                return;
            }

            try
            {
                foreach (var update in updates)
                {
                    ApplyUpdate(_dsStatus, update.Path, update.Value);
                    _log.LogAdd($"DS_Status更新: {update.Path} = {update.Value}", _log.DEBUG);
                }

                _dsStatusVersion = version;
                
                // バージョン番号も更新
                if (_dsStatus["DS_Version"] != null)
                {
                    _dsStatus["DS_Version"] = version;
                }

                DS_StatusUpdated?.Invoke(this, EventArgs.Empty);
                _log.LogAdd($"DS_Status差分更新完了: Version={version}, 更新数={updates.Count}", _log.INFO);
            }
            catch (Exception ex)
            {
                _log.LogAdd($"DS_Status差分更新エラー: {ex.Message}", _log.ERR);
            }
        }

        /// <summary>
        /// JSONパスに従って値を更新
        /// </summary>
        private void ApplyUpdate(JsonNode node, string path, object? value)
        {
            // パスを分解（例: "DS_Floors[0].DS_CurPrgNo"）
            var parts = path.Split('.');
            JsonNode? current = node;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];
                current = NavigateToNode(current, part);
                if (current == null)
                {
                    _log.LogAdd($"パスが見つかりません: {path} (at {part})", _log.WARNING);
                    return;
                }
            }

            // 最後の要素に値を設定
            var lastPart = parts[^1];
            SetNodeValue(current, lastPart, value);
        }

        /// <summary>
        /// ノードをナビゲート
        /// </summary>
        private JsonNode? NavigateToNode(JsonNode? node, string part)
        {
            if (node == null) return null;

            // 配列アクセス（例: "DS_Floors[0]"）
            if (part.Contains('['))
            {
                var propName = part.Substring(0, part.IndexOf('['));
                var indexStr = part.Substring(part.IndexOf('[') + 1, part.IndexOf(']') - part.IndexOf('[') - 1);
                
                if (int.TryParse(indexStr, out int index))
                {
                    var array = node[propName]?.AsArray();
                    if (array != null && index < array.Count)
                    {
                        return array[index];
                    }
                }
            }
            else
            {
                // 通常のプロパティアクセス
                return node[part];
            }

            return null;
        }

        /// <summary>
        /// ノードに値を設定
        /// </summary>
        private void SetNodeValue(JsonNode? node, string property, object? value)
        {
            if (node == null) return;

            // 配列アクセス
            if (property.Contains('['))
            {
                var propName = property.Substring(0, property.IndexOf('['));
                var indexStr = property.Substring(property.IndexOf('[') + 1, property.IndexOf(']') - property.IndexOf('[') - 1);
                
                if (int.TryParse(indexStr, out int index))
                {
                    var array = node[propName]?.AsArray();
                    if (array != null && index < array.Count)
                    {
                        array[index] = JsonValue.Create(value);
                    }
                }
            }
            else
            {
                // 通常のプロパティ
                node[property] = JsonValue.Create(value);
            }
        }

        /// <summary>
        /// DV_Resultを設定
        /// </summary>
        public void SetDV_Result(string jsonString)
        {
            try
            {
                var json = JsonNode.Parse(jsonString);
                if (json == null)
                {
                    _log.LogAdd("DV_Result JSONパースエラー", _log.ERR);
                    return;
                }

                DV_Result = json;
                _log.LogAdd("DV_Result設定完了", _log.INFO);
            }
            catch (Exception ex)
            {
                _log.LogAdd($"DV_Result設定エラー: {ex.Message}", _log.ERR);
            }
        }

        /// <summary>
        /// すべてのデータをクリア
        /// </summary>
        public void Clear()
        {
            _daMaster = null;
            _dsStatus = null;
            _dvResult = null;
            _dsStatusVersion = 0;
            CmpNo = null;
            OrgCd = null;
            _log.LogAdd("データマネージャーをクリアしました", _log.INFO);
        }
    }
}

// Made with Bob
