using Npgsql;
using Xunit.Abstractions;

namespace OsakaAddressMvc.Tests;

// ============================================================================
// データベース接続テスト（疎通確認用）
// ----------------------------------------------------------------------------
// 目的：開発環境から DB に接続できるかを確認する。自動テストではなく、必要なときに手動で実行する。
//
// 通常の dotnet test では実行されない（[DatabaseFact] により、環境変数が未設定ならスキップ）。
// 実行方法（PowerShell・ソリューションのフォルダーで実行）：
//   $env:OSAKA_TEST_DB = "Host=localhost;Port=5432;Database=...;Username=...;Password=..."
//   dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed"
//   Remove-Item Env:OSAKA_TEST_DB
//
//   ※ 接続文字列（パスワードを含む）はソースに書かず、必ず環境変数で渡すこと。
//   ※ 顧客先の DB に接続する前に、接続してよいか必ず確認すること。
//   ※ 出力された DB 名・ユーザー・バージョンは、疎通確認のエビデンスとして利用できる。
//
// ----------------------------------------------------------------------------
// DB 別のドライバー（テストプロジェクトに追加する）
// ----------------------------------------------------------------------------
// [PostgreSQL]  ← 現在のコード
//   dotnet add OsakaAddressMvc.Tests package Npgsql
//   using Npgsql;  /  NpgsqlConnection, NpgsqlCommand
//   接続文字列：Host=localhost;Port=5432;Database=osaka;Username=postgres;Password=...
//   確認 SQL ：SELECT current_database(), current_user, version()
//
// [SQL Server]
//   dotnet add OsakaAddressMvc.Tests package Microsoft.Data.SqlClient
//   using Microsoft.Data.SqlClient;  /  SqlConnection, SqlCommand
//   接続文字列：Server=localhost,1433;Database=osaka;User Id=sa;Password=...;TrustServerCertificate=True
//   確認 SQL ：SELECT DB_NAME(), SUSER_SNAME(), @@VERSION
//   ※ 既定で暗号化接続のため、ローカルの自己署名証明書では TrustServerCertificate=True が必要。
//
// [MySQL / MariaDB]
//   dotnet add OsakaAddressMvc.Tests package MySqlConnector
//   using MySqlConnector;  /  MySqlConnection, MySqlCommand
//   接続文字列：Server=localhost;Port=3306;Database=osaka;User ID=root;Password=...
//   確認 SQL ：SELECT DATABASE(), CURRENT_USER(), VERSION()
//
// [Oracle]
//   dotnet add OsakaAddressMvc.Tests package Oracle.ManagedDataAccess.Core
//   using Oracle.ManagedDataAccess.Client;  /  OracleConnection, OracleCommand
//   接続文字列：User Id=system;Password=...;Data Source=localhost:1521/FREEPDB1
//   確認 SQL ：SELECT SYS_CONTEXT('USERENV', 'DB_NAME'), USER, 'Oracle' FROM dual
//
// [SQLite]
//   dotnet add OsakaAddressMvc.Tests package Microsoft.Data.Sqlite
//   using Microsoft.Data.Sqlite;  /  SqliteConnection, SqliteCommand
//   接続文字列：Data Source=osaka.db
//   確認 SQL ：SELECT 'main', 'sqlite', sqlite_version()
//
// どのドライバーも ADO.NET 共通の DbConnection を継承しているため、
// クラス名・接続文字列・確認 SQL を差し替えるだけで、下のテストの構造はそのまま使える。
// ============================================================================

public sealed class DatabaseConnectionTests(ITestOutputHelper output)
{
    // [DatabaseFact] がスキップしなかった時点で、環境変数は設定済みであることが保証される。
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(DatabaseFactAttribute.EnvironmentVariable);

    [DatabaseFact]
    [Trait("Category", "Integration")]
    public async Task PostgreSQLに接続できる()
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT current_database(), current_user, version()", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        output.WriteLine($"DB      : {reader.GetString(0)}");
        output.WriteLine($"ユーザー: {reader.GetString(1)}");
        output.WriteLine($"バージョン: {reader.GetString(2)}");
    }
}
