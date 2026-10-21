using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OsakaAddressMvc.Models;
using OsakaAddressMvc.Services;

namespace OsakaAddressMvc.Controllers;

public sealed class AddressController(AddressService addressService) : Controller
{
    // ダウンロード用 JSON の設定。生成コストがかかるため、リクエストごとに作らず使い回す。
    private static readonly JsonSerializerOptions DownloadJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // v0.5: 選択単位を area（例：大阪市北区・堺市堺区・豊中市）に変更。
    // v0.4 までの ward=北区 形式の URL も、大阪市の区として引き続き受け付ける。
    [HttpGet]
    public IActionResult Index(string? keyword, string? area, string? ward, string? town,
        string? chome, string? ban, string? go, string? mode)
    {
        var requestedArea = area ?? ward;
        var selectedArea = addressService.ResolveArea(requestedArea);
        var areaKey = selectedArea?.Key;
        var towns = addressService.GetTowns(areaKey);
        var hasTownData = selectedArea?.HasTownData == true;

        // 町名データのある地域は一覧に含まれる町名だけを選択状態にし、それ以外は入力値をそのまま保持する。
        string? selectedTown = hasTownData ? (towns.Contains(town) ? town : null) : town?.Trim();
        var chomes = addressService.GetChomes(areaKey, selectedTown);
        string? selectedChome = hasTownData ? (chomes.Contains(chome) ? chome : null) : chome?.Trim();
        string? address = null;
        string? error = null;

        if (!string.IsNullOrWhiteSpace(requestedArea) && selectedArea is null)
            error = "指定された地域は一覧にありません。";
        else if (mode == "show" && selectedArea is not null)
        {
            address = addressService.BuildAddress(areaKey, selectedTown,
                hasTownData && chomes.Count == 0 ? null : selectedChome, ban, go);
            if (address is null)
                error = hasTownData
                    ? "町名・丁目・番・号を確認してください（番・号は1～6桁の数字、号のみの指定は不可）。"
                    : "町名・丁目・番・号を確認してください（町名は漢字・かなで30文字以内、丁目は1～99、番・号は1～6桁の数字、号のみの指定は不可）。";
        }

        return View(new AddressViewModel
        {
            Keyword = keyword,
            SelectedArea = selectedArea,
            SelectedTown = selectedTown, SelectedChome = selectedChome,
            SelectedBan = ban, SelectedGo = go,
            Areas = addressService.SearchAreas(keyword),
            Towns = towns, Chomes = chomes,
            AllowWithoutChome = addressService.CanOmitChome(areaKey, selectedTown),
            ResultAddress = address,
            ErrorMessage = error
        });
    }

    // 選択した住所を JSON ファイルとしてダウンロードさせる（画面表示用の Index とは別のアクション）。
    [HttpGet]
    public IActionResult DownloadJson(string? area, string? ward, string? town, string? chome, string? ban, string? go)
    {
        var built = addressService.TryBuild(area ?? ward, town, chome, ban, go);
        if (built is null) return BadRequest("住所の指定が不正です。");
        return Download(addressService.GetAddressJson(built), "osaka-address.json");
    }

    [HttpGet]
    public IActionResult DownloadDataset() =>
        Download(addressService.GetDatasetJson(), "osaka-address-dataset.json");

    private FileContentResult Download(object payload, string filename)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, DownloadJsonOptions);
        Response.Headers["Cache-Control"] = "no-store";
        return File(json, "application/json; charset=utf-8", filename);
    }
}
