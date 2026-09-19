using System;
using CosmicStoreAPI.Error;
using Microsoft.AspNetCore.Mvc;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Turns ASP.NET status-code re-executes into a JSON error body Angular can display.
/// </summary>
[Route("/errors/{code}")]
public class ErrorController:BaseController
{
      /// <summary>
      /// Returns an <see cref="ApiErrorResponse"/> with the original HTTP status (401, 404, etc.).
      /// </summary>
      public IActionResult Error(int code)
        {
            // Returning 200 here made every 401/403/404 look like a success to the
            // Angular client, so failed requests silently rendered as empty pages.
            return new ObjectResult(new ApiErrorResponse(code)) { StatusCode = code };
        }
}