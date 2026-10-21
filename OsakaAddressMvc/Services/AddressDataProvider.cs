using System.Text.Json;
using Microsoft.Extensions.Options;
using OsakaAddressMvc.Models;

namespace OsakaAddressMvc.Services;

// 市町村一覧（Data/Municipalities.json）と大阪市の区別町名データ（Data/Wards/）を起動時に一度だけ読み込む。
// データを修正した場合はアプリを再起動すること。
public sealed class AddressDataProvider
{
    public const int ExpectedMunicipalityCount = 43;
    public const int ExpectedOsakaCityWardCount = 24;

    // Web 既定（プロパティ名の大文字・小文字を区別しない）。ファイルごとに作らず使い回す。
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyDictionary<string, WardData> _wards;
    private readonly IReadOnlyDictionary<string, AreaInfo> _areaByKey;

    // v0.5.5: IWebHostEnvironment 全体ではなく、必要なデータフォルダーの場所だけを受け取る。
    // テストでは Options.Create(...) で任意のフォルダーを渡せる。
    public AddressDataProvider(IOptions<AddressDataOptions> options)
    {
        var dataDirectory = options.Value.DataDirectory;
        _wards = LoadOsakaCityWards(Path.Combine(dataDirectory, "Wards"));
        Prefecture = LoadPrefecture(Path.Combine(dataDirectory, "Municipalities.json"));
        Areas = BuildAreas(Prefecture, _wards);
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

    private static Dictionary<string, WardData> LoadOsakaCityWards(string directory)
    {
        var loaded = new Dictionary<string, WardData>(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(directory, "*.json"))
        {
            var data = JsonSerializer.Deserialize<WardData>(File.ReadAllText(path), JsonOptions);
            if (data is null || string.IsNullOrWhiteSpace(data.Ward) || data.Towns is null)
                throw new InvalidDataException($"区データの形式が不正です: {path}");
            if (!loaded.TryAdd(data.Ward, data))
                throw new InvalidDataException($"区名が重複しています: {data.Ward}");
            if (data.Towns.GroupBy(t => t.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new InvalidDataException($"町名が重複しています: {data.Ward}");
        }
        if (loaded.Count != ExpectedOsakaCityWardCount)
            throw new InvalidDataException(
                $"大阪市の区データは{ExpectedOsakaCityWardCount}ファイル必要です。現在 {loaded.Count} 件です。");
        return loaded;
    }

    private static PrefectureData LoadPrefecture(string path)
    {
        if (!File.Exists(path))
            throw new InvalidDataException($"市町村データが見つかりません: {path}");
        var data = JsonSerializer.Deserialize<PrefectureData>(File.ReadAllText(path), JsonOptions);
        if (data is null || data.Prefecture != "大阪府" || data.Municipalities is null)
            throw new InvalidDataException($"市町村データの形式が不正です: {path}");
        if (data.Municipalities.Count != ExpectedMunicipalityCount)
            throw new InvalidDataException(
                $"大阪府の市町村は{ExpectedMunicipalityCount}件必要です。現在 {data.Municipalities.Count} 件です。");
        if (data.Municipalities.GroupBy(m => m.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new InvalidDataException("市町村名が重複しています。");
        foreach (var m in data.Municipalities)
        {
            if (string.IsNullOrWhiteSpace(m.Name) || m.Code.Length != 5 || !m.Code.All(char.IsAsciiDigit))
                throw new InvalidDataException($"市町村の名前またはコードが不正です: {m.Name}");
            if (m.Wards.GroupBy(w => w.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new InvalidDataException($"区名が重複しています: {m.Name}");
        }
        return data;
    }

    private static AreaInfo[] BuildAreas(PrefectureData prefecture, IReadOnlyDictionary<string, WardData> wards)
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

        // 大阪市の区一覧と Data/Wards/ のファイルが過不足なく一致することを確認する。
        var osakaWards = areas.Where(a => a.HasTownData).Select(a => a.Ward!).ToHashSet(StringComparer.Ordinal);
        if (!osakaWards.SetEquals(wards.Keys))
            throw new InvalidDataException("Municipalities.json の大阪市の区と Data/Wards/ のファイルが一致しません。");

        // 表示順：大阪市の区 → 堺市の区 → 市 → 町村（各グループ内は JSON の記載順）。
        return areas
            .OrderBy(a => a.Municipality == "大阪市" ? 0 : a.Ward is not null ? 1 : a.Kind == "市" ? 2 : 3)
            .ToArray();
    }
}
