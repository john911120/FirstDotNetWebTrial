using Microsoft.EntityFrameworkCore;

namespace OsakaAddressMvc.Infrastructure.Database;

// v0.6.0: 住所データの DB（PostgreSQL）にアクセスするための EF Core の DbContext。
// テーブルは DbUp（db/migration/*.sql）が作成する。EF Core のマイグレーション機能は使わず、
// EnsureCreated() や Database.Migrate() も呼ばない（スキーマの管理は DbUp に一本化する）。
// 読み取り専用で使うため、DbSet ではなく IQueryable を公開している。
public sealed class AddressDbContext(DbContextOptions<AddressDbContext> options) : DbContext(options)
{
    public IQueryable<PrefectureRow> Prefectures => Set<PrefectureRow>().AsNoTracking();
    public IQueryable<MunicipalityRow> Municipalities => Set<MunicipalityRow>().AsNoTracking();
    public IQueryable<WardRow> Wards => Set<WardRow>().AsNoTracking();
    public IQueryable<TownRow> Towns => Set<TownRow>().AsNoTracking();
    public IQueryable<ChomeRow> Chomes => Set<ChomeRow>().AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // テーブル名・列名・主キーは各 Row クラスの属性（[Table]・[Column]・[PrimaryKey]）で指定している。
        modelBuilder.Entity<PrefectureRow>();
        modelBuilder.Entity<MunicipalityRow>();
        modelBuilder.Entity<WardRow>();
        modelBuilder.Entity<TownRow>();
        modelBuilder.Entity<ChomeRow>();
    }
}
