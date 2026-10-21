using OsakaAddressMvc.Services;

namespace OsakaAddressMvc.Tests;

// TEST_PLAN.md の手動テストのうち、AddressService.BuildAddress で確認できるものを自動化する。
// コメントの No. は TEST_PLAN.md の番号に対応する。
public sealed class BuildAddressTests
{
    private readonly AddressService _service = TestServices.CreateAddressService();

    [Theory]
    [InlineData("東淀川区", "大桐", "2丁目", null, null, "大阪府大阪市東淀川区大桐2丁目")]      // No.3
    [InlineData("中央区", "上町", null, null, null, "大阪府大阪市中央区上町")]                // No.6 丁目なしも可
    [InlineData("中央区", "上町", "1丁目", null, null, "大阪府大阪市中央区上町1丁目")]        // No.7
    [InlineData("北区", "角田町", null, "5", "15", "大阪府大阪市北区角田町5-15")]            // No.16
    [InlineData("北区", "角田町", null, "５", "１５", "大阪府大阪市北区角田町5-15")]          // No.18 全角→半角
    [InlineData("東淀川区", "大桐", "2丁目", "3", "4", "大阪府大阪市東淀川区大桐2丁目3-4")]   // No.20
    [InlineData("北区", "角田町", null, "5", null, "大阪府大阪市北区角田町5")]               // 番のみは可
    public void 正しい入力なら住所を組み立てる(
        string ward, string town, string? chome, string? ban, string? go, string expected)
    {
        Assert.Equal(expected, _service.BuildAddress(ward, town, chome, ban, go));
    }

    [Theory]
    [InlineData("北区", "角田町", null, null, "15")]        // No.19 号のみ
    [InlineData("北区", "角田町", null, "abc", null)]       // No.21 数字以外
    [InlineData("北区", "角田町", null, "1234567", null)]   // No.21 7桁
    [InlineData("北区", "角田町", null, "05", null)]        // 先頭が0
    [InlineData("東淀川区", "大桐", null, null, null)]      // 丁目が必須の町で丁目なし
    [InlineData("天王寺区", "上汐", "1丁目", null, null)]   // 登録されていない丁目（上汐は3～6丁目）
    [InlineData("北区", "存在しない町", null, null, null)]  // 存在しない町名
    [InlineData("存在しない区", "角田町", null, null, null)] // 存在しない区名
    public void 不正な入力ならnullを返す(
        string ward, string town, string? chome, string? ban, string? go)
    {
        Assert.Null(_service.BuildAddress(ward, town, chome, ban, go));
    }

    // 24区・72地域の件数確認は AreaDataTests にまとめた（v0.5.5 で重複を整理）。

    // ---- v0.5 の地域対応（TEST_PLAN G-1 に記載していたテストを v0.5.5 で実装） ----

    [Theory]
    [InlineData("北区", "大阪市北区")]
    [InlineData("東淀川区", "大阪市東淀川区")]
    public void 区名のみの指定は大阪市の区として扱う(string input, string expectedKey)
    {
        Assert.Equal(expectedKey, _service.ResolveArea(input)?.Key);
    }

    [Theory]
    [InlineData("堺区")]     // 堺市にのみある区
    [InlineData("美原区")]
    public void 区名のみで大阪市にない区は解決しない(string input)
    {
        Assert.Null(_service.ResolveArea(input));
    }

    [Theory]
    [InlineData("堺市堺区", "大仙町", null, null, null, "大阪府堺市堺区大仙町")]                       // No.31
    [InlineData("堺市北区", "東三国ヶ丘町", "3", "1", "2", "大阪府堺市北区東三国ヶ丘町3丁目1-2")]      // No.32
    [InlineData("吹田市", "千里山西", "５", null, null, "大阪府吹田市千里山西5丁目")]                  // No.33 全角
    [InlineData("島本町", "桜井", "2", null, null, "大阪府三島郡島本町桜井2丁目")]                     // No.34 郡名
    public void 大阪市以外の地域でも住所を組み立てる(
        string area, string town, string? chome, string? ban, string? go, string expected)
    {
        Assert.Equal(expected, _service.BuildAddress(area, town, chome, ban, go));
    }

    [Theory]
    [InlineData("吹田市", "千里山西5丁目", null)]   // No.35 町名に丁目を含む
    [InlineData("吹田市", "千里山西1", null)]       // 町名に数字
    [InlineData("吹田市", "Senriyama", null)]       // 英字
    [InlineData("吹田市", "千里山西!", null)]       // 記号
    [InlineData("吹田市", "千里山西", "0")]         // 丁目が0
    [InlineData("吹田市", "千里山西", "100")]       // 丁目が100以上
    [InlineData("吹田市", "千里山西", "三")]        // 丁目が漢数字
    [InlineData("吹田市", "", null)]                // 町名が空
    public void 大阪市以外の地域で不正な入力ならnullを返す(string area, string town, string? chome)
    {
        Assert.Null(_service.BuildAddress(area, town, chome));
    }

    [Fact]
    public void 町名が30文字を超えるとnullを返す()
    {
        Assert.NotNull(_service.BuildAddress("吹田市", new string('山', AddressService.MaxTownNameLength), null));
        Assert.Null(_service.BuildAddress("吹田市", new string('山', AddressService.MaxTownNameLength + 1), null));
    }

    [Fact]
    public void 大阪市の区は町名データ外の町名を拒否する()
    {
        // 大阪市の区は収録済みの町名だけを受け付ける（形式が正しくても一覧にない町名は不可）。
        Assert.Null(_service.BuildAddress("大阪市北区", "千里山西", null));
    }
}
