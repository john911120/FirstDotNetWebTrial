using Microsoft.EntityFrameworkCore;
using OsakaAddressMvc.Infrastructure.Database;
using OsakaAddressMvc.Models;

namespace OsakaAddressMvc.Services;

/*
 v0.6.0: PostgreSQL（DbUp で作成したテーブル）から住所データを読み込む。
 JsonAddressDataSource と同じ形（PrefectureData・WardData）に詰め替えて返すため、
 AddressDataProvider・AddressService・画面は、データ元が JSON か DB かを意識しない。

 AddressDataProvider は Singleton、DbContext は Scoped（短命）のため、DbContext を直接受け取らず
 IDbContextFactory から必要なときだけ作成し、読み終わったらすぐに破棄する（DI の寿命の不一致を避ける）。
*/
public sealed class DatabaseAddressDataSource(IDbContextFactory<AddressDbContext> contextFactory) : IAddressDataSource
{
    // AddressDataProvider は大阪府のデータを前提に検証する。
    private const string PrefectureCode = "27";

    public string SourceName => "PostgreSQL（ConnectionStrings:OAMVC）";

    public PrefectureData LoadPrefecture()
    {
        using var db = contextFactory.CreateDbContext();

        var prefecture = db.Prefectures.SingleOrDefault(p => p.Code == PrefectureCode)
            ?? throw new InvalidDataException($"都道府県データ（コード {PrefectureCode}）がありません（データ元：{SourceName}）。");

        // 行政区を市町村コードごとにまとめる（ToLookup はキーごとの一覧。キーがない場合は空になる）。
        var wardsByMunicipality = db.Wards
            .OrderBy(w => w.MunicipalityCode).ThenBy(w => w.SortOrder)
            .ToList()
            .ToLookup(w => w.MunicipalityCode);

        var municipalities = db.Municipalities
            .OrderBy(m => m.SortOrder)
            .ToList()
            .Select(m => new MunicipalityData
            {
                Code = m.Code,
                Name = m.Name,
                Roman = m.Roman,
                Kind = m.Kind,
                County = m.County,
                HasTownData = m.HasTownData,
                Wards = wardsByMunicipality[m.Code]
                    .Select(w => new WardEntry { Code = w.Code, Name = w.Name, Roman = w.Roman })
                    .ToList()
            })
            .ToList();

        return new PrefectureData
        {
            Prefecture = prefecture.Name,
            PrefectureCode = prefecture.Code,
            SourceUrl = prefecture.SourceUrl,
            Municipalities = municipalities
        };
    }

    public IReadOnlyList<WardData> LoadOsakaCityWards()
    {
        using var db = contextFactory.CreateDbContext();

        // 町名データを持つ市（大阪市）の区。LINQ のクエリ構文で書いた join は SQL の INNER JOIN に変換される。
        var wards = (from w in db.Wards
                     join m in db.Municipalities on w.MunicipalityCode equals m.Code
                     where m.HasTownData
                     orderby w.SortOrder
                     select w).ToList();

        // 町名は区コードごと、丁目は（区コード, 町名）ごとにまとめる。並び順は sort_order のまま保たれる。
        var townsByWard = db.Towns
            .OrderBy(t => t.WardCode).ThenBy(t => t.SortOrder)
            .ToList()
            .ToLookup(t => t.WardCode);
        var chomesByTown = db.Chomes
            .OrderBy(c => c.WardCode).ThenBy(c => c.TownName).ThenBy(c => c.SortOrder)
            .ToList()
            .ToLookup(c => (c.WardCode, c.TownName));

        return wards
            .Select(w => new WardData
            {
                Ward = w.Name,
                SourceUrl = w.TownSourceUrl ?? "",
                Towns = townsByWard[w.Code]
                    .Select(t => new TownData
                    {
                        Name = t.Name,
                        AllowWithoutChome = t.AllowWithoutChome,
                        Chomes = chomesByTown[(t.WardCode, t.Name)].Select(c => c.Name).ToList()
                    })
                    .ToList()
            })
            .ToList();
    }
}
