# DSDsp WebSocket通信 電文仕様書

## 概要

DSDspアプリケーションとDSServer_Main間のWebSocket通信で使用する電文の仕様を定義します。

## 電文フォーマット

### 基本形式

```
OrgCd,CmpNo,From,Command,MsgDetail
```

- **OrgCd**: 団体コード（例: "JS"）
- **CmpNo**: 競技会番号（例: "123456"）
- **From**: 送信元（"DSP"=DSDsp、"SVR"=サーバー）
- **Command**: コマンド名
- **MsgDetail**: JSON形式のメッセージ詳細

### 例

```
JS,123456,DSP,DP_ASK_DA,{"orgCd":"JS"}
```

---

## 1. 接続・初期化フロー

### 1.1 DA_Master取得（競技会マスタ）

#### DP_ASK_DA（クライアント → サーバー）

**目的**: 指定した団体コードの実施中競技会のDA_Masterを要求

**電文例**:

```
JS,,DSP,DP_ASK_DA,{"orgCd":"JS"}
```

**MsgDetail（JSON）**:

```json
{
  "orgCd": "JS"
}
```

#### DP_ANS_DA（サーバー → クライアント）

**ケース1: 実施中競技会が1つの場合**

DA_Masterの全体を返す

**電文例**:

```
JS,123456,SVR,DP_ANS_DA,{DA_Masterの内容}
```

**MsgDetail（JSON）**: DA_Master全体（TestData/123456/DA_Master.json参照）

**ケース2: 実施中競技会が複数の場合**

競技会リストを返す

**電文例**:

```
JS,,SVR,DP_ANS_CMP_LIST,{"competitions":[...]}
```

**MsgDetail（JSON）**:

```json
{
  "competitions": [
    {
      "cmpNo": "123456",
      "cmpName": "第1回全日本選手権",
      "cmpDate": "2026-06-20"
    },
    {
      "cmpNo": "123457",
      "cmpName": "第2回全日本選手権",
      "cmpDate": "2026-06-21"
    }
  ]
}
```

#### DP_SEL_CMP（クライアント → サーバー）

**目的**: 複数競技会から1つを選択

**電文例**:

```
JS,123456,DSP,DP_SEL_CMP,{"cmpNo":"123456"}
```

**MsgDetail（JSON）**:

```json
{
  "cmpNo": "123456"
}
```

**応答**: サーバーは選択された競技会のDA_MasterをDP_ANS_DAで返す

---

### 1.2 DS_Status取得（競技会進行状況）

#### DP_ASK_DS（クライアント → サーバー）

**目的**: 競技会の進行状況（DS_Status）を要求

**電文例**:

```
JS,123456,DSP,DP_ASK_DS,{}
```

**MsgDetail（JSON）**:

```json
{}
```

#### DP_ANS_DS（サーバー → クライアント）

**目的**: DS_Statusの全体を返す（初回取得時）

**電文例**:

```
JS,123456,SVR,DP_ANS_DS,{DS_Statusの内容}
```

**MsgDetail（JSON）**: DS_Status全体（TestData/123456/DS_Status.json参照）

---

### 1.3 DV_Result取得（採点結果）

#### DP_ASK_DV_RESULT（クライアント → サーバー）

**目的**: 指定した区分・ラウンドの採点結果を要求

**電文例**:

```
JS,123456,DSP,DP_ASK_DV_RESULT,{"kbnNo":"01","rndNo":"010"}
```

**MsgDetail（JSON）**:

```json
{
  "orgCd": "JS",
  "cmpNo": "123456",
  "kbnNo": "01",
  "rndNo": "010"
}
```

**パラメータ**:

- **orgCd**: 団体コード
- **cmpNo**: 競技会番号
- **kbnNo**: 区分番号（例: "01"）
- **rndNo**: ラウンド番号（例: "010"）

#### DP_ANS_DV_RESULT（サーバー → クライアント）

**目的**: 指定された区分・ラウンドのDV_Result全体を返す

**電文例**:

```
JS,123456,SVR,DP_ANS_DV_RESULT,{DV_Resultの内容}
```

