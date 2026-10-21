using Microsoft.Extensions.Options;
using OsakaAddressMvc.Models;
using OsakaAddressMvc.Services;

namespace OsakaAddressMvc.Tests;

// テスト用のサービス生成。テストの出力先にコピーされた Data/（Municipalities.json と Wards/）を読み込む。
// v0.5.5: AddressDataProvider がデータフォルダーの場所だけを受け取るようになったため、
// IWebHostEnvironment の偽実装（TestWebHostEnvironment）は不要になった。
internal static class TestServices
{
    public static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");

    public static AddressDataProvider CreateDataProvider() =>
        new(Options.Create(new AddressDataOptions { DataDirectory = DataDirectory }));

    public static AddressService CreateAddressService() => new(CreateDataProvider());
}
