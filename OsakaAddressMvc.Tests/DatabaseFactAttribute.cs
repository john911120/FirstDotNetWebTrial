namespace OsakaAddressMvc.Tests;

// 指定した環境変数（接続文字列）が設定されているときだけ実行する Fact。
// 未設定の場合はスキップされるため、DB を起動していない日でも通常の dotnet test は成功する。
// xUnit の FactAttribute を継承し、Skip に理由を入れることで実行を止めている。
//
//   [DatabaseFact]                               … 環境変数 OSAKA_TEST_DB（疎通確認用）
//   [DatabaseFact(MigrationEnvironmentVariable)] … 環境変数 OSAKA_MIGRATION_TEST_DB（v0.6 マイグレーション確認用）
public sealed class DatabaseFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "OSAKA_TEST_DB";
    public const string MigrationEnvironmentVariable = "OSAKA_MIGRATION_TEST_DB";

    public DatabaseFactAttribute() : this(EnvironmentVariable)
    {
    }

    public DatabaseFactAttribute(string environmentVariable)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environmentVariable)))
            Skip = $"環境変数 {environmentVariable} が未設定のため、DB を使うテストをスキップしました。";
    }
}
