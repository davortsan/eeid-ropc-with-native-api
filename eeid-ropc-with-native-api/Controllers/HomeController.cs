using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using eeid_ropc_with_native_api.Models;
using eeid_ropc_with_native_api.Services;

namespace eeid_ropc_with_native_api.Controllers;

public class HomeController : Controller
{
    private readonly IExternalIdDemoService _externalIdDemoService;

    public HomeController(IExternalIdDemoService externalIdDemoService)
    {
        _externalIdDemoService = externalIdDemoService;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunTransparentAuth(CancellationToken cancellationToken)
    {
        var result = await _externalIdDemoService.ExecuteAsync(cancellationToken);
        return View("TransparentAuthResult", result);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
