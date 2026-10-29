using Microsoft.Extensions.Options;
using OsakaAddressMvc.Models;
using OsakaAddressMvc.Services;

namespace OsakaAddressMvc.Tests;

// v0.6.0: AddressDataProvider の検証を、JSON ファイルを書き換えずに確認する。
// 実際の JSON を読み込んだあとで一部を書き換える「偽のデータ元」を渡す（IAddressDataSource による抽象化の効果）。
public sealed class AddressDataProviderTests
{
    private static readonly IAddressDataSource JsonSource = new JsonAddressDataSource(
        Options.Create(new AddressDataOptions { DataDirectory = TestServices.DataDirectory }));

    [Fact]
    public void 正しいデータなら72地域を作成する()
    {
        Assert.Equal(72, new AddressDataProvider(JsonSource).Areas.Count);
    }

    [Fact]
    public void 市町村が43件でなければ例外になる()
    {
        var source = new EditedSource(JsonSource)
        {
            EditPrefecture = p => p.Municipalities.RemoveAt(p.Municipalities.Count - 1)
        };

        var ex = Assert.Throws<InvalidDataException>(() => new AddressDataProvider(source));
        Assert.Contains("43件必要", ex.Message);
    }

    [Fact]
    public void 大阪市の区データが24区でなければ例外になる()
    {
        var source = new EditedSource(JsonSource) { EditWards = wards => wards.RemoveAt(0) };

        var ex = Assert.Throws<InvalidDataException>(() => new AddressDataProvider(source));
        Assert.Contains("24区分必要", ex.Message);
    }

    [Fact]
    public void 区名が重複していれば例外になりデータ元の名前を表示する()
    {
        var source = new EditedSource(JsonSource) { EditWards = wards => wards.Add(wards[0]) };

        var ex = Assert.Throws<InvalidDataException>(() => new AddressDataProvider(source));
        Assert.Contains("区名が重複しています", ex.Message);
        Assert.Contains(EditedSource.Name, ex.Message);
    }

    // 本物のデータ元から読み込んだデータを、指定した処理で書き換えて返す偽のデータ元。
    private sealed class EditedSource(IAddressDataSource inner) : IAddressDataSource
    {
        public const string Name = "テスト用に書き換えたデータ";

        public Action<PrefectureData>? EditPrefecture { get; init; }
        public Action<List<WardData>>? EditWards { get; init; }

        public string SourceName => Name;

        public PrefectureData LoadPrefecture()
        {
            var data = inner.LoadPrefecture();
            EditPrefecture?.Invoke(data);
            return data;
        }

        public IReadOnlyList<WardData> LoadOsakaCityWards()
        {
            var wards = inner.LoadOsakaCityWards().ToList();
            EditWards?.Invoke(wards);
            return wards;
        }
    }
}
