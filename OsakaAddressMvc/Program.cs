using Microsoft.Extensions.Options;
using OsakaAddressMvc;
using OsakaAddressMvc.Infrastructure;
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

// v0.6: DB マイグレーション（DbUp）の設定。接続文字列は ConnectionStrings:OAMVC（開発中は User Secrets）。
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

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

// v0.6: 起動時に DB マイグレーションを実行する（Database:MigrateOnStartup が true の場合）。
// 失敗した場合は起動を中止する。接続文字列が未設定なら警告のみ出し、DB なしで起動を続ける。
RunDatabaseMigration(app);

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

// ---- 起動時の処理 ----

static void RunDatabaseMigration(WebApplication app)
{
    var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    if (!options.MigrateOnStartup) return;

    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("OsakaAddressMvc.DatabaseMigrator");
    var connectionString = app.Configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        logger.LogWarning("接続文字列 ConnectionStrings:{Name} が未設定のため、DB マイグレーションをスキップしました。",
            DatabaseOptions.ConnectionStringName);
        return;
    }

    var result = DatabaseMigrator.Migrate(connectionString, logger);
    if (!result.Successful)
        throw new InvalidOperationException(
            $"DB マイグレーションに失敗しました（{result.ErrorScript?.Name ?? "不明なスクリプト"}）。", result.Error);

    logger.LogInformation("DB マイグレーション完了：今回適用したスクリプト {Count} 件。", result.Scripts.Count());
}
