using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/sports")]
public sealed class SportsController : ControllerBase
{
	[HttpGet]
	public ActionResult<IReadOnlyList<SportDefinition>> GetAll() => Ok(SportCatalog.All);

	[HttpGet("{key}")]
	public ActionResult<SportDefinition> Get(string key)
	{
		var sport = SportCatalog.Find(key);
		return sport is null ? NotFound() : Ok(sport);
	}
}
