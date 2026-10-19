# 設計メモ — 現場日報ボード（genba-nippo）

## 1. 題材の選び方

ASP.NET Core（.NET 10）と Vue 3 + TypeScript で、業務系 Web アプリを設計からテストまで一人で作る個人開発。
5 分で全体を説明でき、かつ設計の判断を具体的に語れる規模に絞った。

- 題材は **一般的な建設業務**（作業日報・工程の進捗）だけを使う。特定の顧客・案件の情報や仕様は一切含めない
- 画面は 3 つ（現場一覧・現場詳細〔工程表／日報一覧のタブ〕・日報の入力と承認）、テーブルは 4 つ。1 人で 1〜2 週間で作り切れる大きさ
- 「CRUD だけ」にならないよう、設計判断が必要な業務ルールを 4 つ入れた（下記 3 章）

## 2. 機能スコープ（MVP）

| 機能 | 内容 |
|---|---|
| 現場 | 現場名・所在地・工期を登録して一覧表示 |
| 工程 | 現場ごとに工種（例: 基礎工事）と予定期間を登録。進捗ボードで「予定 vs 実績」と遅れを表示 |
| 作業日報 | 日付・天候・作業員数・作業内容・工程ごとの進捗率を記録 |
| 承認フロー | 下書き → 提出 → 承認 / 差戻し。承認された日報の進捗率だけが工程に反映される |
| CSV 出力 | 月単位で日報を CSV 出力（Excel でそのまま開ける UTF-8 BOM 付き） |

スコープ外（意図的に外したもの）: 認証・権限、写真添付、帳票 PDF、マルチテナント。README の「今後の拡張」に記載。

## 3. 設計判断が必要な業務ルール

1. **承認フローの状態遷移をドメインに閉じ込める**
   `DailyReport` が自分の状態遷移を持ち、不正な遷移（承認済みの編集、下書きの承認など）は例外にする。
   コントローラーには `if (status == ...)` を書かない。
2. **古い日報の承認で進捗が巻き戻らない**
   10/20 の日報（進捗 60%）が先に承認され、あとから 10/18 の日報（進捗 40%）が承認されても、工程の進捗は 60% のまま。
   工程は「最後に反映した日報の日付」を持ち、それより古い日付の反映は無視する。
3. **遅れ判定は「今日」を注入してテストする**
   予定進捗 = 工期の経過日数 ÷ 全日数。実績が予定より閾値（既定 10 ポイント）以上低ければ「遅れ」、終了予定日を過ぎて未完了なら「期限超過」。
   .NET 8 で追加された `TimeProvider` を DI して、テストでは日付を固定する。閾値は `appsettings.json` の Options で変更できる。
4. **同時編集の衝突を 409 で返す（楽観的同時実行制御）**
   日報は `Version` を持ち、画面が読み込んだ時点の Version を送り返す。他の人が先に更新していれば 409 Conflict を返し、画面は「最新の内容を読み込む」ボタンを出す。
   工程も `Version` を持つ。別々の日報が同時に承認され、同じ工程の進捗を取り合った場合に、後から保存した側が古い値で上書きしないよう DB の UPDATE 時に検出する。
   どちらも EF Core の同時実行トークン（`UPDATE ... WHERE Version = 読み込んだ値`）で実現している。

## 4. 構成

```
Vue 3 + TypeScript (Vite)  ──/api──▶  ASP.NET Core Web API (.NET 10)
                                          │ Controllers → Application Services
                                          │ Domain（業務ルール・依存なし）
                                          ▼
                                     EF Core 10 → SQLite（SQL Server に差し替え可能）
```

| プロジェクト | 役割 |
|---|---|
| `GenbaNippo.Domain` | エンティティと業務ルール。外部ライブラリに依存しない |
| `GenbaNippo.Infrastructure` | EF Core の DbContext・テーブル定義・マイグレーション |
| `GenbaNippo.Api` | コントローラー・ユースケース（Application Services）・DTO・例外→ProblemDetails 変換 |
| `GenbaNippo.Domain.Tests` | 業務ルールの単体テスト（xUnit） |
| `GenbaNippo.Api.Tests` | `WebApplicationFactory` を使った API の結合テスト（SQLite インメモリ） |
| `web/` | Vue 3 + TypeScript のフロント。Vitest で単体テスト |

## 5. API 一覧

| メソッド | パス | 内容 |
|---|---|---|
| GET / POST | `/api/sites` | 現場の一覧 / 登録 |
| GET | `/api/sites/{siteId}` | 現場の詳細 |
| GET / POST | `/api/sites/{siteId}/work-items` | 工程の一覧（遅れ判定つき）/ 登録 |
| PUT | `/api/work-items/{id}` | 工程の更新 |
| GET / POST | `/api/sites/{siteId}/reports?month=YYYY-MM` | 日報の一覧 / 作成 |
| GET / PUT | `/api/reports/{id}` | 日報の取得 / 更新（Version 必須） |
| POST | `/api/reports/{id}/submit` `/approve` `/reject` | 状態遷移（Version 必須） |
| GET | `/api/sites/{siteId}/reports/export?month=YYYY-MM` | 月次 CSV 出力 |

エラーはすべて RFC 9457 の ProblemDetails 形式で返す（400 入力エラー / 404 / 409 衝突・状態不正）。
