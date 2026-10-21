namespace OsakaAddressMvc.Models;

// /PostalCode 画面専用のモデル。既存の住所検索画面のモデルには手を加えない。
public sealed class PostalCodeViewModel
{
    // v0.5: 区名から「地域」（大阪市北区・堺市堺区・豊中市など）に変更した。
    public AreaInfo? SelectedArea { get; init; }
    public string? SelectedTown { get; init; }
    public IReadOnlyList<AreaInfo> Areas { get; init; } = [];
    public IReadOnlyList<string> Towns { get; init; } = [];
    public IReadOnlyList<PostalCodeCandidate> Candidates { get; init; } = [];
    public string? Message { get; init; }
    public bool HasSearched { get; init; }
}

public sealed record PostalCodeCandidate(string ZipCode, string Prefecture, string City, string Town)
{
    public string FormattedZipCode => $"{ZipCode[..3]}-{ZipCode[3..]}";
}
