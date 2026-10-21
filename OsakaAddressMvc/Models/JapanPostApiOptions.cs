namespace OsakaAddressMvc.Models;

// ClientId / SecretKey は環境変数 JapanPost__ClientId / JapanPost__SecretKey から設定します。
// 公開リポジトリや appsettings.json に実際の認証情報を保存しないでください。
public sealed class JapanPostApiOptions
{
    public string ClientId { get; set; } = "";
    public string SecretKey { get; set; } = "";
}
