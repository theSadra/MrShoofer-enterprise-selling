using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Application.Areas.AgencyArea
{
  [Area("AgencyArea")]
  [AllowAnonymous]
  public class ErrorController : Controller
  {
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(ILogger<ErrorController> logger)
    {
      _logger = logger;
    }

    [Route("Error/{statusCode:int}")]
    public IActionResult HandleError(int statusCode)
    {
      if (Response.StatusCode == StatusCodes.Status200OK)
        Response.StatusCode = statusCode;

      LogExceptionFeature();

      if (statusCode == 403)
      {
        Response.StatusCode = 403;
        return View("AccessDenied");
      }

      if (statusCode == 404)
      {
        Response.StatusCode = 404;
        return View("NotFound");
      }

      Response.StatusCode = statusCode >= 400 ? statusCode : 500;
      return View("GenericError");
    }

    /// <summary>Exception-handler entry used by UseExceptionHandler — never exposes stack traces.</summary>
    [Route("Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
      Response.StatusCode = StatusCodes.Status500InternalServerError;
      LogExceptionFeature();
      return View("GenericError");
    }

    private void LogExceptionFeature()
    {
      var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
      if (feature?.Error == null) return;
      _logger.LogError(feature.Error, "Unhandled exception at {Path}", feature.Path);
    }
  }
}
