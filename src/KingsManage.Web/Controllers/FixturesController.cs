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
	private readonly IMatchService _matchService;

	public FixturesController(IMatchService matchService)
	{
		_matchService = matchService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<MatchViewModel>>> GetAll(
		CancellationToken cancellationToken
	)
	{
		var matches = await _matchService.GetAllAsync(cancellationToken);
		return Ok(matches
			.OrderBy(match => match.Date)
			.Select(MatchViewModel.FromMatch)
			.ToList());
	}
}
