using System.Text;
using OsakaAddressMvc.Models;

namespace OsakaAddressMvc.Services;

// 組み立て済みの住所。表示用の文字列と、JSON 出力用に正規化した各要素を持つ。
public sealed record BuiltAddress(
    AreaInfo Area, string Town, string? Chome, string? Ban, string? Go, string Formatted);

// v0.5: 対象を大阪市24区から大阪府全域（43市町村・72地域）に拡張した。
// 大阪市の区は収録済みの町名・丁目から選択し、それ以外の地域は町名を入力して形式のみ検査する。
public sealed class AddressService(AddressDataProvider provider)
{
    public const int MaxTownNameLength = 30;
    public const int MaxChomeNumber = 99;

    // v0.1 の Where / Select / Distinct を引き継ぎ、検索対象を「地域」に広げた。
    public IReadOnlyList<AreaInfo> SearchAreas(string? keyword)
    {
        var term = keyword?.Trim() ?? "";
        return provider.Areas
            .Where(a => term.Length == 0 ||
                a.Key.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (a.County?.Contains(term, StringComparison.Ordinal) ?? false) ||
                a.Roman.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.MunicipalityRoman.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                RomanLabel(a).Contains(term, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();
    }

    // ローマ字の検索用表記（例：Osaka-shi Kita-ku、Toyonaka-shi、Shimamoto-cho）。
    private static string RomanLabel(AreaInfo a)
    {
        var suffix = a.Kind switch { "町" => "-cho", "村" => "-mura", _ => "-shi" };
        return a.Ward is null ? $"{a.Roman}{suffix}" : $"{a.MunicipalityRoman}-shi {a.Roman}-ku";
    }

    // 地域キーを解決する。v0.4 までの URL（ward=北区 など区名のみ）は大阪市の区として扱う。
    public AreaInfo? ResolveArea(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var key = input.Trim();
        return provider.GetArea(key) ??
            (key.EndsWith('区') ? provider.GetArea($"大阪市{key}") : null);
    }

    public bool IsKnownArea(string? area) => ResolveArea(area) is not null;

    public bool HasTownData(string? area) => ResolveArea(area)?.HasTownData == true;

    public IReadOnlyList<string> GetTowns(string? area) =>
        provider.GetTownData(ResolveArea(area))?.Towns
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .Select(t => t.Name)
            .Distinct()
            .ToArray() ?? [];

    public IReadOnlyList<string> GetChomes(string? area, string? town) =>
        FindTown(ResolveArea(area), town)?.Chomes.ToArray() ?? [];

    public bool CanOmitChome(string? area, string? town) =>
        FindTown(ResolveArea(area), town)?.AllowWithoutChome == true;

    private TownData? FindTown(AreaInfo? area, string? town) =>
        provider.GetTownData(area)?.Towns.FirstOrDefault(t => t.Name == town);

    // 丁目と番・号は別の概念。例：大阪市北区角田町5-15 は丁目なし、番=5、号=15。
    public string? BuildAddress(string? area, string? town, string? chome,
        string? ban = null, string? go = null) =>
        TryBuild(area, town, chome, ban, go)?.Formatted;

    public BuiltAddress? TryBuild(string? areaKey, string? town, string? chome,
        string? ban = null, string? go = null)
    {
        var area = ResolveArea(areaKey);
        if (area is null) return null;

        string normalizedTown;
        string? normalizedChome;
        if (area.HasTownData)
        {
            // 町名データのある地域（大阪市の区）では、登録された町名・丁目だけを許可する。
            var townData = FindTown(area, town);
            if (townData is null) return null;
            normalizedTown = townData.Name;
            if (string.IsNullOrWhiteSpace(chome))
            {
                if (townData.Chomes.Count > 0 && !townData.AllowWithoutChome) return null;
                normalizedChome = null;
            }
            else if (townData.Chomes.Contains(chome)) normalizedChome = chome;
            else return null;
        }
        else
        {
            // 町名データのない地域では、町名と丁目の形式のみを検査する（実在性は確認しない）。
            if (!TryNormalizeTownName(town, out var freeTown) ||
                !TryNormalizeChome(chome, out normalizedChome)) return null;
            normalizedTown = freeTown;
        }

        // 番・号が数字のみで構成されているかを検査し、全角数字は半角に統一する。
        // 実在する住居表示番号かどうかは照合しない。
        if (!TryNormalizeNumber(ban, 6, out var normalizedBan) ||
            !TryNormalizeNumber(go, 6, out var normalizedGo)) return null;
        if (normalizedBan is null && normalizedGo is not null) return null;

        var suffix = normalizedBan is null ? "" :
            normalizedGo is null ? normalizedBan : $"{normalizedBan}-{normalizedGo}";
        return new BuiltAddress(area, normalizedTown, normalizedChome, normalizedBan, normalizedGo,
            $"{area.AddressPrefix}{normalizedTown}{normalizedChome}{suffix}");
    }

    // 入力された町名を正規化して検査する。漢字・ひらがな・カタカナ（々・ヶ・ー などを含む）のみ許可し、
    // 数字・記号・空白、「丁目」で終わる入力は拒否する（丁目は専用の欄で指定する）。
    public static bool TryNormalizeTownName(string? input, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        var name = input.Trim().Normalize(NormalizationForm.FormKC);
        if (name.Length > MaxTownNameLength || name.EndsWith("丁目", StringComparison.Ordinal)) return false;
        foreach (var rune in name.EnumerateRunes())
            if (!IsJapaneseNameRune(rune.Value)) return false;
        normalized = name;
        return true;
    }

    private static bool IsJapaneseNameRune(int c) =>
        c is >= 0x3041 and <= 0x309F      // ひらがな
          or >= 0x30A1 and <= 0x30FA      // カタカナ（ヶ・ヵ を含む）
          or 0x30FC                        // 長音符 ー
          or 0x3005 or 0x3006             // 々 〆
          or >= 0x3400 and <= 0x4DBF      // CJK 統合漢字拡張 A
          or >= 0x4E00 and <= 0x9FFF      // CJK 統合漢字
          or >= 0xF900 and <= 0xFAFF      // CJK 互換漢字
          or >= 0x20000 and <= 0x3FFFF;   // CJK 統合漢字拡張 B 以降

    // 丁目を「n丁目」の形に統一する。「3」「３」「3丁目」はいずれも「3丁目」になる。空欄は丁目なし。
    public static bool TryNormalizeChome(string? input, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(input)) return true;
        var value = input.Trim().Normalize(NormalizationForm.FormKC);
        if (value.EndsWith("丁目", StringComparison.Ordinal)) value = value[..^2];
        if (!TryNormalizeNumber(value, 2, out var number) || number is null ||
            int.Parse(number) > MaxChomeNumber) return false;
        normalized = $"{number}丁目";
        return true;
    }

    private static bool TryNormalizeNumber(string? input, int maxLength, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(input)) return true;
        var number = input.Trim().Normalize(NormalizationForm.FormKC);
        if (number.Length < 1 || number.Length > maxLength || number[0] == '0' ||
            !number.All(char.IsAsciiDigit)) return false;
        normalized = number;
        return true;
    }

