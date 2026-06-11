# refactor-instructions.md

plc-comm-slmp-dotnet のリファクタリング指示書。
この文書は実装担当モデル向けの完結した作業指示である。実装前にこの文書全体を読むこと。

> **最重要の前提**: このライブラリは NuGet に公開済み(`PlcComm.Slmp` 0.1.14)であり、
> SLMP バイナリ 3E/4E の実装は 5 スタック横並びの実機検証記録(`TODO.md`、
> `internal_docs/`)に紐づく。**公開 API と送信フレームのバイト列を変えてはならない。**
>
> このリポジトリは plc-comm 一族の **.NET 基準実装**であり、構造は比較的健全
> (単一 async クライアント、internal ペイロードビルダーは一部テスト済み、
> 共有スペックベクトルあり)。本タスクは Rust 版で実施済みの
> 「client の純粋ロジック分離」の .NET ミラーであり、**規模は小さくてよい**。
> 変更すべきものが見つからなければ、それを正直に報告して終了してよい。

---

## Objective

公開 API・ワイヤバイト列・クロススタック互換を一切壊さずに:

1. **`SlmpClient.cs`(1,950 行)内の純粋なペイロードビルダー/デコーダを
   internal static クラスへ move-only 分離し、直接テストを拡充する**
2. (任意)`SlmpClientExtensions.cs`(1,636 行)内の read-plan 最適化機構の
   internal 分離(Rust 版 helpers.rs の D2 と同型)

---

## Project Understanding

### 何のライブラリか

三菱 MELSEC PLC と SLMP バイナリ 3E/4E で通信する .NET 9 ライブラリ(TCP/UDP)。
高レベル契約(`HIGH_LEVEL_API_CONTRACT.md`)の基準実装。`SlmpClientFactory.
OpenAndConnectAsync` → `QueuedSlmpClient`(直列化ラッパ)が推奨入口。

### モジュール構成(src/PlcComm.Slmp/、計約 7,800 行)

| ファイル | 行数 | 内容 |
|---|---|---|
| `SlmpClient.cs` | 1,950 | トランスポート(TCP/UDP)+ 全コマンド面 + internal ビルダー(`BuildExtendedRandomReadPayload` 689 行〜、`BuildLabel*Payload` 1,191 行〜) |
| `SlmpClientExtensions.cs` | 1,636 | 契約ヘルパ(`ReadTypedAsync` / `ReadNamedAsync` / `PollAsync` / single-request / chunked)+ read-plan 最適化(private) |
| `SlmpEndCodes.cs` | 2,086 | エンドコード表(データ。触らない) |
| `SlmpDeviceRanges.cs` | 855 | デバイスレンジカタログ |
| `QueuedSlmpClient.cs` / Factory / Options / Models / Enums / Targeting / Address / Profiles | 約 1,200 | 健全 |

### テスト / CI

- `tests/`: `SlmpClientGuardTests`(421)/ `SlmpDeviceRangeCatalogTests`(437)/
  `SlmpClientExtensionsTests`(261)/ `SlmpFrameVectorTests`(200、共有ベクトル)/
  `SlmpParserTests` / `SlmpClientPayloadTests`(45 行と薄い)ほか
- `run_ci.bat`: `dotnet build` → `dotnet test --no-build` → `dotnet format --verify-no-changes`
- `Directory.Build.props`: `TreatWarningsAsErrors=true`

---

## Behaviors To Preserve(絶対に壊さない既存挙動)

1. **公開 API**: すべての public 型・メソッド・シグネチャ・既定値
   (`FrameType` 既定 4E、`CompatibilityMode` 既定 Iqr、`TargetAddress` 既定
   0xFF/0x03FF、`MonitoringTimer` 0x0010 等)。
2. **送信フレームのバイト列**: `SlmpFrameVectorTests` + 共有ベクトルが契約。
   既存ベクトルの編集禁止。
3. **ガード挙動**: `SlmpClientGuardTests` が固定している事前検証と例外
   (送信前拒否のタイミングを変えない)。
4. **`QueuedSlmpClient` の直列化セマンティクス**(下流アプリ plc-scope-dotnet が利用)。
5. **セマンティック原子性**(`HIGH_LEVEL_API_CONTRACT.md` 第 4〜5 節)。
6. **NuGet パッケージ ID・バージョン・CHANGELOG**: 本タスクで変更しない。

---

## Non-Negotiables(交渉不可の制約)

- 最初に `git status` を確認する。未コミット変更があれば混ぜず、報告して停止する。
- 編集前に Baseline Commands をすべて実行し、結果(テスト件数含む)を記録する。
- 変更は小さく戻しやすい単位。コミットはユーザーの指示があるまで行わない。
- 無関係な整形・「ついで」リファクタリングをしない。
- NuGet 依存を追加しない。csproj / props を変更しない。
- 分離した型の可視性は `internal` まで(既存の `InternalsVisibleTo` 構成を確認し、
  無ければテストからの参照方法を報告してから進める)。
- 既存テスト・既存ベクトルの既存内容を変更しない(追加のみ可)。
- 実機 PLC への接続を行わない。
- 正しさが不明な場合は実装を止め、「Stop And Ask」として質問を報告書に書く。

---

## Stop And Ask Conditions(即時停止して質問する条件)

- 移動対象が `&this` の状態(FrameType / CompatibilityMode / TargetAddress 等)に依存して
  いて引数化が必要になった(引数化は**シグネチャ設計の判断**を伴うため、案を添えて質問)
