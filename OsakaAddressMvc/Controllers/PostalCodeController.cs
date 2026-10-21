using Microsoft.AspNetCore.Mvc;
using OsakaAddressMvc.Models;
using OsakaAddressMvc.Services;

namespace OsakaAddressMvc.Controllers;

// v0.3: 住所検索（/Address）とは独立した URL（/PostalCode）で郵便番号検索を提供する。
// v0.5: 大阪府内の全地域に対応。大阪市の区は町名を一覧から選び、それ以外の地域は町名を入力する。
public sealed class PostalCodeController(
    AddressService addressService, PostkunPostalCodeService postalCodeService) : Controller
{
    [HttpGet]
    public IActionResult Index(string? area, string? ward, string? town) =>
        View(CreateModel(area ?? ward, town));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(string? area, string? ward, string? town,
        CancellationToken cancellationToken)
    {
        // 外部APIへ送信する前に、地域・町名をサーバー側で検証する。
        // 大阪市の区は JSON データとの一致、それ以外の地域は町名の形式を確認する。
        var selectedArea = addressService.ResolveArea(area ?? ward);
        string? validTown = null;
        if (selectedArea is not null)
        {
            if (selectedArea.HasTownData)
                validTown = town is not null && addressService.GetTowns(selectedArea.Key).Contains(town) ? town : null;
            else if (AddressService.TryNormalizeTownName(town, out var normalized))
                validTown = normalized;
        }
        if (selectedArea is null || validTown is null)
        {
            return View("Index", CreateModel(area ?? ward, town,
                "地域と町名を正しく指定してください（町名は漢字・かなで30文字以内、丁目は含めない）。", true));
        }

        var lookup = await postalCodeService.LookupAsync(selectedArea, validTown, cancellationToken);
        var message = lookup.Status switch
        {
            PostalCodeSearchStatus.Success => null,
            PostalCodeSearchStatus.NotFound =>
                "該当する町名の郵便番号を確認できませんでした。町名の表記（丁目を含めない）を確認してください。丁目・番・号は照合対象外です。",
            PostalCodeSearchStatus.RateLimited =>
                "ポストくん API へのアクセスが制限されています。時間をおいてお試しください。",
            _ => "郵便番号 API に接続できませんでした。従来の住所検索は引き続き利用できます。"
        };
        return View("Index", CreateModel(selectedArea.Key, validTown, message, true, lookup.Candidates));
    }

    private PostalCodeViewModel CreateModel(string? area, string? town,
        string? message = null, bool hasSearched = false,
        IReadOnlyList<PostalCodeCandidate>? candidates = null)
    {
        var selectedArea = addressService.ResolveArea(area);
        var towns = addressService.GetTowns(selectedArea?.Key);
        var selectedTown = selectedArea?.HasTownData == true
            ? (towns.Contains(town) ? town : null)
            : town?.Trim();
        return new PostalCodeViewModel
        {
            SelectedArea = selectedArea,
            SelectedTown = selectedTown,
            Areas = addressService.SearchAreas(null),
            Towns = towns,
            Message = message,
            HasSearched = hasSearched,
            Candidates = candidates ?? []
        };
    }
}
