using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.MediaGovernance.Services.Images;

namespace Umbraco.Community.MediaGovernance.TestSite.Controllers;

/// <summary>Test site only: a public page showing how templates use the governance helpers.</summary>
[Route("governance-demo")]
public class GovernanceDemoController : Controller
{
	private readonly IImageCatalog _catalog;

	public GovernanceDemoController(IImageCatalog catalog)
	{
		_catalog = catalog;
	}

	[HttpGet("")]
	public async Task<IActionResult> Index() => View(await _catalog.ListImagesAsync());
}
