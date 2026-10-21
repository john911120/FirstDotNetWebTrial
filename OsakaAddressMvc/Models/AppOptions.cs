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
