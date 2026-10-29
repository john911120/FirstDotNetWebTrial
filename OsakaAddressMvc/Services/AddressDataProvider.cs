using OsakaAddressMvc.Models;

namespace OsakaAddressMvc.Services;

// 住所データを起動時に一度だけ読み込み、検証して、画面で使う「地域」の一覧を作る。
// v0.6.0-alpha.2: 読み込み元（JSON・DB）を IAddressDataSource に分離した。
// ここに残しているのは、データ元に関係なく必要な検証（件数・重複・整合性）だけ。
// データを修正した場合はアプリを再起動すること。
public sealed class AddressDataProvider
{
    public const int ExpectedMunicipalityCount = 43;
    public const int ExpectedOsakaCityWardCount = 24;

    private readonly IReadOnlyDictionary<string, WardData> _wards;
    private readonly IReadOnlyDictionary<string, AreaInfo> _areaByKey;

    public AddressDataProvider(IAddressDataSource source)
    {
        _wards = ValidateOsakaCityWards(source.LoadOsakaCityWards(), source.SourceName);
        Prefecture = ValidatePrefecture(source.LoadPrefecture(), source.SourceName);
        Areas = BuildAreas(Prefecture, _wards, source.SourceName);
        _areaByKey = Areas.ToDictionary(a => a.Key, StringComparer.Ordinal);
    }

    public PrefectureData Prefecture { get; }

    // 表示順（大阪市の区 → 堺市の区 → 市 → 町村）に並んだ全地域。
    public IReadOnlyList<AreaInfo> Areas { get; }

    // 大阪市の区別町名データ（キーは「北区」などの区名）。
    public IReadOnlyDictionary<string, WardData> Wards => _wards;

    public AreaInfo? GetArea(string? key) =>
        key is not null && _areaByKey.TryGetValue(key, out var area) ? area : null;

    // 町名データを持つ地域（大阪市の区）だけ WardData を返す。
    public WardData? GetTownData(AreaInfo? area) =>
        area is { HasTownData: true, Ward: not null } && _wards.TryGetValue(area.Ward, out var data) ? data : null;

    private static Dictionary<string, WardData> ValidateOsakaCityWards(
        IReadOnlyList<WardData> wards, string sourceName)
    {
        var loaded = new Dictionary<string, WardData>(StringComparer.Ordinal);
        foreach (var data in wards)
        {
            if (string.IsNullOrWhiteSpace(data.Ward))
                throw new InvalidDataException($"区名が空の区データがあります（データ元：{sourceName}）。");
            if (!loaded.TryAdd(data.Ward, data))
                throw new InvalidDataException($"区名が重複しています: {data.Ward}（データ元：{sourceName}）");
            if (data.Towns.GroupBy(t => t.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new InvalidDataException($"町名が重複しています: {data.Ward}（データ元：{sourceName}）");
        }
        if (loaded.Count != ExpectedOsakaCityWardCount)
            throw new InvalidDataException(
                $"大阪市の区データは{ExpectedOsakaCityWardCount}区分必要です。現在 {loaded.Count} 件です（データ元：{sourceName}）。");
        return loaded;
    }

    private static PrefectureData ValidatePrefecture(PrefectureData data, string sourceName)
    {
        if (data.Prefecture != "大阪府")
            throw new InvalidDataException($"市町村データの都道府県が「大阪府」ではありません（データ元：{sourceName}）。");
        if (data.Municipalities.Count != ExpectedMunicipalityCount)
            throw new InvalidDataException(
                $"大阪府の市町村は{ExpectedMunicipalityCount}件必要です。現在 {data.Municipalities.Count} 件です（データ元：{sourceName}）。");
        if (data.Municipalities.GroupBy(m => m.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new InvalidDataException($"市町村名が重複しています（データ元：{sourceName}）。");
        foreach (var m in data.Municipalities)
        {
            if (string.IsNullOrWhiteSpace(m.Name) || m.Code.Length != 5 || !m.Code.All(char.IsAsciiDigit))
                throw new InvalidDataException($"市町村の名前またはコードが不正です: {m.Name}（データ元：{sourceName}）");
            if (m.Wards.GroupBy(w => w.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new InvalidDataException($"区名が重複しています: {m.Name}（データ元：{sourceName}）");
        }
        return data;
    }

    private static AreaInfo[] BuildAreas(
        PrefectureData prefecture, IReadOnlyDictionary<string, WardData> wards, string sourceName)
    {
        var areas = new List<AreaInfo>();
        foreach (var m in prefecture.Municipalities)
        {
            string group = m.Wards.Count > 0 ? $"{m.Name}（{m.Wards.Count}区）" : m.Kind == "市" ? "その他の市" : "町村";
            if (m.Wards.Count == 0)
            {
                areas.Add(new AreaInfo(m.Name, m.Code, m.Name, null, m.County, m.Kind, m.Roman, m.Roman, group, false));
                continue;
            }
            foreach (var w in m.Wards)
                areas.Add(new AreaInfo($"{m.Name}{w.Name}", w.Code, m.Name, w.Name, m.County, m.Kind,
                    w.Roman, m.Roman, group, m.HasTownData));
        }

        // 市町村データの大阪市の区と、区別町名データが過不足なく一致することを確認する。
        var osakaWards = areas.Where(a => a.HasTownData).Select(a => a.Ward!).ToHashSet(StringComparer.Ordinal);
        if (!osakaWards.SetEquals(wards.Keys))
            throw new InvalidDataException($"市町村データの大阪市の区と区別町名データが一致しません（データ元：{sourceName}）。");

        // 表示順：大阪市の区 → 堺市の区 → 市 → 町村（各グループ内はデータの記載順）。
        return areas
            .OrderBy(a => a.Municipality == "大阪市" ? 0 : a.Ward is not null ? 1 : a.Kind == "市" ? 2 : 3)
            .ToArray();
    }
}
