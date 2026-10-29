namespace OsakaAddressMvc.Tests;

// v0.6.0: DB を使うテストを1つのコレクションにまとめ、並列に実行しないようにする。
// xUnit は既定でテストクラス同士を並列に実行するため、同じテスト用 DB に対して
// 複数のテストが同時にマイグレーション（CREATE TABLE など）を実行すると、
// PostgreSQL のエラー（23505: pg_type_typname_nsp_index の重複）で失敗する。
// 同じコレクションに属するテストクラスは、順番に1つずつ実行される。
[CollectionDefinition(Name)]
public sealed class DatabaseCollection
{
    public const string Name = "Database";
}
