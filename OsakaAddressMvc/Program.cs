using Microsoft.Extensions.Options;
using OsakaAddressMvc;
using OsakaAddressMvc.Models;
using OsakaAddressMvc.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

// v0.5.5: 住所データの場所を appsettings.json（AddressData:DataDirectory）から読み込む。
// 相対パスはコンテンツルートを基準に解決する。
builder.Services.AddOptions<AddressDataOptions>()
    .Bind(builder.Configuration.GetSection(AddressDataOptions.SectionName))
    .PostConfigure(options =>
    {
        if (!Path.IsPathRooted(options.DataDirectory))
            options.DataDirectory = Path.Combine(builder.Environment.ContentRootPath, options.DataDirectory);
    });
builder.Services.AddSingleton<AddressDataProvider>();
builder.Services.AddSingleton<AddressService>();

// 外部APIに依存するのは郵便番号検索のみとし、既存の住所検索は外部APIなしで動作させる。
// v0.5.5: 接続先とタイムアウトを appsettings.json（PostalCodeApi）から読み込み、起動時に値を検証する。
builder.Services.AddOptions<PostalCodeApiOptions>()
    .Bind(builder.Configuration.GetSection(PostalCodeApiOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out _),
        "PostalCodeApi:BaseAddress には絶対 URL を指定してください。")
    .Validate(options => options.TimeoutSeconds is >= 1 and <= 60,
        "PostalCodeApi:TimeoutSeconds は 1～60 の範囲で指定してください。")
    .ValidateOnStart();
builder.Services.AddHttpClient<PostkunPostalCodeService>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<PostalCodeApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseAddress);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd($"OsakaAddressMvc/{AppInfo.Version}");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

var app = builder.Build();

// 起動時に大阪府の市町村一覧（43件）と大阪市24区の町名 JSON を読み込んで検証する。
// データに不備があれば、この時点で起動を中止する。
app.Services.GetRequiredService<AddressDataProvider>();

if (!app.Environment.IsDevelopment())
{
    // v0.5.5: HomeController の削除に伴い、エラー画面は ErrorController（/Error）に移した。
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Address}/{action=Index}/{id?}")
   .WithStaticAssets();
app.Run();