**MsgDetail（JSON）**: DV_Result全体

---

## 2. リアルタイム更新

### 2.1 DS_Status差分更新

#### DP_UPD_DS（サーバー → クライアント）

**目的**: DS_Statusの変更部分のみを通知（自動プッシュ）

**電文例**:

```
JS,123456,SVR,DP_UPD_DS,{"updates":[...]}
```

**MsgDetail（JSON）**:

```json
{
  "version": 123,
  "updates": [
    {
      "path": "DS_Floors[0].DS_CurPrgNo",
      "value": "5"
    },
    {
      "path": "DS_Floors[0].DS_PRGRSs[4].DS_PrgSts",
      "value": "1"
    },
    {
      "path": "DS_Floors[0].DS_PRGRSs[4].DS_CurDanNo",
      "value": "3"
    },
    {
      "path": "DS_Floors[0].DS_PRGRSs[4].DS_CurHeat",
      "value": "2"
    }
  ]
}
```

**パラメータ**:

- **version**: DS_Statusのバージョン番号
- **updates**: 変更内容の配列
  - **path**: JSONパス形式での変更箇所
  - **value**: 新しい値

**クライアント側の処理**:

1. 受信したupdatesを順次適用
2. メモリ上のDS_Statusを最新状態に保つ
3. バージョン番号を記録

---

### 2.2 DA_Master更新通知

#### DP_UPD_DA（サーバー → クライアント）

**目的**: DA_Masterが更新された際に全体を再送（頻度は低い）

**電文例**:

```
JS,123456,SVR,DP_UPD_DA,{DA_Masterの内容}
```

**MsgDetail（JSON）**: DA_Master全体

---

### 2.3 DV_Result更新通知

#### DP_UPD_DV_RESULT（サーバー → クライアント）

**目的**: 採点結果が更新された際に全体を再送

**電文例**:

```
JS,123456,SVR,DP_UPD_DV_RESULT,{DV_Resultの内容}
```

**MsgDetail（JSON）**: DV_Result全体（該当区分・ラウンド）

---

## 3. 接続シーケンス

### 3.1 正常フロー（実施中競技会が1つ）

```
クライアント                    サーバー
    |                              |
    |------ 接続確立 ------------->|
    |                              |
    |--- DP_ASK_DA --------------->|
    |    {"orgCd":"JS"}            |
    |                              |
    |<-- DP_ANS_DA ----------------|
    |    {DA_Master全体}           |
    |                              |
    |--- DP_ASK_DS --------------->|
    |    {}                        |
    |                              |
    |<-- DP_ANS_DS ----------------|
    |    {DS_Status全体}           |
    |                              |
    |--- DP_ASK_DV_RESULT -------->|
    |    {"kbnNo":"01","rndNo":"010"}
    |                              |
    |<-- DP_ANS_DV_RESULT ---------|
    |    {DV_Result全体}           |
    |                              |
    |<-- DP_UPD_DS ----------------|（自動プッシュ）
    |    {差分更新}                |
    |                              |
```

### 3.2 複数競技会がある場合

```
クライアント                    サーバー
    |                              |
    |--- DP_ASK_DA --------------->|
    |    {"orgCd":"JS"}            |
    |                              |
    |<-- DP_ANS_CMP_LIST ----------|
    |    {競技会リスト}            |
    |                              |
    |--- DP_SEL_CMP -------------->|
    |    {"cmpNo":"123456"}        |
    |                              |
    |<-- DP_ANS_DA ----------------|
    |    {DA_Master全体}           |
    |                              |
    |（以降は通常フロー）          |
```

---

## 4. エラー処理

### 4.1 エラー応答

エラー時は、コマンド名に`_NG`を付けて返す

**例**: DP_ASK_DA → DP_ANS_DA_NG

**電文例**:

```
JS,123456,SVR,DP_ANS_DA_NG,{"error":"競技会が見つかりません"}
```

**MsgDetail（JSON）**:

```json
{
  "error": "エラーメッセージ"
}
```

---

