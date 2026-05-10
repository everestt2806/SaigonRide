using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SaigonRide.Web.Models;

namespace SaigonRide.Web.Controllers;

[Route("[controller]")]
public class ErrorController : Controller
{
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(ILogger<ErrorController> logger) => _logger = logger;

    [HttpGet, HttpGet("{statusCode:int}")]
    public IActionResult Index(int? statusCode = null)
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        if (feature is not null)
        {
            _logger.LogError(feature.Error, "Unhandled exception caught by /Error.");
        }
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View(new ErrorViewModel
        {
            RequestId = requestId,
            StatusCode = statusCode
        });
    }
}