- 特性テスト採取中に出力が文書・共有ベクトルと食い違って見えた(**修正せず**報告)
- 既存テストが自分の変更後に落ちた ⇒ 即座に巻き戻して報告
- 公開 API・フレームバイト列に影響しうる変更が必要に見えた
- 本書の Debt Map に無い大きな問題を発見した(報告のみ)

---

## Baseline Commands

作業ディレクトリ: リポジトリルート。.NET 9 SDK。OS は問わない。実機 PLC 不要・接続禁止。

```powershell
git status                                       # クリーンであることを確認
dotnet build PlcComm.Slmp.sln
dotnet test PlcComm.Slmp.sln --no-build          # テスト件数を記録
dotnet format PlcComm.Slmp.sln --verify-no-changes
```

---

## Debt Map

行番号は調査時点(main, commit `9a7023c`)のアンカー。ドリフトしていたら宣言名で探すこと。

### D1. ペイロードビルダーの直接テスト不足 【実装可 / 最優先】

- **根拠**: `SlmpClientPayloadTests.cs` は 45 行のみ。internal ビルダー
  (`BuildExtendedRandomReadPayload` / `BuildExtendedRandomWordWritePayload` /
  `BuildExtendedRandomBitWritePayload` / `BuildExtendedMonitorRegisterPayload` /
  `BuildLabelArrayReadPayload` / `BuildLabelArrayWritePayload` /
  `BuildLabelRandomReadPayload` / `BuildLabelRandomWritePayload`)の入力バリエーション
  (境界点数、複数 CPU ターゲット、abbreviation ラベル有無等)が薄い。
- **改善案**: 現在の出力バイト列を採取して特性テストを追加(`SlmpClientPayloadTests` に
  追記、または新ファイル)。期待値は現在の実装出力に限る。
- **リスク**: 低。

### D2. `SlmpClient.cs` のトランスポートとビルダーの同居 【実装可(小規模)】

- **根拠**: TCP/UDP 接続・フレーム送受信と、純粋なペイロード組立が同一クラス。
  ビルダーのうち static なもの(Label 系 1,191〜1,300 行)はそのまま移動可能。
  instance 状態に触るもの(Extended 系 689〜857 行)は移動前に依存を確認する。
- **改善案**: internal static `SlmpPayloads` クラスへ move-only 分離
  (まず static 物のみ。instance 依存物は Stop And Ask 参照)。
- **リスク**: 低〜中。D1 完了後に着手。

### D3. `SlmpClientExtensions.cs` 内の read-plan 機構 【任意・小】

- **根拠**: `ReadNamedAsync` のバッチ最適化機構(private 型群)が契約ヘルパと同居
  (Rust 版 helpers.rs と同型の負債。Rust 側は分離指示済み)。
- **改善案**: internal クラス/ファイルへ move-only 分離。時間や確信が足りなければ
  実施せず提案として報告するだけでよい。
- **検証**: `SlmpClientExtensionsTests` が無修正で通ること。

### D4. その他(現状維持 / 報告のみ)

- `SlmpEndCodes.cs`(2,086 行)はデータ表。触らない。
- 約 60 メソッドのフラットなコマンド面はクロススタック対応の意図的な構造。分割しない。
- CI(`run_ci.bat` / workflows)は機能している。変更不要。

---

## Implementation Phases

### Phase 0: 現状確認

1. `git status` 確認(クリーンでなければ停止・報告)
2. Baseline Commands を実行し、結果を記録

### Phase 1: 特性テスト拡充(D1)

1. ビルダーごとに代表入力の出力バイト列を採取し、テストを追加
2. 全テスト実行

### Phase 2: ビルダー分離(D2)

1. static ビルダーを `SlmpPayloads` へ move-only 移動 → 全テスト
2. instance 依存ビルダーは依存を報告し、引数化案を Stop And Ask に記録(実装しない)

### Phase 3: read-plan 分離(D3、任意)

1. D2 完了後に着手。move-only 分離 → 全テスト
2. 確信が持てなければ提案として報告

### Phase 4: 検証と報告

1. 全 Verification Requirements を最終実行し、Reporting Format に従って報告

---

## Verification Requirements

各フェーズ完了時に最低限:

```powershell
dotnet build PlcComm.Slmp.sln
dotnet test PlcComm.Slmp.sln --no-build
dotnet format PlcComm.Slmp.sln --verify-no-changes
```

最終フェーズでは追加で:

- テスト件数が baseline から増えていること(D1 追加分)
- `git diff` で確認: 公開シグネチャ無変更、既存ベクトル無変更、csproj / props /
  `CHANGELOG.md` 無変更、samples 無変更

---

## Reporting Format

1. **Baseline 結果**: 実行コマンドと結果(テスト件数)
2. **D1 追加テスト一覧**: ビルダー × 入力ケース
3. **D2/D3 の移動一覧**(実施時)/ 見送り理由(未実施時)
4. **各フェーズの検証結果**: 最後に実行したコマンドと結果(失敗を隠さない)
5. **Stop And Ask**: 発生した質問と停止範囲
6. **未実施事項**

---

## Out-of-scope Items(やらないこと)

- 公開 API の変更・追加・整理
- 送信フレームバイト列・ガード挙動・例外文言の変更
- `SlmpEndCodes.cs` / `SlmpDeviceRanges.cs` のデータ変更
- コマンド面の再構成、sync API の追加
- バージョン変更、`CHANGELOG.md` 更新、NuGet publish
- 依存追加、csproj / props / CI 変更
- `samples/` / `docsrc/` / `internal_docs/` の変更
- 実機 PLC を使う検証
- 兄弟リポジトリの変更
