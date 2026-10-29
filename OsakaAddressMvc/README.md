# Osaka Address MVC（学習用 v0.6.0-alpha.1）

ASP.NET Core MVC / C# 14 / .NET 10 の練習用プロジェクトです。**大阪府の43市町村（33市・9町・1村）** を対象に、地域（大阪市24区・堺市7区・その他の市町村）を選び、**町名・丁目（ある場合）・番・号** を指定して住所を組み立て、検索結果やデータ一式を JSON ファイルとしてダウンロードできます。Rider、Visual Studio、VS Code のいずれでも編集できます。v0.6.0-alpha.1 で **DbUp による PostgreSQL へのマイグレーション**を追加しました（画面は引き続き JSON を使用。EF Core・Docker は未導入）。

- v0.3：ポストくん公開 API による郵便番号検索を別 URL に追加（APIキー不要。既存の住所検索は外部API障害の影響を受けません）。
- v0.4：起動時のデータ検証、`JsonSerializerOptions` の再利用、xUnit による自動テストを追加。
- v0.5：対象を大阪市から大阪府全域へ拡張（詳細は「v0.5 の更新内容」）。
- v0.5.5：機能は変えずに整理・テスト強化（設定の外部化、ポストくん API のテスト、DB 接続テストの安全化。詳細は「v0.5.5 の更新内容」）。
- **v0.6.0-alpha.1：DbUp で住所データを PostgreSQL に登録**（テーブル作成・初期データ・起動時マイグレーション。詳細は「v0.6.0-alpha.1 の更新内容」）。

## v0.1 の概要

1. 大阪市の区名を日本語・ローマ字で検索する。
2. 東淀川区の19町名と学習用丁目候補を選択する。
3. LINQ の `Where` / `Select` / `Distinct` でデータを絞り込む。
4. `Controller → Service → ViewModel → Razor View` で GET リクエストを処理する。
5. 日本語の住所（例：大阪市東淀川区大桐2丁目）を表示する。

## v0.2 の更新内容

1. **24区すべての町名に対応**：大阪市公式の「区別町名一覧」を参考に、`Data/Wards/` に区ごと1ファイル、計24個の JSON ファイルを追加しました。合計 **580件の町名** を収録しています。`AddressService` から町名の定義を切り離し、`AddressDataProvider` が読み込みます。
2. **町名に対応する丁目候補**：共通の `1～7丁目` を廃止。町名ごとに丁目の範囲を保持します。丁目がない町にも対応し、`中央区上町` のような「丁目なし／丁目あり」の両方を使う例にも対応しています。
3. **jQuery と関連ライブラリを撤去**：jQuery本体・Validation・Unobtrusive Validation の配布ファイル、レイアウトの読み込み、不要な検証用 Partial View を削除しました。選択の更新と色モード切替は標準 JavaScript で実装しています。
4. **JSON ダウンロード**：住所を表示した後の「選択した住所を JSON で保存」と、「全24区データを JSON でダウンロード」の2種類を追加しました。UTF-8 で出力します。
5. **ライト／ダーク切替**：Bootstrap 5.3 の `data-bs-theme` を使用。ヘッダーのボタンで切り替え、選択内容をブラウザーの `localStorage` に保存します。
6. **学習向けの構造分離**：検索・検証・住所構築のロジックを `AddressService` に維持し、データを JSON、読み込み処理を Provider に切り分けました。

## v0.2.1 のバグ修正（丁目なし町名・番／号入力）

