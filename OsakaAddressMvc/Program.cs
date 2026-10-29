using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OsakaAddressMvc;
using OsakaAddressMvc.Infrastructure;
using OsakaAddressMvc.Infrastructure.Database;
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

// v0.6.0: 住所データの読み込み元を AddressData:Source で切り替える（Json：既定 / Database：PostgreSQL）。
// AddressDataProvider・AddressService・画面は IAddressDataSource だけを見るため、ここ以外は変更不要。
var addressDataSource = builder.Configuration.GetSection(AddressDataOptions.SectionName)
    .GetValue(nameof(AddressDataOptions.Source), AddressDataSourceKind.Json);
if (addressDataSource == AddressDataSourceKind.Database)
{
    var connectionString = builder.Configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            $"AddressData:Source が Database の場合は、接続文字列 ConnectionStrings:{DatabaseOptions.ConnectionStringName} を設定してください。");

    // DbContext は短命（Scoped）なので、Singleton の DatabaseAddressDataSource にはファクトリーを渡す。
    builder.Services.AddDbContextFactory<AddressDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddSingleton<IAddressDataSource, DatabaseAddressDataSource>();
}
else
{
    builder.Services.AddSingleton<IAddressDataSource, JsonAddressDataSource>();
}
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

// v0.6: 起動時に DB マイグレーションを実行する（Database:MigrateOnStartup が true の場合）。
// 失敗した場合は起動を中止する。接続文字列が未設定なら警告のみ出し、DB なしで起動を続ける。
// v0.6.0: DB から住所データを読む場合に備えて、データの読み込みより「先に」実行する。
RunDatabaseMigration(app);

// 起動時に大阪府の市町村一覧（43件）と大阪市24区の町名データを読み込んで検証する。
// データに不備があれば、この時点で起動を中止する。
var dataSource = app.Services.GetRequiredService<IAddressDataSource>();
app.Services.GetRequiredService<AddressDataProvider>();
app.Logger.LogInformation("住所データの読み込み元：{Source}", dataSource.SourceName);

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
