using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/club-teams")]
public class ClubTeamsController : ControllerBase
{
	private readonly IClubTeamService _clubTeamService;

	public ClubTeamsController(IClubTeamService clubTeamService)
	{
		_clubTeamService = clubTeamService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<ClubTeamProfile>>> GetAll(
		CancellationToken cancellationToken
	)
	{
		var profiles = await _clubTeamService.GetAllAsync(cancellationToken);
		return Ok(profiles);
	}

	[Authorize(Roles = "Admin")]
	[HttpPost]
	public async Task<ActionResult<ClubTeamProfile>> Create(
		ClubTeamProfile profile,
		CancellationToken cancellationToken
	)
	{
		var validationError = Validate(profile);
		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var createdProfile = await _clubTeamService.CreateAsync(profile, cancellationToken);
		return Created("/api/club-teams", createdProfile);
	}

	[Authorize(Roles = "Admin")]
	[HttpPut("{id}")]
	public async Task<ActionResult<ClubTeamProfile>> Update(
		string id,
		ClubTeamProfile profile,
		CancellationToken cancellationToken
	)
	{
		if (!Guid.TryParse(id, out var teamId))
		{
			return BadRequest("Team id must be a valid GUID.");
		}

		var validationError = Validate(profile);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var updatedProfile = await _clubTeamService.UpdateAsync(
			teamId,
			profile,
			cancellationToken
		);

		return Ok(updatedProfile);
	}

	[Authorize(Roles = "Admin")]
	[HttpDelete("{id}")]
	public async Task<IActionResult> Delete(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!Guid.TryParse(id, out var teamId))
		{
			return BadRequest("Team id must be a valid GUID.");
		}

		var result = await _clubTeamService.DeleteAsync(teamId, cancellationToken);
		return result switch
		{
			ClubTeamDeleteResult.Deleted => NoContent(),
			ClubTeamDeleteResult.NotFound => NotFound(),
			ClubTeamDeleteResult.InUse => Conflict(
				"This team is used by existing club records. Make it inactive instead."
			),
			_ => StatusCode(StatusCodes.Status500InternalServerError)
		};
	}

	private static string? Validate(ClubTeamProfile profile)
	{
		if (string.IsNullOrWhiteSpace(profile.DisplayName))
		{
			return "Display name is required.";
		}

		if (profile.DisplayName.Trim().Length > 50)
		{
			return "Display name must be 50 characters or fewer.";
		}

		if (string.IsNullOrWhiteSpace(profile.ShortName))
		{
			return "Short name is required.";
		}

		if (profile.ShortName.Trim().Length > 20)
		{
			return "Short name must be 20 characters or fewer.";
		}

		if (profile.SortOrder < 0 || profile.SortOrder > 100)
		{
			return "Sort order must be between 0 and 100.";
		}

		if ((profile.Competitions ?? []).Any(competition =>
			string.IsNullOrWhiteSpace(competition) || competition.Trim().Length > 100))
		{
			return "Competition names are required and must be 100 characters or fewer.";
		}

		return null;
	}
}
