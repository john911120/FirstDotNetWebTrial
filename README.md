# FirstDotNetWebTrial（大阪府住所検索）

[![CI](https://github.com/john911120/FirstDotNetWebTrial/actions/workflows/ci.yml/badge.svg)](https://github.com/john911120/FirstDotNetWebTrial/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)

大阪府内の住所を、地域・町名・丁目・番・号を選んで組み立てる Web アプリケーションです。ASP.NET Core MVC（.NET 10）で作られた学習用のアプリで、ブラウザーから利用します。

## 主な機能

- **住所の組み立て**：大阪府の43市町村（72地域：大阪市24区・堺市7区・その他の市町村）から地域を選び、町名・丁目・番・号を指定して住所を作成します。
- **入力の検査と表記の統一**：全角数字は半角に、丁目は「3丁目」の形に統一します。存在しない丁目や、番を省いた「号だけ」の指定などは受け付けません。
- **郵便番号の検索**：公開 API「ポストくん」を使い、地域と町名から郵便番号の候補を表示します。API に接続できない場合も、住所の組み立ては利用できます。
- **JSON でのダウンロード**：作成した住所1件、または府内の市町村一覧と大阪市の町名データ一式を JSON ファイルで保存できます。
- **ライト／ダーク表示**：画面の配色を切り替えられます。選んだ配色はブラウザーに保存されます。

### 住所の例

| 指定内容 | 作成される住所 |
| --- | --- |
| 大阪市東淀川区 → 大桐 → 2丁目 | `大阪府大阪市東淀川区大桐2丁目` |
| 大阪市北区 → 角田町 → 丁目なし → 番 `5`・号 `15` | `大阪府大阪市北区角田町5-15` |
| 島本町 → 桜井 → 丁目 `2` | `大阪府三島郡島本町桜井2丁目`（郡名は自動で付きます） |

大阪市24区は収録済みの町名・丁目の一覧から選びます。それ以外の地域は町名を入力します（形式のみ検査し、実在するかどうかは確認しません）。

## 必要なもの

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker（PostgreSQL から住所データを読み込む場合のみ）

## 起動方法

```powershell
git clone https://github.com/john911120/FirstDotNetWebTrial.git
cd FirstDotNetWebTrial
dotnet run --project OsakaAddressMvc
```

ターミナルに表示された `https://localhost:xxxx` をブラウザーで開きます。郵便番号の検索は `/PostalCode`、またはヘッダーの「郵便番号検索（API）」から開きます。

Visual Studio や Rider では、`OsakaAddress.slnx` を開きます。

### PostgreSQL から読み込む場合（任意）

標準では、アプリに同梱の JSON ファイルから住所データを読み込みます。設定を変えると、PostgreSQL から同じデータを読み込むこともできます。画面の動作と結果はどちらでも同じです。

1. リポジトリのルートに `.env` を作り、データベースのパスワードを書きます（このファイルは Git の管理対象外です）。

   ```text
   OAMVC_DB_PASSWORD=任意のパスワード
   ```

2. PostgreSQL 16 を起動します（ポート `5434`、データベース `oamvc`、ユーザー `oamvc_user`）。

   ```powershell
   docker compose up -d
   ```

3. 接続文字列と読み込み元を User Secrets に保存します。

   ```powershell
   dotnet user-secrets set "ConnectionStrings:OAMVC" "Host=localhost;Port=5434;Database=oamvc;Username=oamvc_user;Password=..." --project OsakaAddressMvc
   dotnet user-secrets set "AddressData:Source" "Database" --project OsakaAddressMvc
   ```

4. `dotnet run --project OsakaAddressMvc` で起動します。初回はテーブルと初期データが自動で作成されます。ログに `住所データの読み込み元：PostgreSQL` と表示されれば切り替わっています。

JSON に戻すには、`dotnet user-secrets remove "AddressData:Source" --project OsakaAddressMvc` を実行します。

## テスト

```powershell
dotnet test
```

PostgreSQL を使うテストは、接続先を環境変数で指定したときだけ実行されます。指定がない場合は「スキップ」と表示されますが、失敗ではありません。手順の詳細と画面での確認項目は [動作確認の手順](OsakaAddressMvc/TEST_PLAN.md) を参照してください。

`main` への push とプルリクエストのたびに、GitHub Actions が PostgreSQL を含むすべてのテストを実行します。

## ドキュメント

| 文書 | 内容 |
| --- | --- |
| [利用ガイド](OsakaAddressMvc/README.md) | 画面の使い方、入力の規則、URL での指定、JSON の形式、設定、データの出典 |
| [動作確認の手順](OsakaAddressMvc/TEST_PLAN.md) | 自動テストの実行方法と、画面での確認項目 |

## 今後の予定

- 住所を正規化する Web API の追加と、OpenAPI による仕様の公開
- 大阪市以外の地域の町名データの収録

大阪都構想の住民投票（2027年春の見通し）の結果によって、大阪市の区の構成が変わる可能性があります。このため、上記の作業は投票の結果を確認してから着手します。

## 注意事項

- 学習を目的としたアプリです。住所データは公開情報をもとに作成したもので、公式の住所マスタではありません。
- 番・号は形式のみを検査し、実在する住所かどうかは確認しません。
- 大阪市の区の構成が変更された場合、収録データは実際の住所と一致しなくなります。
- 郵便番号の検索は外部サービスを利用します。利用条件はポストくんの[利用規約](https://postcode.teraren.com/terms)に従ってください。
