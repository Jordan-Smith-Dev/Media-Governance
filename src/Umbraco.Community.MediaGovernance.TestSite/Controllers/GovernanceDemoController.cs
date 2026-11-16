using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.TestSite.Controllers;

/// <summary>Test site only: a public page showing how templates use the governance helpers.</summary>
[Route("governance-demo")]
public class GovernanceDemoController(IImageCatalog catalog) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await catalog.ListImagesAsync());
}
