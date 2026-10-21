namespace OsakaAddressMvc.Tests;

// 環境変数 OSAKA_TEST_DB（接続文字列）が設定されているときだけ実行する Fact。
// 未設定の場合はスキップされるため、DB を起動していない日でも通常の dotnet test は成功する。
// xUnit の FactAttribute を継承し、Skip に理由を入れることで実行を止めている。
public sealed class DatabaseFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "OSAKA_TEST_DB";

    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
            Skip = $"環境変数 {EnvironmentVariable} が未設定のため、DB 接続テストをスキップしました。";
    }
}
