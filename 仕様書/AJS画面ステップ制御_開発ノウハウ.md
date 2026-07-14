# DSDsp AJS画面ステップ制御 開発ノウハウ

作成日: 2025-07-14  
対象リポジトリ: `DScore/DSDsp`  
対象コミット: `d6e245b`

---

## 1. アーキテクチャ概要

### 1.1 ステップ制御の全体像

```
[再生ボタン]
    │
    ▼
MainWindow.BtnPlay_Click()
    │
    ├─ ExecuteCurrentStep()
    │       │
    │       └─ ExecuteAjsStep()
    │               │
    │               ├─ _currentStep==0 のとき: ShowScreen() → 新インスタンス表示
    │               │
    │               ├─ currentScreen.ExecuteStep(_currentStep)
    │               │
    │               └─ 最終ステップ判定
    │                       │
    │                       ├─ WaitsForLastStepFadeOut==true
    │                       │       → LastStepFadeOutCompleted イベント待ち
    │                       │           → (フェードアウト完了後) MoveToNextAjsScreen()
    │                       │
    │                       └─ WaitsForLastStepFadeOut==false
    │                               → MoveToNextAjsScreen() 即時実行
    │
    └─ BtnPlay_Click: stepped==true なら _currentStep++
```

### 1.2 _currentStep の管理主体

| 変数 | 所在 | 役割 |
|---|---|---|
| `MainWindow._currentStep` | `MainWindow.xaml.cs` | 再生ボタン操作のステップカウンター |
| `DSDspScreenBase._currentStep` | `DSDspScreenBase.cs` | 画面側に渡すステップ番号（`ExecuteStep(int)` で上書き） |

**重要**: `currentScreen.ExecuteStep(step)` を呼ぶと画面側の `_currentStep` が書き換わる。  
画面側の `_currentStep` はあくまで参照用で、MainWindow 側の `_currentStep` がマスター。

---

## 2. DSDspScreenBase の仕組み

### 2.1 派生クラスで実装必須のメンバー

```csharp
// ステップ総数を返す（画面ごとに定義）
protected abstract int TotalSteps { get; }

// 現在の _currentStep に応じた処理を実行
protected abstract void ExecuteCurrentStep();
```

### 2.2 最終ステップフェードアウト待機の仕組み

```csharp
// 基底クラス（DSDspScreenBase）

// フェードアウト完了まで次画面遷移を待機するか（デフォルト false）
public virtual bool WaitsForLastStepFadeOut => false;

// フェードアウト完了通知イベント
public event EventHandler? LastStepFadeOutCompleted;

// 派生クラスから呼び出す通知メソッド
protected void RaiseLastStepFadeOutCompleted()
    => LastStepFadeOutCompleted?.Invoke(this, EventArgs.Empty);
```

#### 派生クラスでの使い方（最終 Step のフェードアウト完了を通知する）

```csharp
// 例: DSP_TIT_002
public override bool WaitsForLastStepFadeOut => true;

public void Step3()  // 最終フェードアウトStep
{
    var sb = new Storyboard();
    _partsMain.フェードアウト(true, PartsTIT002.IM_種目1, sb, 0);
    // ...
    sb.Completed += (s, e) => RaiseLastStepFadeOutCompleted(); // ← ここで通知
    sb.Begin();
}
```

---

## 3. 画面別ステップ構成

### 3.1 TIT_001（区分ラウンド紹介）

```
TotalSteps = 4
case 0: Step1()  - ヘッダ設定
case 1: Step2()  - タイトルフェードイン
case 2: Step3()  - サブタイトルフェードイン
case 3: Step4()  - 全体フェードアウト
```
- `WaitsForLastStepFadeOut` = **false**（変更対象外・即時遷移）

### 3.2 TIT_002 / TIT_003（種目紹介）

```
TotalSteps = 3
case 0: Step1()  - ヘッダ設定（競技会名・区分ラウンド名）
case 1: Step2()  - 種目情報アニメーション表示
case 2: Step3()  - フェードアウト → RaiseLastStepFadeOutCompleted()
```
- `WaitsForLastStepFadeOut` = **true**
- **Step1→Step2 は再生ボタン操作**（TIT_001 と同じ操作性）

### 3.3 SOL_001 / SOL_002（ソロ選手紹介）

```
TotalSteps = 2
case 0: Step1() + Step2()  - ヘッダ設定 + 選手情報アニメーション（同時）
case 1: Step3()            - フェードアウト → RaiseLastStepFadeOutCompleted()
```
- `WaitsForLastStepFadeOut` = **true**
- Step1 + Step2 は自動同時実行（再生ボタン1回で両方）

### 3.4 SOL_003〜006 / DUE_003〜004（選手結果）

```
TotalSteps = 3
case 0: Step1() + Step2()  - ヘッダ設定 + 選手情報（同時）
case 1: Step3(DV_Result)   - 採点結果表示
case 2: Step4()            - フェードアウト → RaiseLastStepFadeOutCompleted()
```
- `WaitsForLastStepFadeOut` = **true**

### 3.5 DUE_001 / DUE_002（デュエル選手紹介）

