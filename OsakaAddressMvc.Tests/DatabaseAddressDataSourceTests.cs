using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using OsakaAddressMvc.Infrastructure;
using OsakaAddressMvc.Infrastructure.Database;
using OsakaAddressMvc.Services;
using Xunit.Abstractions;

namespace OsakaAddressMvc.Tests;

// ============================================================================
// v0.6.0: DB（EF Core）から読み込んだ住所データが、元の JSON と完全に一致することを確認する（手動実行）。
// ----------------------------------------------------------------------------
// 「DbUp で JSON から作った初期データを登録 → EF Core で読み込む → 元の JSON と比較」を一度に検証する。
// 環境変数 OSAKA_MIGRATION_TEST_DB（テスト専用の DB の接続文字列）を設定したときだけ実行する。
//   $env:OSAKA_MIGRATION_TEST_DB = "Host=localhost;Port=5434;Database=oamvc_test;Username=oamvc_user;Password=..."
//   dotnet test --filter "FullyQualifiedName~DatabaseAddressDataSourceTests" --logger "console;verbosity=detailed"
//   Remove-Item Env:OSAKA_MIGRATION_TEST_DB
// ============================================================================
[Collection(DatabaseCollection.Name)]   // DB を使うテストは並列に実行しない
public sealed class DatabaseAddressDataSourceTests(ITestOutputHelper output)
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(DatabaseFactAttribute.MigrationEnvironmentVariable);

    [DatabaseFact(DatabaseFactAttribute.MigrationEnvironmentVariable)]
    [Trait("Category", "Integration")]
    public void DBから読み込んだデータはJSONと完全に一致する()
    {
        var migration = DatabaseMigrator.Migrate(ConnectionString!, NullLogger.Instance);
        Assert.True(migration.Successful, migration.Error?.Message);

        // アプリと同じく、DbContext はファクトリーから作る。
        var options = new DbContextOptionsBuilder<AddressDbContext>().UseNpgsql(ConnectionString!).Options;
        var fromDb = new AddressDataProvider(
            new DatabaseAddressDataSource(new PooledDbContextFactory<AddressDbContext>(options)));
        var fromJson = TestServices.CreateDataProvider();

        // 画面で使う72地域（AreaInfo は record のため、全項目が等しいかで比較される）
        Assert.Equal(fromJson.Areas, fromDb.Areas);

        // 市町村一覧（43件・行政区を含む）と、大阪市24区の町名・丁目を JSON に変換して丸ごと比較する
        Assert.Equal(ToJson(fromJson.Prefecture), ToJson(fromDb.Prefecture));
        Assert.Equal(fromJson.Wards.Keys.Order(), fromDb.Wards.Keys.Order());
        foreach (var (ward, data) in fromJson.Wards)
            Assert.Equal(ToJson(data), ToJson(fromDb.Wards[ward]));

        output.WriteLine($"地域 {fromDb.Areas.Count} 件・区 {fromDb.Wards.Count} 件が JSON と一致しました。");
    }

    private static string ToJson<T>(T value) => JsonSerializer.Serialize(value);
}