    // 選択した住所1件の JSON。v0.5 で都道府県以下の構成（郡・市町村・区）を明示する形式に変更した。
    public object GetAddressJson(BuiltAddress address) => new
    {
        prefecture = "大阪府",
        county = address.Area.County,
        city = address.Area.Municipality,
        ward = address.Area.Ward,
        town = address.Town,
        chome = address.Chome,
        ban = address.Ban,
        go = address.Go,
        formattedAddress = address.Formatted,
        localGovernmentCode = address.Area.Code,
        townSource = address.Area.HasTownData ? "大阪市 区別町名一覧（収録データから選択）" : "利用者入力（形式のみ検査）",
        version = AppInfo.Version
    };

    // 全データの JSON。府内の市町村・区の一覧と、大阪市24区の町名・丁目データを含む。
    public object GetDatasetJson() => new
    {
        prefecture = provider.Prefecture.Prefecture,
        version = AppInfo.Version,
        municipalitiesSource = provider.Prefecture.SourceUrl,
        townDataSource = "大阪市 区別町名一覧",
        municipalities = provider.Prefecture.Municipalities,
        osakaCityWards = provider.Areas
            .Where(a => a.HasTownData)
            .Select(a => provider.GetTownData(a)!)
            .ToArray()
    };
    
    
}