```
TotalSteps = 2
case 0: Step1() + Step2()  - ヘッダ設定 + 2選手フェードイン（同時）
case 1: Step3()            - フェードアウト → RaiseLastStepFadeOutCompleted()
```
- `WaitsForLastStepFadeOut` = **true**

### 3.6 SOL_007 / SOL_008 / GRP_001〜004 / COM_001〜002（途中結果・一覧）

**1ページの場合** (`TotalSteps = 2`)：
```
case 0: Step1() + Step2() + Step3(p=0)  - ヘッダ + 一覧1ページ目（同時）
case 1: Step4(onCompleted: Step5)       - 一覧フェードアウト → Step5自動実行
                                          Step5完了 → RaiseLastStepFadeOutCompleted()
```

**複数ページの場合** (`TotalSteps = _ページ数 * 2 + 1`)：
```
case 0        : Step1 + Step2 + Step3(p=0)
case 1        : Step4(p=0)
case 2        : Step3(p=1)
case 3        : Step4(p=1)
...
case n*2-1    : Step4(p=n-1, onCompleted: Step5)  ← 最終ページ
case n*2      : Step5  ← この case には到達しない（コールバック経由）
```

- `TotalSteps = _ページ数 == 1 ? 2 : _ページ数 * 2 + 1`
- `WaitsForLastStepFadeOut` = **true**

---

## 4. バグと修正パターン

### 4.1【バグ】EnsurePartsMainInitialized() が毎回 非表示() を呼ぶ

**症状**: Step2 が実行されているにも関わらず、選手情報・画像が画面に表示されない。

**原因**:
```csharp
// 問題のあったコード（SOL_001, SOL_002, DUE_001, DUE_002）
protected override void EnsurePartsMainInitialized()
{
    base.EnsurePartsMainInitialized();
    if (_partsMain != null)   // ← 毎回 true になる
    {
        非表示();  // Step1 でも Step2 でも呼ばれる → Step2 の冒頭で消える
    }
}
```

`case 0: Step1(); Step2();` の場合、Step1 が `EnsurePartsMainInitialized()` → `非表示()` を呼び、
続く Step2 も `EnsurePartsMainInitialized()` → `非表示()` を呼ぶため、Step1 で設定した内容が消える。

**修正**: `base` を呼ぶ前に null チェックし、初回のみ実行する。

```csharp
// 修正後
protected override void EnsurePartsMainInitialized()
{
    bool wasNull = (_partsMain == null);  // base 呼び出し前にチェック
    base.EnsurePartsMainInitialized();
    if (wasNull && _partsMain != null)    // 新規作成された場合のみ
    {
        非表示();
    }
}
```

### 4.2【バグ】Step5 が2回実行される（1ページ表示の画面）

**症状**: SOL_007 等で Step5（タイトルフェードアウト）が連続2回実行され、次の画面に遷移する。

**原因**:
```
TotalSteps = _ページ数 * 2 + 1 = 3（1ページの場合）

_currentStep=1: Step4(onCompleted: Step5) → Step4完了でStep5①実行
_currentStep=2（最終ステップ）: ExecuteCurrentStep() の最後の else → Step5②実行
```

`_ページ数==1` かつ `_currentStep=2` に到達すると、
`p = (2-2)/2+1 = 1` で `p < _ページ数=1` が false → `else: Step5()` が実行される。

**修正**: 1ページの場合はそもそも `_currentStep=2` が最終ステップにならないよう `TotalSteps` を2にする。

```csharp
// 修正後
protected override int TotalSteps => _ページ数 == 1 ? 2 : _ページ数 * 2 + 1;
```

### 4.3【バグ】フェードアウトと次の画面表示が重なる

**症状**: 最終 Step（フェードアウト）が視覚的に完了する前に、次の画面が表示されてしまう。

**原因**: WPF の `Storyboard` は非同期で動作するため、`Begin()` を呼んだ直後に次のコードが実行される。
`ExecuteAjsStep()` は `currentScreen.ExecuteStep()` → `return true` → MainWindow 側で `_currentStep++` → 最終判定 → 次の画面 Step0 即時実行 という流れになっていた。

**修正**: `DSDspScreenBase` に `WaitsForLastStepFadeOut` プロパティと `LastStepFadeOutCompleted` イベントを追加し、
フェードアウト完了後に `RaiseLastStepFadeOutCompleted()` を呼ぶ設計に変更。

```csharp
// 最終Step の Storyboard に Completed ハンドラを追加
sb.Completed += (s, e) => RaiseLastStepFadeOutCompleted();
sb.Begin();
```

---

## 5. Step4 → Step5 自動実行パターン（多ページ画面）

ページネーションのある画面（SOL_007/008, GRP/COM）では、
最後のページの Step4（一覧フェードアウト）完了後に Step5（タイトルフェードアウト）を自動実行したい。

### 実装パターン