## 5. データ構造

### 5.1 DS_Status構造（抜粋）

```json
{
  "filename": "DS_Status.json",
  "DS_OrgCD": "JS",
  "DS_CompNo": "123456",
  "DS_Version": 123,
  "DS_Floors": [
    {
      "DS_FlrCd": "A",
      "DS_CurPrgNo": "5",
      "DS_CurPrgSubNo": "0",
      "DS_PRGRSs": [
        {
          "DS_PrgNo": "5",
          "DS_PrgSubNo": "0",
          "DS_KbnNo": "01",
          "DS_RndNo": "010",
          "DS_DGrpNo": "1",
          "DS_PrgSts": "1",
          "DS_CurDanNo": "3",
          "DS_CurHeat": "2",
          "DS_PRGDANCEs": [...]
        }
      ]
    }
  ]
}
```

### 5.2 差分更新のJSONパス形式

| パス例                                  | 説明                  |
| --------------------------------------- | --------------------- |
| `DS_Floors[0].DS_CurPrgNo`              | フロアAの現在進行番号 |
| `DS_Floors[0].DS_PRGRSs[4].DS_PrgSts`   | 進行5の状態           |
| `DS_Floors[0].DS_PRGRSs[4].DS_CurDanNo` | 進行5の現在種目番号   |
| `DS_Floors[0].DS_PRGRSs[4].DS_CurHeat`  | 進行5の現在ヒート     |

---

## 6. 設定ファイル（DSDsp.json）

```json
{
  "WebSocketSettings": {
    "ServerIpAddress": "127.0.0.1",
    "ServerPort": 8080,
    "ClientId": "DSDsp_001",
    "OrgCd": "JS"
  },
  "LogSettings": {
    "LogLevel": 3,
    "LogPath": "./Logs"
  }
}
```

**新規追加項目**:

- **OrgCd**: 団体コード（DP_ASK_DAで使用）

---

## 7. 実装上の注意点

### 7.1 DS_Status管理

- **初回**: DP_ANS_DSで全体を受信し、メモリに保持
- **更新**: DP_UPD_DSで差分を受信し、メモリ上のDS_Statusに適用
- **バージョン管理**: DS_Versionで整合性を確認

### 7.2 DA_Master管理

- 競技会中の更新頻度は低い
- 更新時はDP_UPD_DAで全体を再受信

### 7.3 DV_Result管理

- 区分・ラウンド単位で管理
- 更新時はDP_UPD_DV_RESULTで全体を再受信

### 7.4 複数起動対応

- ClientIdで各インスタンスを識別
- 同じOrgCd、CmpNoでも異なるClientIdを使用

---

## 8. 電文一覧

| 電文名           | 方向 | 目的                  |
| ---------------- | ---- | --------------------- |
| DP_ASK_DA        | C→S  | DA_Master要求         |
| DP_ANS_DA        | S→C  | DA_Master応答         |
| DP_ANS_CMP_LIST  | S→C  | 競技会リスト応答      |
| DP_SEL_CMP       | C→S  | 競技会選択            |
| DP_ASK_DS        | C→S  | DS_Status要求         |
| DP_ANS_DS        | S→C  | DS_Status応答（全体） |
| DP_UPD_DS        | S→C  | DS_Status更新（差分） |
| DP_ASK_DV_RESULT | C→S  | DV_Result要求         |
| DP_ANS_DV_RESULT | S→C  | DV_Result応答         |
| DP_UPD_DA        | S→C  | DA_Master更新通知     |
| DP_UPD_DV_RESULT | S→C  | DV_Result更新通知     |
| DP*ANS*\*\_NG    | S→C  | エラー応答            |

C=クライアント（DSDsp）、S=サーバー（DSServer_Main）

---

## 9. 今後の拡張

- 表示制御コマンド（画面切り替え指示など）
- ハートビート（接続確認）
- 再接続時の差分同期

---

## 変更履歴

| 日付       | バージョン | 変更内容 |
| ---------- | ---------- | -------- |
| 2026-06-20 | 1.0        | 初版作成 |
