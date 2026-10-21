namespace OsakaAddressMvc.Models;

// v0.5: Data/Municipalities.json の形式。大阪府の43市町村（33市・9町・1村）を保持する。
public sealed class PrefectureData
{
    public string Prefecture { get; set; } = "";
    public string PrefectureCode { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public List<MunicipalityData> Municipalities { get; set; } = new();
}

public sealed class MunicipalityData
{
    // 全国地方公共団体コード（5桁、検査数字なし）。ポストくん API の jis 項目と照合する。
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Roman { get; set; } = "";
    // 政令指定都市・市・町・村のいずれか。
    public string Kind { get; set; } = "";
    // 町村の場合の郡名（例：三島郡）。市の場合は null。
    public string? County { get; set; }
    // true の場合、区ごとの町名・丁目データ（Data/Wards/）を使用する。現在は大阪市のみ。
    public bool HasTownData { get; set; }
    // 政令指定都市の行政区。区のない市町村は空配列。
    public List<WardEntry> Wards { get; set; } = new();
}

public sealed class WardEntry
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Roman { get; set; } = "";
}

// 画面で選択する「地域」の単位。政令指定都市は区ごと、それ以外は市町村ごとに1件となる。
// Key は府内で一意な表記（例：大阪市北区・堺市北区・豊中市・島本町）。
public sealed record AreaInfo(
    string Key,
    string Code,
    string Municipality,
    string? Ward,
    string? County,
    string Kind,
    string Roman,
    string MunicipalityRoman,
    string Group,
    bool HasTownData)
{
    // 一覧に表示する短い名前（大阪市・堺市の区は区名のみ、町村は郡名を併記）。
    public string ShortLabel => Ward ?? (County is null ? Municipality : $"{Municipality}（{County}）");

    // 郵便番号データの市区町村表記（例：大阪市北区・堺市堺区・三島郡島本町）。
    public string PostalCityName => $"{County}{Municipality}{Ward}";

    // 住所表記用（大阪府に続く部分）。郡名を含める。
    public string AddressPrefix => $"大阪府{County}{Municipality}{Ward}";
}
