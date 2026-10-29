using System.Text.Json;
using Microsoft.Extensions.Options;
using OsakaAddressMvc.Models;

namespace OsakaAddressMvc.Services;

/*
 v0.6.0-alpha.2: Data/Municipalities.json と Data/Wards/*.json から住所データを読み込む。
 ここではファイルの有無と JSON の形式だけを確認し、内容の検証は AddressDataProvider に任せる。
*/
public sealed class JsonAddressDataSource(IOptions<AddressDataOptions> options) : IAddressDataSource
{
    // Web 既定（プロパティ名の大文字・小文字を区別しない）。ファイルごとに作らず使い回す。
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _dataDirectory = options.Value.DataDirectory;

    public string SourceName => $"JSON ({_dataDirectory})";

    public PrefectureData LoadPrefecture()
    {
        var path = Path.Combine(_dataDirectory, "Municipalities.json");
        if (!File.Exists(path)) 
        {
            throw new InvalidDataException($"市町村データが見つかりません: {path}");
        }
        
        var data = JsonSerializer.Deserialize<PrefectureData>(File.ReadAllText(path), JsonOptions);
        if (data is null || data.Municipalities is null) 
        {
            throw new InvalidDataException($"市町村データの形式が不正です: {path}");
        }
        
        return data;
    }

    public IReadOnlyList<WardData> LoadOsakaCityWards()
    {
        var directory = Path.Combine(_dataDirectory, "Wards");
        if (!Directory.Exists(directory))
        {
            throw new InvalidDataException($"区別町名データのフォルダーが見つかりません: {directory}");
        }

        var wards = new List<WardData>();
        // ファイル名順に読む（OS によって GetFiles の順序が変わらないように）。
        foreach (var path in Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            var data = JsonSerializer.Deserialize<WardData>(File.ReadAllText(path), JsonOptions);
            if (data is null || data.Towns is null)
            {
                throw new InvalidDataException($"区データの形式が不正です: {path}");
            }

            // v0.6.0-alpha.2 修正：以前は throw の直後（if の中）にあり、到達しないため常に 0 件になっていた。
            wards.Add(data);
        }
        return wards;
    }
}
