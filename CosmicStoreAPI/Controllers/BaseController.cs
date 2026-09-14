using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CosmicStoreAPI.Controllers
{
    /// <summary>
    /// Shared API base: every derived controller is served at <c>api/{ControllerName}</c>.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BaseController : ControllerBase
    {
    }
}
