using OsakaAddressMvc.Infrastructure;

namespace OsakaAddressMvc.Tests;

// v0.6: DB マイグレーションのスクリプトを、DB に接続せずに検査する。
public sealed class MigrationScriptTests
{
    [Fact]
    public void マイグレーションスクリプトは3桁の連番で埋め込まれている()
    {
        var names = DatabaseMigrator.GetScriptNames()
            .Select(n => n[DatabaseMigrator.ScriptResourcePrefix.Length..])
            .ToArray();

        Assert.True(names.Length >= 3, "db/migration/ のスクリプトが埋め込まれていません（csproj の EmbeddedResource を確認）。");
        // Flyway と同じ命名規則。番号を3桁にしないと、DbUp の文字列順で V10 が V2 より先に実行される。
        Assert.All(names, n => Assert.Matches(@"^V\d{3}__[a-z0-9_]+\.sql$", n));
        // 番号は 1 から欠番・重複なし
        Assert.Equal(Enumerable.Range(1, names.Length), names.Select(n => int.Parse(n[1..4])));
        Assert.Equal("V001__create_tables.sql", names[0]);
    }

    [Fact]
    public void シードデータの件数はJSONデータと一致する()
    {
        var provider = TestServices.CreateDataProvider();
        var sql = string.Join("\n", DatabaseMigrator.GetScriptNames().Select(DatabaseMigrator.ReadScript));

        var municipalities = provider.Prefecture.Municipalities;
        var towns = provider.Wards.Values.SelectMany(w => w.Towns).ToArray();

        Assert.Equal(municipalities.Count, CountRows(sql, "municipalities"));          // 43
        Assert.Equal(municipalities.Sum(m => m.Wards.Count), CountRows(sql, "wards"));  // 31
        Assert.Equal(towns.Length, CountRows(sql, "towns"));                           // 580
        Assert.Equal(towns.Sum(t => t.Chomes.Count), CountRows(sql, "chomes"));        // 1815
    }

    // "INSERT INTO {table} (" の次の行から、"  (" で始まる行を ";" で終わる行まで数える（シードは1行1件で記載）。
    private static int CountRows(string sql, string table)
    {
        var start = sql.IndexOf($"INSERT INTO {table} (", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{table} の INSERT 文が見つかりません。");

        var count = 0;
        foreach (var raw in sql[start..].Split('\n').Skip(1))
        {
            var line = raw.TrimEnd('\r');
            if (!line.StartsWith("  (", StringComparison.Ordinal)) break;
            count++;
            if (line.EndsWith(';')) break;
        }
        return count;
    }
}
