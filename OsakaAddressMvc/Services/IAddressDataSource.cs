using OsakaAddressMvc.Models;

namespace OsakaAddressMvc.Services;

/*
 v0.6.0-alpha.2: 住所データを「どこから読むか」だけを表すインターフェース。
 実装：JsonAddressDataSource（Data/ の JSON）。v0.6.0 で DB（PostgreSQL）の実装を追加する予定。
 読み込んだデータの件数・重複などの検証は、データ元に関係なく AddressDataProvider が行う。
*/
public interface IAddressDataSource
{
    // エラーメッセージに表示するデータ元の名前（例：JSON（C:\...\Data））
    string SourceName { get; }

    PrefectureData LoadPrefecture();

    // 区名の重複を AddressDataProvider で検査できるように、Dictionary ではなく List で返す。
    IReadOnlyList<WardData> LoadOsakaCityWards();
}
