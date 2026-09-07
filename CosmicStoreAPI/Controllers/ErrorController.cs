using System;
using CosmicStoreAPI.Error;
using Microsoft.AspNetCore.Mvc;

namespace CosmicStoreAPI.Controllers;

[Route("/errors/{code}")]
public class ErrorController:BaseController
{

      public IActionResult Error(int code)
        {
            return new OkObjectResult(new ApiErrorResponse(code));
        }
}