- **原因**：v0.2 では丁目のない町名（例：北区角田町）を選ぶと、丁目の選択欄が無効になる一方、住所の番・号を指定する入力欄がありませんでした。このため `大阪府大阪市北区角田町5-15` を作成できませんでした。なお `5-15` は「5丁目15」ではありません。
- **修正内容**：丁目が存在する町名では従来どおり丁目のプルダウンを使い、丁目がない町名では「丁目なし」と説明して、独立した **番（街区符号）・号（住居番号）** の欄から入力できるようにしました。丁目のある町名も番・号の入力に対応します。
- **例**：北区 → 角田町 → 丁目なし → 番 `5`・号 `15` → **`大阪府大阪市北区角田町5-15`**。東淀川区 → 大桐 → 2丁目 → （番・号は空欄）→ **`大阪府大阪市東淀川区大桐2丁目`**。
- **入力値の検証**：番・号は任意ですが、指定時は半角／全角の数字1～6桁とし、表示・ダウンロード時には半角に統一します。号だけの指定や、区／町／丁目の不正な組み合わせは拒否します。**実在する番・号かどうかは確認しません**。
- **JSON**：選択住所のダウンロードに `ban`・`go` を追加し、組み立て後の住所と一致させました。表示する住所には都道府県 `大阪府` を含めています。全24区の町名データは変更していません。
- **データの根拠**：[大阪市 北区町名一覧](https://www.city.osaka.lg.jp/shimin/page/0000061051.html) では角田町に丁目がありません。[HEP FIVE 公式アクセス](https://www.hepfive.jp/access) の所在地は `大阪府大阪市北区角田町5-15` です。

## v0.3 の更新内容（API 接続先の変更、バージョン据え置き）

**目的：従来の MVC 住所検索を変更せず、公開 REST API「ポストくん」で郵便番号だけを別画面で検索する。** 全国住所データの一括取込・CSV更新・DB・バッチ・キャッシュは導入しません。

1. **別 URL**：`GET /PostalCode` で区を選択して町名一覧を表示し、`POST /PostalCode/Search` で郵便番号を検索します。従来の `/Address/Index` と2種類の JSON ダウンロードは変更していません。
2. **接続先をポストくんへ変更**：`PostkunPostalCodeService` が `GET https://postcode.teraren.com/postcodes.json` を `HttpClient` / `async/await` で呼び出します。検索語には大阪市の区・町名を含め、必要時に町名単独検索も試みます。**登録・APIキー・環境変数は不要**です。
3. **候補の厳格な照合**：JSON 配列の `new`（7桁の郵便番号）と `prefecture`、`city`、`suburb` を読み取り、**大阪府・大阪市の選択した区・町名**に完全一致した候補のみ表示します。違う地域の同名町名は表示しません。重複郵便番号は除外します。
4. **制限事項**：郵便番号は原則として町域単位です。丁目・番・号の存在確認や建物固有の郵便番号を保証しません。API の検索範囲・応答件数（最大100件／照会）などの制限で結果が得られない場合もあります。
5. **障害隔離**：未検出・アクセス制限・通信失敗・タイムアウト・不正な JSON を案内。タイムアウトは8秒で自動再試行しません（検索語を変えた補助照会は実施）。失敗しても住所表示、町名データ、JSONダウンロードは利用できます。
6. **既存機能を維持**：`AddressService`・`AddressDataProvider`・`Data/Wards/`（24ファイル）・Bootstrap 5.3の色モード・jQuery非使用・v0.2.1の番／号入力は従来どおりです。住所JSONの `version` 項目は互換性のため `0.2.1` のままです。

### v0.3 の画面レイアウト調整（同一バージョン）

- `/PostalCode` の「区選択」と「町名選択」それぞれの操作ボタンを、Bootstrap グリッドを使って選択欄の右側に揃えました。画面幅が狭い場合はボタンが入力欄の下に並び、横幅を確保します。
- 「住所検索に戻る」ボタンを検索カード内の右下に配置しました。結果カードは従来どおり下段に表示します。
- MVC テンプレート由来のフッター `position: absolute` を通常のフローに変更し、ズーム時や狭い画面でもボタンとフッターが重ならないようにしました。
- **画面配置のみの変更**です。Controller、外部API、住所JSON、ルーティング、バージョン番号は変更しません。

### 郵便番号検索の使い方

プロジェクトフォルダーで `dotnet run` を実行した後、表示されるローカルURLに `/PostalCode` を付けてアクセスします。

1. 区（例：`北区`）を選択し「町名一覧を表示」を押します。
2. 町名（例：`角田町`）を選び、「ポストくんで郵便番号を検索」を押します。
3. API が一致する郵便番号を返した場合のみ候補が表示されます。HEP FIVE の `5-15` は検索対象に含めません。
4. 接続エラーが出ても「住所検索に戻る」から既存の住所検索を使用できます。

外部 API の利用条件・仕様は [ポストくん REST API 仕様](https://postcode.teraren.com/doc) と [利用規約](https://postcode.teraren.com/terms) を参照してください。個人・法人の無料利用、登録不要は提供元の公開案内に基づきます。規約や仕様は変更される可能性があります。個人の詳細住所（番・号）は API に送信しません。

### v0.3 の関連ファイル

```text
Controllers/PostalCodeController.cs   # 郵便番号検索の GET/POST
Models/PostalCodeViewModel.cs         # 検索結果の表示用モデル
Services/PostkunPostalCodeService.cs  # ポストくん REST API 呼び出し
Views/PostalCode/Index.cshtml         # 独立した Razor ページ
Program.cs                            # HttpClient DI 登録
```

## v0.4 の更新内容（起動時検証・JSON 設定の再利用・自動テスト）

1. **起動時のデータ検証**：`Program.cs` で `AddressDataProvider` を起動時に生成するようにしました。これまでは Singleton が最初のリクエスト時に生成されていたため、区データに不備があってもアプリは起動し、最初の利用者に 500 エラーが表示されていました。現在は起動時点で例外となり、アプリが起動しません。
2. **`JsonSerializerOptions` の再利用**：`AddressDataProvider`（ファイル読み込みのループ内）と `AddressController.Download` で毎回生成していた設定を `static readonly` フィールドに移しました（コード分析ルール CA1869）。
3. **xUnit による自動テスト**：`OsakaAddressMvc.Tests` プロジェクトを追加し、`TEST_PLAN.md` の No.3・6・7・16・18～21 を `BuildAddress` の単体テストとして自動化しました。テストプロジェクトは Web プロジェクトと同じ階層に配置しています（Web プロジェクトのフォルダー内に置くと、テストのソースまで Web 側でコンパイルされるため）。

```powershell
# ソリューションのフォルダー（OsakaAddress.slnx がある場所）で実行
dotnet test
```

```text
OsakaAddress/
├── OsakaAddress.slnx
├── OsakaAddressMvc/          # Web アプリ本体
└── OsakaAddressMvc.Tests/    # xUnit テスト
```

## v0.5 の更新内容（大阪府全域への拡張）

**目的：v0.4 までは大阪市24区のみが対象で、堺市・豊中市・吹田市など大阪市外の住所を一切扱えなかった。府内すべての市町村を選択・組み立て・郵便番号検索できるようにする。**

### 1. 「区」から「地域」へのモデル変更

- 選択単位を **地域（`AreaInfo`）** に変更しました。政令指定都市は区ごと、それ以外は市町村ごとに1件で、合計 **72地域**（大阪市24区 ＋ 堺市7区 ＋ 31市 ＋ 9町1村）です。
- 地域キーは府内で一意な表記です（例：`大阪市北区`・`堺市北区`・`豊中市`・`島本町`）。大阪市と堺市の両方にある **北区・西区** も区別できます。
- 町村の住所には郡名を付けます（例：`大阪府三島郡島本町桜井2丁目`、`大阪府南河内郡千早赤阪村水分180`）。

### 2. 市町村マスタ `Data/Municipalities.json` を追加

- 大阪府の43市町村について、名前・ローマ字・種別（政令指定都市／市／町／村）・郡名・**地方公共団体コード（5桁）**・行政区を収録しました。
- 出典は大阪府公式サイトの[住民基本台帳人口に関するページ](https://www.pref.osaka.lg.jp/o040050/shichoson/juki/juki2012.html)に掲載された市区町村一覧です。地方公共団体コードはポストくん API の応答（`jis` 項目）と一部照合しました。
- 起動時に「43市町村であること」「名前・コードの重複や形式不正がないこと」「大阪市の区の一覧と `Data/Wards/` の24ファイルが過不足なく一致すること」を検証し、不備があれば起動を中止します（v0.4 の方針を継承）。

### 3. 町名の扱い（大阪市とそれ以外で異なる）

| 地域 | 町名の指定方法 | 丁目 | 検証の範囲 |
| --- | --- | --- | --- |
| 大阪市24区 | 収録済みの町名一覧から選択（v0.4 と同じ） | 町名ごとの丁目一覧から選択 | 町名・丁目の組合せが収録データと一致すること |
| 堺市7区・その他の市町村 | **町名を入力** | 数字（1～99）を入力。`3`・`３`・`3丁目` は `3丁目` に統一 | **形式のみ**（漢字・ひらがな・カタカナで30文字以内、数字・記号・空白・「丁目」を含めない） |

- 大阪市外の町名・丁目データは未収録のため、**入力された町名が実在するかどうかは確認しません**。画面に「入力」バッジと注意書きを表示します。
- 町名の実在確認には、郵便番号検索（ポストくん API）で町域名が一致するかを確認できます。
- 番・号の規則（1～6桁の数字、全角→半角、号のみ不可、先頭0不可）は全地域で共通です。

### 4. 郵便番号検索（`/PostalCode`）の府内全域対応

- 地域の選択欄を、大阪市・堺市・その他の市・町村のグループ（`optgroup`）で表示します。大阪市の区は町名を一覧から選び、それ以外の地域は町名を入力します。
- API 応答の照合条件を「`大阪市{区}` と完全一致」から、**都道府県＝大阪府、町名（`suburb`）が完全一致、かつ地方公共団体コード（`jis`）または市区町村名（郡名を含む `city`、例：`堺市堺区`・`三島郡島本町`）が一致** に変更しました。別の地域の同名町域は表示しません。
- 外部 API へ送信する前に、地域の存在と町名の形式をサーバー側で検証します。不正な入力では API を呼び出しません。

### 5. 互換性

- v0.4 までの URL（`/Address?ward=北区&town=角田町` など区名のみの指定）は、**大阪市の区** として引き続き受け付けます。新しい URL は `area=大阪市北区` の形式です。堺市にのみある区名（`中区`・`美原区` など）は区名だけでは解決せず、`area=堺市中区` のように指定します。
- 住所 JSON の形式を変更し、`county`（郡）・`city`（市町村）・`ward`（区、ない場合は `null`）・`localGovernmentCode`・`townSource` を追加しました。`version` は v0.2.1 からの据え置きをやめ **`0.5`** に統一しました。
- 全データ JSON（`/Address/DownloadDataset`）は、府内43市町村の一覧（`municipalities`）と大阪市24区の町名データ（`osakaCityWards`）を出力します。

### 6. その他

- バージョン表記を `AppInfo.Version`（`0.5`）に集約し、画面・User-Agent・JSON で共通に使用します。プロジェクトの `<Version>` も `0.5.0` に設定しました。
- 番・号の入力欄を部分ビュー `Views/Address/_BanGoInputs.cshtml` に切り出し、町名選択型・町名入力型の両方で共用します。
- 使用していなかった旧 API 用の設定クラス `Models/JapanPostApiOptions.cs` を削除しました（TEST_PLAN の D-11「旧 API 用のファイルを残さない」に対応）。
- コード内のコメントと README・TEST_PLAN をすべて日本語に統一しました（MVC テンプレート由来の英語コメントも翻訳）。
- xUnit テストを大阪府全域の地域データ・住所組み立て・旧 URL 互換の確認まで拡張しました（`AreaDataTests.cs` を追加）。

### 7. 区データのファイル名と地方公共団体コード（データのみの変更）

- `Data/Wards/` の24ファイルを、区名だけのファイル名（例：`北区.json`）から **地域キーと同じ名前**（例：`大阪市北区.json`）に変更しました。将来 `堺市北区.json` などを追加しても、同名の区とファイル名が衝突しません。
- 各ファイルの先頭に **`"code"`（地方公共団体コード、例：北区 `27127`）** を1行追加しました。値は `Municipalities.json` の大阪市の区のコードと一致します。町名・丁目のデータは変更していません（24区・580町名）。
- **モデル・読み込みロジックは変更していません。** `AddressDataProvider` はファイル名ではなく JSON 内の `"ward"` で区を識別し、`WardData` に定義のない `"code"` は読み込み時に無視されます。このため全データ JSON の `osakaCityWards` には `code` は出力されません。
- `"code"` で区データを検索する方式（`WardData.Code` の追加、`AddressDataProvider` の辞書キーを区名からコードへ変更）は、大阪市外の町名データを収録する段階で導入する予定です。

### v0.5 の操作例

1. 「地域名検索」に `吹田` と入力して検索し、「吹田市」を選びます。
2. 町名に `千里山西`、丁目に `5`、番に `10`、号に `3` を入力します。
3. 「住所を表示」で `大阪府吹田市千里山西5丁目10-3` が表示されます（形式のみ検査）。
4. 「ポストくんで郵便番号を調べる」で、API に `千里山西` の町域が存在するかを確認できます。

## v0.5.5 の更新内容（整理・テスト強化）

**目的：v0.5 の機能は変更せず、安全性・保守性・テストの信頼性を高める。** 画面の操作と住所の組み立て結果は v0.5 と同じです（JSON の `version` のみ `0.5.5` に変わります）。

### 1. DB 接続テストの整理（セキュリティ）

- `DatabaseConnectionTests.cs` に書かれていた接続文字列（パスワードを含む）の既定値を削除しました。接続文字列は環境変数 `OSAKA_TEST_DB` からのみ読み込みます。
- 新しい属性 `[DatabaseFact]`（`FactAttribute` を継承）を追加しました。環境変数が未設定の場合はテストをスキップするため、**DB を起動していなくても通常の `dotnet test` は成功**します。
- `[Trait("Category", "Integration")]` を付け、`--filter "Category=Integration"` で疎通確認だけを実行できるようにしました。
- クラス名を `DataBaseConnectionTests` から `DatabaseConnectionTests`（ファイル名と一致）に、`String` を C# の慣例どおり `string` に変更しました。

### 2. バージョン表記とテストの修正

- `AppInfo.Version` を `0.5.5`、プロジェクトの `<Version>` を `0.5.5` にしました。画面・User-Agent・JSON はすべて `AppInfo.Version` を参照します。
- JSON のバージョンを確認するテストは `"0.5"` を直接書いていたため、バージョンを上げると失敗していました。期待値を `AppInfo.Version` から取得するように変更し、テスト名を `住所JSONのバージョンはAppInfoと一致する` にしました。

### 3. 設定値の外部化（Options パターン）

`appsettings.json` に次のセクションを追加し、コードに直接書いていた値を移しました。Spring の `@ConfigurationProperties` に相当します。

```json
"AddressData": {
  "DataDirectory": "Data"
},
"PostalCodeApi": {
  "BaseAddress": "https://postcode.teraren.com/",
  "TimeoutSeconds": 8
}
```

- `AddressData:DataDirectory`：住所データのフォルダー。相対パスはコンテンツルートを基準に解決します。
- `PostalCodeApi`：郵便番号 API の接続先とタイムアウト（秒）。起動時に値を検証し、不正な URL や 1～60 秒以外のタイムアウトでは起動を中止します。
- `AddressDataProvider` は `IWebHostEnvironment` 全体ではなく、`IOptions<AddressDataOptions>`（データフォルダーの場所）だけを受け取るように変更しました。これにより、テスト用の `TestWebHostEnvironment`（使わないプロパティを6つ実装していた偽クラス）が不要になりました。
- 設定クラスは `Models/AppOptions.cs` にまとめています。

### 4. テストの追加・整理

- **ポストくん API のテスト**（`PostkunPostalCodeServiceTests`）を追加しました。偽の `HttpMessageHandler` で応答を差し替えるため、外部 API には接続しません。候補の照合（堺市北区・他府県の同名町域の除外、重複除外）、404・429・500、不正な JSON、通信エラー、タイムアウト、利用者による取り消しを確認します。
- v0.5 の `TEST_PLAN.md`（G-1）に記載していたものの未実装だった地域対応のテスト（区名のみの指定、大阪市以外の地域での住所組み立て、町名・丁目の形式検査など）を `BuildAddressTests` に追加しました。
- `BuildAddressTests` と `AreaDataTests` で重複していた件数確認（24区・72地域）を `AreaDataTests` に一本化しました。
- テストプロジェクトのデータコピー範囲を `Data/Wards/*.json` から `Data/**/*.json` に変更し、`Municipalities.json` も明示的にコピーするようにしました。
- 使用していなかったパッケージ `Npgsql.EntityFrameworkCore.PostgreSQL` をテストプロジェクトから削除しました（DB 接続テストは `Npgsql` のみ使用）。

### 5. 不要なファイルの削除

- jQuery 関連（`wwwroot/lib/jquery`・`jquery-validation`・`jquery-validation-unobtrusive`）と `Views/Shared/_ValidationScriptsPartial.cshtml` を再度削除しました。IDE のテンプレート機能などで再生成された場合は、v0.2 の方針どおり削除してください。
- テンプレート由来の `HomeController`・`Views/Home`（Index・Privacy）とヘッダーの Privacy リンクを削除しました。エラー画面は `ErrorController`（`/Error`）として独立させ、表示内容を日本語にしました。
- 使用していなかった `Models/JapanPostApiOptions.cs` を削除しました。

### 6. リポジトリ管理

- ソリューションのフォルダーに `.gitignore` を置き（`bin/`・`obj/`・`.idea/`・`*.DotSettings.user` など）、`git init` 済みの状態にしました。最初のコミットはご自身の名前で行ってください。

```powershell
git add .
git commit -m "v0.5.5: 整理・テスト強化"
```

- 本体とテストは同じコミットで管理してください。旧バージョンのテストを新しいバージョンに上書きすると、v0.5 作成時のように API の不一致でビルドが失敗します。

## v0.6.0-alpha.1 の更新内容（DB マイグレーション：DbUp）

**目的：v0.6 の第1段階として、住所データを PostgreSQL に登録する仕組みを作る。** 画面は引き続き JSON のデータを使います（DB からの読み込みは次の段階）。DB を用意しなくても、従来どおり JSON だけで動作します。

### 1. なぜ Flyway ではなく DbUp か

Java（Spring Boot）では `flyway-core` を依存関係に追加するだけで、起動時に自動でマイグレーションが実行されます。一方 .NET では Flyway はビルド・実行の流れの外にある Java 製ツールのため、CLI や Docker イメージの管理、JDBC 形式の接続文字列が別途必要になります。そこで、**NuGet パッケージ1つで同じ考え方（SQL ファイルによるスキーマ履歴管理）を実現できる DbUp** を採用しました。

- パッケージ：`dbup-postgresql` 7.0.1（`dbup-core` と `Npgsql` は推移的に追加される）
- スクリプトの命名は Flyway と同じ `V{番号}__{説明}.sql`。将来 Flyway に移行しても SQL はそのまま使えます。
- **番号は3桁でゼロ埋め**します（`V001`）。DbUp はファイル名の文字列順で実行するため、`V10` が `V2` より先に実行されるのを防ぎます。
- 適用済みのスクリプトは DB の `schemaversions` テーブルに記録され、再実行されません（Flyway の `flyway_schema_history` に相当）。

### 2. テーブルと初期データ

| スクリプト | 内容 |
| --- | --- |
| `db/migration/V001__create_tables.sql` | `municipalities`（43市町村）・`wards`（大阪市24区・堺市7区）・`towns`（大阪市の町名）・`chomes`（丁目）を作成。主キー・外部キー・一意制約・CHECK 制約（郡名は町村のみ、など）でデータの整合性を DB 側でも保証 |
| `db/migration/V002__seed_municipalities_and_wards.sql` | 市町村43件・行政区31件（`Data/Municipalities.json` から生成） |
| `db/migration/V003__seed_osaka_city_towns.sql` | 大阪市24区の町名580件・丁目1,815件（`Data/Wards/` から生成） |

- スクリプトは `csproj` の `EmbeddedResource` でアセンブリに埋め込むため、出力先へのコピー漏れ（v0.5.5 で起きた `bin/` の問題）が起きません。
- PostgreSQL は引用符なしの名前を小文字に変換するため、DB 名・テーブル名・列名はすべて小文字（`snake_case`）にしています。
- 初期データは v0.5.5 の JSON から一度だけ生成したものです。**適用済みのスクリプトは書き換えず**、データを変更する場合は `V004__...sql` のように新しいスクリプトを追加します。

### 3. 起動時のマイグレーション

`Program.cs` で、アプリ起動時（`Database:MigrateOnStartup` が `true` の場合）に `Infrastructure/DatabaseMigrator.cs` を呼び出します。v0.4 の「起動時のデータ検証」と同じく、**失敗した場合は起動を中止**します。

| 設定 | appsettings.json | appsettings.Development.json |
| --- | --- | --- |
| `Database:MigrateOnStartup` | `false` | `true` |
| `ConnectionStrings:OAMVC` | 空（未設定） | ― （User Secrets で設定） |

接続文字列が未設定の場合は警告をログに出してマイグレーションをスキップし、従来どおり JSON で起動します。

### 4. 開発環境での準備

1. 既存の PostgreSQL に専用のロールと DB を作成します（初回のみ）。新しいコンテナーは不要です。パスワードは実行前に書き換えてください。

   ```powershell
   psql -h localhost -p 5433 -U postgres -f OsakaAddressMvc/db/setup/create_database.sql
   ```

2. 接続文字列を **User Secrets** に保存します（プロジェクトの外に保存されるため Git に含まれません。`csproj` に `UserSecretsId` を設定済み）。

   ```powershell
   cd OsakaAddressMvc
   dotnet user-secrets set "ConnectionStrings:OAMVC" "Host=localhost;Port=5433;Database=oamvc;Username=oamvc_user;Password=..."
   ```

3. `dotnet run` で起動すると、ログに `DB マイグレーション完了：今回適用したスクリプト 3 件。` と表示されます。2回目以降の起動では `0 件` になります。

### 5. テスト

- `MigrationScriptTests`（DB 不要）：スクリプトが3桁の連番で埋め込まれていること、シードの件数（43・31・580・1,815）が JSON と一致することを確認します。
- `DatabaseMigrationTests`（手動実行）：環境変数 `OSAKA_MIGRATION_TEST_DB` を設定したときだけ、実際の DB にマイグレーションを2回適用し、全データが登録されること・2回目は何も実行されないことを確認します。**必ずテスト専用の空の DB を指定してください。**
- `[DatabaseFact]` は環境変数名を指定できるようにしました（`[DatabaseFact(DatabaseFactAttribute.MigrationEnvironmentVariable)]`）。

## ディレクトリ構成

```text
OsakaAddressMvc/
├── Controllers/
│   ├── AddressController.cs     # 画面の検索と2種類の JSON ダウンロード
│   ├── PostalCodeController.cs  # v0.3 独立郵便番号検索
│   └── ErrorController.cs       # v0.5.5 エラー画面（/Error）
├── Models/
│   ├── AddressViewModel.cs      # 画面状態
│   ├── WardData.cs              # 区・町・丁目のデータ型
│   ├── MunicipalityData.cs      # v0.5 市町村・行政区・地域（AreaInfo）のデータ型
│   ├── PostalCodeViewModel.cs   # 郵便番号候補の画面用モデル
│   ├── AppOptions.cs            # v0.5.5 appsettings.json の設定クラス
│   └── ErrorViewModel.cs        # エラー画面用モデル
├── Services/
│   ├── AddressService.cs        # LINQ、住所選択・組み立て、出力オブジェクト生成
│   ├── AddressDataProvider.cs   # 市町村一覧と区ごとの JSON の読み込み・起動時検証
│   └── PostkunPostalCodeService.cs  # ポストくん API 通信
├── Infrastructure/
│   └── DatabaseMigrator.cs      # v0.6 DbUp によるマイグレーション実行
├── db/
│   ├── migration/               # v0.6 V001～V003 の SQL（アセンブリに埋め込み）
│   └── setup/create_database.sql  # v0.6 開発用 DB・ロールの作成（手動・初回のみ）
├── Data/
│   ├── Municipalities.json      # v0.5 大阪府43市町村の一覧（郡・コード・行政区）
│   └── Wards/                   # 大阪市の区ごとの24ファイル（大阪市中央区.json など）
├── Views/
│   ├── Address/Index.cshtml     # Razor UI、ダウンロード操作
│   ├── Address/_BanGoInputs.cshtml  # v0.5 番・号の入力欄（部分ビュー）
│   ├── PostalCode/Index.cshtml  # v0.3 郵便番号検索
│   ├── Shared/_Layout.cshtml    # Bootstrap と色モードの切替ボタン
│   └── Shared/Error.cshtml      # v0.5.5 日本語のエラー画面
├── wwwroot/
│   ├── css/address.css          # Bootstrap の色変数を使用
│   ├── js/site.js               # 追加ライブラリを使わない JavaScript
│   └── lib/bootstrap/          # Bootstrap 5.3
├── AppInfo.cs                   # バージョン表記（0.6.0-alpha.1）
├── Program.cs                   # DI・Options・MVC ルーティング
├── appsettings.json             # 住所データの場所・郵便番号 API・DB マイグレーションの設定
└── TEST_PLAN.md                 # 手動確認の手順
```

テストプロジェクト（ソリューションのフォルダーで `OsakaAddressMvc` と並べて配置）：

```text
OsakaAddressMvc.Tests/
├── AreaDataTests.cs                  # 市町村・地域の構成、バージョン
├── BuildAddressTests.cs              # 住所の組み立て・入力検査（大阪市・大阪市外）
├── PostkunPostalCodeServiceTests.cs  # v0.5.5 ポストくん API の応答処理（偽 HttpMessageHandler）
├── DatabaseConnectionTests.cs        # DB 疎通確認（手動実行。通常はスキップ）
├── DatabaseFactAttribute.cs          # v0.5.5 環境変数がある場合だけ実行する Fact
└── TestServices.cs                   # テスト用のサービス生成
```

v0.5.5 で、テンプレート由来の `HomeController` と `Views/Home` は削除しました（エラー画面は `ErrorController`）。

## 起動方法（PowerShell / Rider）

.NET 10 SDK が必要です。プロジェクトフォルダー（`OsakaAddressMvc.csproj` がある場所）で実行します。

```powershell
dotnet --version
dotnet restore
dotnet build
dotnet run
```

ターミナルに表示された `http://localhost:xxxx` または `https://localhost:xxxx` にアクセスします。開発中は `dotnet watch run` も利用できます。Rider・Visual Studio で開く場合は、ソリューションのフォルダーにある `OsakaAddress.slnx` を選択してください（Web アプリとテストの両方が開きます）。テストはソリューションのフォルダーで `dotnet test` を実行します。

## 操作例

1. 「地域名検索」に `東淀川` と入力して検索し、「大阪市（24区）」グループの「東淀川区」をクリックします。
2. 町名に「大桐」を選択します。丁目候補が更新されたら「2丁目」を選びます。
3. 「住所を表示」をクリックすると `大阪府大阪市東淀川区大桐2丁目` が表示されます。
4. 「選択した住所を JSON で保存」で1件の住所をダウンロードします。
5. ページ下部の「大阪府データを JSON でダウンロード」で、府内43市町村の一覧と大阪市24区の町名データを取得できます。
6. ヘッダーのボタンでライト／ダークモードを切り替えます。

### 住所 JSON のイメージ

```json
{
  "prefecture": "大阪府",
  "county": null,
  "city": "大阪市",
  "ward": "東淀川区",
  "town": "大桐",
  "chome": "2丁目",
  "ban": null,
  "go": null,
  "formattedAddress": "大阪府大阪市東淀川区大桐2丁目",
  "localGovernmentCode": "27114",
  "townSource": "大阪市 区別町名一覧（収録データから選択）",
  "version": "0.5.5"
}
```

大阪市外の例（町村は `county` に郡名が入ります）：

```json
{
  "prefecture": "大阪府",
  "county": "三島郡",
  "city": "島本町",
  "ward": null,
  "town": "桜井",
  "chome": "2丁目",
  "ban": null,
  "go": null,
  "formattedAddress": "大阪府三島郡島本町桜井2丁目",
  "localGovernmentCode": "27301",
  "townSource": "利用者入力（形式のみ検査）",
  "version": "0.5.5"
}
```

住所一件のダウンロードは `/Address/DownloadJson?area=大阪市東淀川区&town=大桐&chome=2丁目`、`/Address/DownloadJson?area=大阪市北区&town=角田町&ban=5&go=15`、`/Address/DownloadJson?area=島本町&town=桜井&chome=2` など、全体は `/Address/DownloadDataset` です。v0.4 までの `ward=東淀川区` 形式も大阪市の区として利用できます。選択値はサーバー側で存在チェックし、不正な組合せを受け取った場合は `400 Bad Request` を返します。

## データの根拠と留意点

大阪市の町名・丁目の出典は大阪市公式サイト [区別町名一覧](https://www.city.osaka.lg.jp/shimin/page/0000061040.html) です。各区ファイルに、その区の公式ページの URL を `sourceUrl` として記録しました（公式の総合一覧ページには2026年2月20日の更新日が記載されています）。

- 本データは学習目的で町名・丁目を手動で構造化したものです。**公式住所 API・住民基本台帳・完全な住所マスタではありません**。住所の最新性、網羅性、郵便番号、番・号の実在性、建物名、実在性の最終保証はありません。
- 大阪市・堺市の区は「市町村」ではなく行政区です。v0.5 では府内43市町村と両市の行政区を「地域」として扱い、**町名・丁目のデータを収録しているのは大阪市24区のみ**です。
- 市町村一覧の出典は大阪府公式サイトの[住民基本台帳人口に関するページ](https://www.pref.osaka.lg.jp/o040050/shichoson/juki/juki2012.html)です。大阪市外の町名は利用者の入力に依存し、形式のみ検査します。
- 各町の丁目に対してすべての番地が実在することは意味しません。住所を実務利用する場合は最新の公的データや住所 API との照合が必要です。
- JSON ファイルは起動時に一度読み込みます。町名データを編集した場合はアプリを再起動してください。
- DB、ユーザー認証、住所の登録・更新機能は **未実装**です。v0.3 の郵便番号検索のみ外部 API を呼び出しますが、住所データの書き換えは行いません。

## 検証状況

提供物の作成時に、24 JSON ファイルの構文、町名の重複、丁目データ、リンクや主要ソースの静的整合性、配布アーカイブを確認しました。v0.2.1 ではさらに北区角田町の丁目なし判定と、番・号パラメータの伝播を静的検査しています。ただし、生成側の環境には .NET 10 SDK が存在しないため、**C# の実ビルド、Rider・ブラウザーでの動作確認は未実施**です。起動後は `TEST_PLAN.md` の手順で確認してください。v0.3 の API 実応答・結果表示は通信可能なローカル環境で追加確認が必要です。

v0.4 では、ローカル環境（Rider / .NET 10 SDK）でソリューションのビルドと `dotnet test` を実行し、全テストの成功を確認しました。

v0.5 では、`Municipalities.json` の件数（43市町村・72地域）と `Data/Wards/` との整合、主要な地方公共団体コード（27128 大阪市中央区・27141 堺市堺区・27231 大阪狭山市・27382 河南町）とポストくん API の `city` 表記（郡名を含む）を確認しました。**生成側の環境には .NET 10 SDK がないため、v0.5 のビルドと `dotnet test` は未実施**です。ローカル環境で `dotnet test` を実行し、`TEST_PLAN.md` の G 章の手順で画面を確認してください。

v0.5.5 では、生成側の環境で参照の整合性（削除したクラス・ビュー・設定への参照が残っていないこと）、`appsettings.json` の JSON 構文、テストで使う地域キー（`堺市堺区`・`吹田市`・`島本町` など）が `Municipalities.json` に存在することを静的に確認しました。**生成側の環境には .NET 10 SDK がないため、v0.5.5 のビルドと `dotnet test` は未実施**です。ローカル環境で実行し、結果を次に記入してください。

- 補足：旧バージョンの上に展開した環境では、`bin/` に残った v0.4 の区データ（`中央区.json` など）と重複して起動時検証で失敗した。`bin/`・`obj/` を削除して再ビルドすると解消する。

- ローカルでの `dotnet test` 結果（v0.5.5）：合計 59 件、成功 58、スキップ 1（DB 接続テスト）、失敗 0
- 実行日・環境：2026-10-21、Windows / .NET 10 SDK（10.0.13）

v0.6.0-alpha.1 では、生成側の環境で **PostgreSQL 16 に V001～V003 を実際に適用**し、件数（市町村43・行政区31・町名580・丁目1,815）と、CHECK 制約・外部キー制約が不正なデータを拒否することを確認しました。**生成側の環境には .NET 10 SDK がないため、C# のビルドと `dotnet test`、DbUp 経由での実行は未実施**です。ローカル環境で実行し、結果を次に記入してください。

- ローカルでの `dotnet test` 結果（v0.6.0-alpha.1）：合計 62 件、成功 60、スキップ 2（DB 接続テスト・マイグレーション統合テスト）、失敗 0
- DbUp による初回マイグレーション：oamvc に適用 3 件、2回目 0 件。統合テスト（oamvc_test）成功：市町村43・町名580・丁目1,815
- 実行日・環境：2026-10-29、Windows / .NET 10 SDK（10.0.14）/ PostgreSQL 16（compose.yaml）

## 次の段階（未着手）

- **大阪市外の町名データの収録**：日本郵便の「住所の郵便番号（大阪府分 CSV）」などから町域名を取り込めば、堺市・その他の市町村でも町名を一覧から選べるようになります（CSV の町域は丁目単位でないなど表記の癖があるため、変換処理の検討が必要）。
- **v0.6 の次の段階**：データの読み込み元をインターフェースで抽象化し（JSON 実装と DB 実装を設定で切り替え）、EF Core（Npgsql）で DB から読み込む。単体テストは JSON 実装のまま DB なしで実行できるようにする。
- **v0.7**：住所を正規化する Web API と OpenAPI。同じ API を Spring Boot でも作り、同じ DB を共有して比較する。
