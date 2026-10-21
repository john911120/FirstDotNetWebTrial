using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OsakaAddressMvc.Models;

namespace OsakaAddressMvc.Controllers;

// v0.5.5: テンプレート由来の HomeController（Index・Privacy）を削除し、エラー画面だけを独立させた。
// 本番環境では Program.cs の UseExceptionHandler("/Error") からこのアクションが呼ばれる。
public sealed class ErrorController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index() =>
        View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