```csharp
// Step4 にコールバック引数を追加
public void Step4(Action? onCompleted = null)
{
    var fadeOutStoryboard = new Storyboard();
    // ... フェードアウト要素を追加 ...

    if (onCompleted != null)
        fadeOutStoryboard.Completed += (s, e) => onCompleted();

    fadeOutStoryboard.Begin();
}

// ExecuteCurrentStep() 内で、最終ページの Step4 にだけ Step5 を渡す
if (ブロック内 == 1)
{
    // 1ページのみの場合はStep4完了後にStep5を自動実行
    Step4(_ページ数 == 1 ? (Action)Step5 : null);
    return;
}
// ...
Step4(p == _ページ数 - 1 ? (Action)Step5 : null);
```

### Step5 の WaitsForLastStepFadeOut との連携

```
Step4(onCompleted: Step5) が実行される
    ↓
Step4 フェードアウト完了 → Step5() 呼び出し
    ↓
Step5 フェードアウト完了 → RaiseLastStepFadeOutCompleted() 呼び出し
    ↓
MainWindow: LastStepFadeOutCompleted イベント受信 → MoveToNextAjsScreen()
```

---

## 6. 新しい画面を追加するときのチェックリスト

1. **`TotalSteps` を正しく設定する**
   - ステップ数（再生ボタンを押す回数）を数える
   - `case 0` で複数 Step を同時実行する場合は、その分を差し引く

2. **`ExecuteCurrentStep()` の switch を実装する**
   - `case 0` から `case TotalSteps-1` まで漏れなく実装する
   - `case` の数と `TotalSteps` の値が一致していること

3. **`EnsurePartsMainInitialized()` をオーバーライドする場合**
   - `非表示()` などの初期化処理は `wasNull` パターンで初回のみ実行する
   ```csharp
   bool wasNull = (_partsMain == null);
   base.EnsurePartsMainInitialized();
   if (wasNull && _partsMain != null) { 非表示(); }
   ```

4. **フェードアウト完了後に次の画面へ遷移したい場合**
   - `public override bool WaitsForLastStepFadeOut => true;` を追加
   - 最終 Step の `fadeOutStoryboard.Completed` で `RaiseLastStepFadeOutCompleted()` を呼ぶ
   - **TIT_001 は対象外**（即時遷移のままとする設計）

5. **ページネーションがある画面の場合**
   - `TotalSteps = _ページ数 == 1 ? 2 : _ページ数 * 2 + 1` とする
   - `Step4` に `Action? onCompleted = null` 引数を持たせる
   - 最終ページの Step4 呼び出し時に `Step5` を渡す

---

## 7. ステップ制御の全体フロー図（1ページ画面の例）

```
[再生①]  MainWindow._currentStep=0
            → ExecuteAjsStep()
            → ShowScreen(新インスタンス) + currentScreen.ExecuteStep(0)
            → 画面: case 0 → Step1() + Step2() 同時実行
            → return true → _currentStep++ = 1

[再生②]  MainWindow._currentStep=1
            → currentScreen.ExecuteStep(1)
            → 画面: case 1 → Step3() フェードアウト開始
            → return true → _currentStep++ = 2

[再生③]  MainWindow._currentStep=2（= TotalSteps-1 = 最終）
            → currentScreen.ExecuteStep(2)
            → 画面: case 2 → 最終フェードアウト
            → _currentStep >= TotalSteps-1 → 最終ステップ判定
            → _currentAjsIndex++ / _currentStep=0
            → WaitsForLastStepFadeOut==true → イベント待ち
            [フェードアウト完了]
            → LastStepFadeOutCompleted 発火
            → MoveToNextAjsScreen()
            → 次の画面 Step0 実行 → _currentStep=1
```

---

## 8. 関連ファイル一覧

| ファイル | 役割 |
|---|---|
| `MainWindow.xaml.cs` | ステップ制御のメインロジック（`ExecuteAjsStep`, `MoveToNextAjsScreen`） |
| `画面/DSDspScreenBase.cs` | 画面基底クラス（`TotalSteps`, `WaitsForLastStepFadeOut`, `RaiseLastStepFadeOutCompleted`） |
| `画面/DSP_TIT_001_*.cs` | 変更対象外（`TotalSteps=4`, 即時遷移） |
| `画面/DSP_TIT_002_*.cs` | `TotalSteps=3`, Step1→Step2→Step3 全て手動操作 |
| `画面/DSP_TIT_003_*.cs` | `TotalSteps=3`, Step1→Step2→Step3 全て手動操作 |
| `画面/DSP_SOL_001〜002_*.cs` | `TotalSteps=2`, case 0: Step1+Step2同時 |
| `画面/DSP_SOL_003〜006_*.cs` | `TotalSteps=3`, case 0: Step1+Step2同時 |
| `画面/DSP_SOL_007〜008_*.cs` | `TotalSteps=_ページ数==1?2:_ページ数*2+1`, ページネーション対応 |
| `画面/DSP_GRP_001〜004_*.cs` | 同上 |
| `画面/DSP_COM_001〜002_*.cs` | 同上 |
| `画面/DSP_DUE_001〜002_*.cs` | `TotalSteps=2`, case 0: Step1+Step2同時 |
| `画面/DSP_DUE_003〜004_*.cs` | `TotalSteps=3`, case 0: Step1+Step2同時 |
