namespace OsakaAddressMvc.Models;

public sealed class WardData
{
    public string Ward { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public List<TownData> Towns { get; set; } = new();
}

public sealed class TownData
{
    public string Name { get; set; } = "";
    // 丁目がない町は空配列。丁目の数は町によって異なる。
    public List<string> Chomes { get; set; } = new();
    public bool AllowWithoutChome { get; set; }
}
