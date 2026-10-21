using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OsakaAddressMvc.Models;
using OsakaAddressMvc.Services;

namespace OsakaAddressMvc.Tests;

// v0.5.5: ポストくん API の応答処理を、外部 API に接続せずに確認する。
// HttpClient に偽の HttpMessageHandler を渡し、任意の HTTP ステータス・JSON・例外を返させる。
public sealed class PostkunPostalCodeServiceTests
{
    private readonly AddressService _addressService = TestServices.CreateAddressService();

    private AreaInfo OsakaKita => _addressService.ResolveArea("大阪市北区")!;
    private AreaInfo SakaiKita => _addressService.ResolveArea("堺市北区")!;

    [Fact]
    public async Task 一致する候補だけを返し重複を除く()
    {
        var json = ToJson(
            Entry("5300017", "大阪府", OsakaKita, "角田町"),                 // 対象
            Entry("5300017", "大阪府", OsakaKita, "角田町"),                 // 重複（除外）
            Entry("5918000", "大阪府", SakaiKita, "角田町"),                 // 堺市北区の同名町域（除外）
            new { @new = "6000000", prefecture = "京都府", city = "京都市北区", suburb = "角田町", jis = "26101" }, // 他府県（除外）
            new { @new = "123", prefecture = "大阪府", city = OsakaKita.PostalCityName, suburb = "角田町", jis = OsakaKita.Code }); // 郵便番号の形式不正（除外）
        var (service, _) = CreateService(_ => Json(json));

        var result = await service.LookupAsync(OsakaKita, "角田町");

        Assert.Equal(PostalCodeSearchStatus.Success, result.Status);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("530-0017", candidate.FormattedZipCode);
        Assert.Equal(OsakaKita.PostalCityName, candidate.City);
    }

    [Fact]
    public async Task 該当なしなら町名単独でも検索してからNotFoundを返す()
    {
        var (service, handler) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await service.LookupAsync(OsakaKita, "角田町");

        Assert.Equal(PostalCodeSearchStatus.NotFound, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(2, handler.RequestCount);   // 「大阪市北区 角田町」→「角田町」の2回
    }

    [Fact]
    public async Task 応答429ならRateLimitedを返す()
    {
        var (service, _) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var result = await service.LookupAsync(OsakaKita, "角田町");
        Assert.Equal(PostalCodeSearchStatus.RateLimited, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task サーバーエラーならUnavailableを返す(HttpStatusCode status)
    {
        var (service, _) = CreateService(_ => new HttpResponseMessage(status));
        var result = await service.LookupAsync(OsakaKita, "角田町");
        Assert.Equal(PostalCodeSearchStatus.Unavailable, result.Status);
    }

    [Theory]
    [InlineData("{}")]            // 配列ではない
    [InlineData("not json")]      // JSON として解析できない
    public async Task 応答の形式が不正ならUnavailableを返す(string body)
    {
        var (service, _) = CreateService(_ => Json(body));
        var result = await service.LookupAsync(OsakaKita, "角田町");
        Assert.Equal(PostalCodeSearchStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task 通信エラーならUnavailableを返す()
    {
        var (service, _) = CreateService(_ => throw new HttpRequestException("接続できません"));
        var result = await service.LookupAsync(OsakaKita, "角田町");
        Assert.Equal(PostalCodeSearchStatus.Unavailable, result.Status);
    }

    // HttpClient のタイムアウトは、利用者の取り消しではない OperationCanceledException として届く。
    [Fact]
    public async Task タイムアウトならUnavailableを返す()
    {
        var (service, _) = CreateService(_ => throw new TaskCanceledException("タイムアウト"));
        var result = await service.LookupAsync(OsakaKita, "角田町");
        Assert.Equal(PostalCodeSearchStatus.Unavailable, result.Status);
    }

    // 利用者がリクエストを取り消した場合は、Unavailable に変換せずに例外をそのまま上へ伝える。
    // catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) の動作確認。
    [Fact]
    public async Task 利用者の取り消しは例外のまま伝える()
    {
        var (service, _) = CreateService(_ => Json("[]"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.LookupAsync(OsakaKita, "角田町", cts.Token));
    }

    // ---- テスト用の部品 ----

    private static (PostkunPostalCodeService Service, FakeHttpMessageHandler Handler) CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://postcode.example.test/") };
        return (new PostkunPostalCodeService(client, NullLogger<PostkunPostalCodeService>.Instance), handler);
    }

    private static object Entry(string zip, string prefecture, AreaInfo area, string suburb) =>
        new { @new = zip, prefecture, city = area.PostalCityName, suburb, jis = area.Code };

    private static string ToJson(params object[] entries) => JsonSerializer.Serialize(entries);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    // 受け取ったリクエストに対し、指定した関数の結果（応答または例外）を返す偽の HttpMessageHandler。
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }
}
