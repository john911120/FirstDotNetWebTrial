using OsakaAddressMvc;
using OsakaAddressMvc.Services;

namespace OsakaAddressMvc.Tests;

// v0.5: 市町村一覧と地域（区・市町村）の構成を確認する。
public sealed class AreaDataTests
{
    private readonly AddressService _service = TestServices.CreateAddressService();

    [Fact]
    public void 大阪府の地域は72件読み込まれる()
    {
        // 大阪市24区 + 堺市7区 + 31市 + 9町1村 = 72地域
        var areas = _service.SearchAreas(null);
        Assert.Equal(72, areas.Count);
        Assert.Equal(24, areas.Count(a => a.Municipality == "大阪市"));
        Assert.Equal(7, areas.Count(a => a.Municipality == "堺市"));
        Assert.Equal(31, areas.Count(a => a.Ward is null && a.Kind == "市"));
        Assert.Equal(10, areas.Count(a => a.Kind is "町" or "村"));
        Assert.Equal(43, areas.Select(a => a.Municipality).Distinct().Count());
    }

    [Fact]
    public void 町名データを持つのは大阪市の24区のみ()
    {
        var withTowns = _service.SearchAreas(null).Where(a => a.HasTownData).ToArray();
        Assert.Equal(24, withTowns.Length);
        Assert.All(withTowns, a => Assert.Equal("大阪市", a.Municipality));
    }

    [Fact]
    public void 大阪市北区と堺市北区は別の地域として扱う()
    {
        var osaka = _service.ResolveArea("大阪市北区");
        var sakai = _service.ResolveArea("堺市北区");
        Assert.NotNull(osaka);
        Assert.NotNull(sakai);
        Assert.NotEqual(osaka.Code, sakai.Code);
        Assert.True(osaka.HasTownData);
        Assert.False(sakai.HasTownData);
    }

    [Theory]
    [InlineData("島本町", "三島郡島本町", "大阪府三島郡島本町", "27301")]
    [InlineData("堺市堺区", "堺市堺区", "大阪府堺市堺区", "27141")]
    [InlineData("大阪市中央区", "大阪市中央区", "大阪府大阪市中央区", "27128")]
    [InlineData("大阪狭山市", "大阪狭山市", "大阪府大阪狭山市", "27231")]
    [InlineData("河南町", "南河内郡河南町", "大阪府南河内郡河南町", "27382")]
    public void 郵便番号照合用の市区町村名とコード(string key, string postalCity, string prefix, string code)
    {
        var area = _service.ResolveArea(key);
        Assert.NotNull(area);
        Assert.Equal(postalCity, area.PostalCityName);
        Assert.Equal(prefix, area.AddressPrefix);
        Assert.Equal(code, area.Code);
    }

    [Theory]
    [InlineData("Toyonaka", "豊中市")]
    [InlineData("suita-shi", "吹田市")]
    [InlineData("三島郡", "島本町")]
    [InlineData("Higashiyodogawa", "大阪市東淀川区")]
    [InlineData("Sakai-shi Mihara", "堺市美原区")]
    public void 地域はローマ字や郡名でも検索できる(string keyword, string expectedKey)
    {
        Assert.Contains(_service.SearchAreas(keyword), a => a.Key == expectedKey);
    }

    [Fact]
    public void 堺で検索すると堺市の7区が見つかる()
    {
        Assert.Equal(7, _service.SearchAreas("堺市").Count);
    }

    // v0.5.5: 期待値を AppInfo.Version から取得する。バージョンを上げてもテストの修正は不要。
    [Fact]
    public void 住所JSONのバージョンはAppInfoと一致する()
    {
        var built = _service.TryBuild("吹田市", "千里山西", "5");
        Assert.NotNull(built);
        var json = System.Text.Json.JsonSerializer.Serialize(_service.GetAddressJson(built));
        Assert.Contains($"\"version\":\"{AppInfo.Version}\"", json);
        Assert.Contains("\"city\":", json);
    }
}
