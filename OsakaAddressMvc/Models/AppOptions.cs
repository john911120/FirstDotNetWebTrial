namespace OsakaAddressMvc.Models;

// v0.5.5: 設定値を appsettings.json から読み込む Options クラス（Spring の @ConfigurationProperties に相当）。

// 住所データ（Data/Municipalities.json と Data/Wards/）の置き場所。appsettings.json の "AddressData" セクション。
public sealed class AddressDataOptions
{
    public const string SectionName = "AddressData";

    // 相対パスの場合は、Program.cs でアプリのコンテンツルートを基準に絶対パスへ変換する。
    public string DataDirectory { get; set; } = "Data";
}

// 郵便番号 API（ポストくん）の接続設定。appsettings.json の "PostalCodeApi" セクション。
public sealed class PostalCodeApiOptions
{
    public const string SectionName = "PostalCodeApi";

    public string BaseAddress { get; set; } = "https://postcode.teraren.com/";
    public int TimeoutSeconds { get; set; } = 8;
}

// v0.6: DB マイグレーション（DbUp）の設定。appsettings.json の "Database" セクション。
// 接続文字列は ConnectionStrings:OAMVC から読み込む（開発中は User Secrets に保存し、Git に含めない）。
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "OAMVC";

    // true の場合、アプリ起動時に db/migration/ の未適用スクリプトを実行する。
    // 接続文字列が未設定のときは警告を出してスキップする（DB なしでも従来どおり JSON で動作する）。
    public bool MigrateOnStartup { get; set; }
}
