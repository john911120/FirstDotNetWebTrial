using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OsakaAddressMvc.Infrastructure;
using Xunit.Abstractions;

namespace OsakaAddressMvc.Tests;

// ============================================================================
// v0.6: DB マイグレーションの統合テスト（手動実行）
// ----------------------------------------------------------------------------
// 環境変数 OSAKA_MIGRATION_TEST_DB に接続文字列を設定したときだけ実行する（未設定ならスキップ）。
// ※ 必ず「このテスト専用の空の DB」を指定すること。指定した DB にテーブルとデータを作成する。
//    他のプロジェクトの DB を指定すると、そこに municipalities などのテーブルが作られてしまう。
//
// 実行方法（PowerShell）：
//   $env:OSAKA_MIGRATION_TEST_DB = "Host=localhost;Port=5433;Database=oamvc_test;Username=oamvc_user;Password=..."
//   dotnet test --filter "FullyQualifiedName~DatabaseMigrationTests" --logger "console;verbosity=detailed"
//   Remove-Item Env:OSAKA_MIGRATION_TEST_DB
// ============================================================================
public sealed class DatabaseMigrationTests(ITestOutputHelper output)
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(DatabaseFactAttribute.MigrationEnvironmentVariable);

    [DatabaseFact(DatabaseFactAttribute.MigrationEnvironmentVariable)]
    [Trait("Category", "Integration")]
    public async Task マイグレーションを適用すると全データが登録され再実行しても変わらない()
    {
        var first = DatabaseMigrator.Migrate(ConnectionString!, NullLogger.Instance);
        Assert.True(first.Successful, first.Error?.Message);
        output.WriteLine($"1回目：{first.Scripts.Count()} 件のスクリプトを適用");

        // 2回目は適用済みのため、何も実行されない（schemaversions で管理）
        var second = DatabaseMigrator.Migrate(ConnectionString!, NullLogger.Instance);
        Assert.True(second.Successful, second.Error?.Message);
        Assert.Empty(second.Scripts);

        var provider = TestServices.CreateDataProvider();
        var towns = provider.Wards.Values.SelectMany(w => w.Towns).ToArray();

        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();

        Assert.Equal(provider.Prefecture.Municipalities.Count, await CountAsync(connection, "municipalities"));
        Assert.Equal(provider.Prefecture.Municipalities.Sum(m => m.Wards.Count), await CountAsync(connection, "wards"));
        Assert.Equal(towns.Length, await CountAsync(connection, "towns"));
        Assert.Equal(towns.Sum(t => t.Chomes.Count), await CountAsync(connection, "chomes"));
        Assert.Equal(DatabaseMigrator.GetScriptNames().Count, await CountAsync(connection, "schemaversions"));

        output.WriteLine($"municipalities: {await CountAsync(connection, "municipalities")} 件");
        output.WriteLine($"towns         : {await CountAsync(connection, "towns")} 件");
        output.WriteLine($"chomes        : {await CountAsync(connection, "chomes")} 件");
    }

    // テーブル名はこのクラス内の固定値のみを渡す（外部入力を SQL に連結しない）。
    private static async Task<int> CountAsync(NpgsqlConnection connection, string table)
    {
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
