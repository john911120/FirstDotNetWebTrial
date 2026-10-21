using System.Net;
using System.Text.Json;
using OsakaAddressMvc.Models;

namespace OsakaAddressMvc.Services;

public enum PostalCodeSearchStatus
{
    Success, NotFound, RateLimited, Unavailable
}

public sealed record PostalCodeSearchResult(
    PostalCodeSearchStatus Status, IReadOnlyList<PostalCodeCandidate> Candidates);

// ポストくんの公開 REST API を使用する。認証情報や郵便番号マスタの保存は不要。
// 応答はあくまで町域単位の候補であり、丁目・番・号までは検証しない。
// v0.5: 大阪市の区に限らず、大阪府内の全地域（堺市の区・市・町村）を照合できるようにした。
public sealed class PostkunPostalCodeService(
    HttpClient httpClient, ILogger<PostkunPostalCodeService> logger)
{
    public async Task<PostalCodeSearchResult> LookupAsync(
        AreaInfo area, string town, CancellationToken cancellationToken = default)
    {
        // まず住所要素を含む絞り込みを試し、結果がない場合は町名単独でも検索する。
        // 必ず応答側の都道府県・市区町村・町名を照合し、他の地域の同名町域を除外する。
        string[] keywords = [$"{area.Municipality}{area.Ward} {town}", town];
        foreach (string keyword in keywords.Distinct())
        {
            try
            {
                string path = $"postcodes.json?s={Uri.EscapeDataString(keyword)}&per=100";
                using var response = await httpClient.GetAsync(
                    path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    return new(PostalCodeSearchStatus.RateLimited, []);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    continue;
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("ポストくん API HTTP ステータス: {StatusCode}", (int)response.StatusCode);
                    return new(PostalCodeSearchStatus.Unavailable, []);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    logger.LogWarning("ポストくん API の応答が配列形式ではありません。");
                    return new(PostalCodeSearchStatus.Unavailable, []);
                }

                var candidates = new List<PostalCodeCandidate>();
                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object) continue;
                    string? zip = ReadString(entry, "new");
                    string? prefecture = ReadString(entry, "prefecture");
                    string? city = ReadString(entry, "city");
                    string? suburb = ReadString(entry, "suburb");
                    string? jis = ReadCode(entry, "jis");
                    if (zip is null || zip.Length != 7 || !zip.All(char.IsAsciiDigit) ||
                        prefecture != "大阪府" || city is null || suburb != town ||
                        !IsSameArea(area, city, jis))
                        continue;

                    if (candidates.All(item => item.ZipCode != zip))
                        candidates.Add(new PostalCodeCandidate(zip, prefecture, city, suburb));
                }

                if (candidates.Count != 0)
                    return new(PostalCodeSearchStatus.Success,
                        candidates.OrderBy(item => item.ZipCode).Take(30).ToArray());
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("ポストくん API の応答がタイムアウトしました。");
                return new(PostalCodeSearchStatus.Unavailable, []);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("ポストくん API 通信エラー: {ErrorType}", ex.GetType().Name);
                return new(PostalCodeSearchStatus.Unavailable, []);
            }
            catch (JsonException)
            {
                logger.LogWarning("ポストくん API の JSON 解析に失敗しました。");
                return new(PostalCodeSearchStatus.Unavailable, []);
            }
        }
        return new(PostalCodeSearchStatus.NotFound, []);
    }

    // 地方公共団体コード（jis）または市区町村名（郡名を含む表記）のどちらかが一致すれば同じ地域とみなす。
    // 例：堺市北区と大阪市北区は jis・市区町村名のどちらでも区別される。
    private static bool IsSameArea(AreaInfo area, string city, string? jis) =>
        jis == area.Code || city == area.PostalCityName;

    private static string? ReadString(JsonElement entry, string key) =>
        entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    // jis は文字列・数値のどちらで返っても扱えるようにする。
    private static string? ReadCode(JsonElement entry, string key) =>
        !entry.TryGetProperty(key, out var value) ? null : value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
}
