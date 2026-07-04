using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/fixtures")]
public class FixturesController : ControllerBase
{
	private readonly IMatchService matchService;

	public FixturesController(IMatchService matchService)
	{
		this.matchService = matchService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<MatchViewModel>>> GetAll(
		CancellationToken cancellationToken
	)
	{
		var matches = await matchService.GetAllAsync(cancellationToken);
		return Ok(matches
			.OrderBy(match => match.Date)
			.Select(MatchViewModel.FromMatch)
			.ToList());
	}
}
