namespace OsakaAddressMvc.Models;

// Controller から Razor View へ表示用データを渡すためのモデル。
public class AddressViewModel
{
    public string? Keyword { get; init; }
    // v0.5: 区名の文字列から「地域」（大阪市北区・堺市堺区・豊中市など）に変更した。
    public AreaInfo? SelectedArea { get; init; }
    public string? SelectedTown { get; init; }
    public string? SelectedChome { get; init; }
    public string? SelectedBan { get; init; }
    public string? SelectedGo { get; init; }

    public IReadOnlyList<AreaInfo> Areas { get; init; } = Array.Empty<AreaInfo>();
    public IReadOnlyList<string> Towns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Chomes { get; init; } = Array.Empty<string>();

    public string? ResultAddress { get; init; }
    public bool AllowWithoutChome { get; init; }
    public string? ErrorMessage { get; init; }

    // 町名を一覧から選ぶ地域（大阪市の区）かどうか。false の場合は町名を入力する。
    public bool HasTownData => SelectedArea?.HasTownData == true;
}
