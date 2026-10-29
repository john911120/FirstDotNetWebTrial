using DbUp;
using DbUp.Engine;

namespace OsakaAddressMvc.Infrastructure;

// v0.6: DbUp で DB マイグレーションを実行する。
// スクリプトは db/migration/*.sql をアセンブリに埋め込んだもの（ファイルのコピー漏れが起きない）。
// Spring Boot で flyway-core を追加すると起動時に自動実行されるのと同じ役割を、明示的に呼び出す形で行う。
public static class DatabaseMigrator
{
    // 埋め込みリソース名の接頭辞（ルート名前空間 + フォルダー構成）。
    public const string ScriptResourcePrefix = "OsakaAddressMvc.db.migration.";

    // 埋め込まれたスクリプトの一覧を、DbUp と同じ順序（ファイル名の序数比較）で返す。
    public static IReadOnlyList<string> GetScriptNames() =>
        typeof(DatabaseMigrator).Assembly.GetManifestResourceNames()
            .Where(IsMigrationScript)
            .Order(StringComparer.Ordinal)
            .ToArray();

    // 埋め込まれたスクリプトの内容を読み込む（テストでの検査用）。
    public static string ReadScript(string resourceName)
    {
        using var stream = typeof(DatabaseMigrator).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"スクリプトが見つかりません: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // 未適用のスクリプトを番号順に実行する。適用済みのスクリプトは schemaversions テーブルで管理され、再実行されない。
    public static DatabaseUpgradeResult Migrate(string connectionString, ILogger logger)
    {
        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly, IsMigrationScript)
            .WithTransactionPerScript()   // スクリプト単位で成功・失敗（途中で失敗したスクリプトは巻き戻す）
            .WithVariablesDisabled()      // SQL 内の $...$ を DbUp の変数として置換しない
            .LogTo(logger)
            .Build();

        return upgrader.PerformUpgrade();
    }

    private static bool IsMigrationScript(string resourceName) =>
        resourceName.StartsWith(ScriptResourcePrefix, StringComparison.Ordinal) &&
        resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase);
}
