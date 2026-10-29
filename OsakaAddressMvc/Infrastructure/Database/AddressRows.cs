using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OsakaAddressMvc.Infrastructure.Database;

// v0.6.0: DB のテーブル1行に対応するクラス（EF Core のエンティティ）。
// 画面やサービスで使う Models（PrefectureData・WardData など）とは分け、DatabaseAddressDataSource で詰め替える。
// PostgreSQL の名前は小文字の snake_case のため、[Table]・[Column] で C# の名前と対応付ける。

[Table("prefectures")]
[PrimaryKey(nameof(Code))]
public sealed class PrefectureRow
{
    [Column("code")] public string Code { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";
    [Column("source_url")] public string SourceUrl { get; set; } = "";
}

[Table("municipalities")]
[PrimaryKey(nameof(Code))]
public sealed class MunicipalityRow
{
    [Column("code")] public string Code { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";
    [Column("roman")] public string Roman { get; set; } = "";
    [Column("kind")] public string Kind { get; set; } = "";
    [Column("county")] public string? County { get; set; }
    [Column("has_town_data")] public bool HasTownData { get; set; }
    [Column("sort_order")] public int SortOrder { get; set; }
}

[Table("wards")]
[PrimaryKey(nameof(Code))]
public sealed class WardRow
{
    [Column("code")] public string Code { get; set; } = "";
    [Column("municipality_code")] public string MunicipalityCode { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";
    [Column("roman")] public string Roman { get; set; } = "";
    [Column("town_source_url")] public string? TownSourceUrl { get; set; }
    [Column("sort_order")] public int SortOrder { get; set; }
}

// 主キーが複数列（区コード + 町名）のため、[PrimaryKey] に両方を指定する（EF Core 7 以降の属性）。
[Table("towns")]
[PrimaryKey(nameof(WardCode), nameof(Name))]
public sealed class TownRow
{
    [Column("ward_code")] public string WardCode { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";
    [Column("allow_without_chome")] public bool AllowWithoutChome { get; set; }
    [Column("sort_order")] public int SortOrder { get; set; }
}

[Table("chomes")]
[PrimaryKey(nameof(WardCode), nameof(TownName), nameof(Name))]
public sealed class ChomeRow
{
    [Column("ward_code")] public string WardCode { get; set; } = "";
    [Column("town_name")] public string TownName { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";
    [Column("sort_order")] public int SortOrder { get; set; }
}
