using KingsManage;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
	private readonly IMatchService _matchService;

	public MatchesController(IMatchService matchService)
	{
		_matchService = matchService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<Match>>> GetAll(
		[FromQuery] string? seasonId,
		CancellationToken cancellationToken
	)
	{
		if (!string.IsNullOrWhiteSpace(seasonId))
		{
			var seasonMatches = await _matchService.GetBySeasonAsync(
				seasonId,
				cancellationToken
			);

			return Ok(seasonMatches);
		}

		var matches = await _matchService.GetAllAsync(cancellationToken);

		return Ok(matches);
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<Match>> GetById(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return BadRequest("Match id is required.");
		}

		var match = await _matchService.GetByIdAsync(id, cancellationToken);

		if (match is null)
		{
			return NotFound();
		}

		return Ok(match);
	}

	[HttpPost]
	public async Task<ActionResult<Match>> Create(
		Match match,
		CancellationToken cancellationToken
	)
	{
		var validationError = ValidateMatch(match);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var createdMatch = await _matchService.CreateAsync(match, cancellationToken);

		return CreatedAtAction(
			nameof(GetById),
			new { id = createdMatch.Id },
			createdMatch
		);
	}

	[HttpPut("{id}")]
	public async Task<ActionResult<Match>> Update(
		string id,
		Match match,
		CancellationToken cancellationToken
	)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return BadRequest("Match id is required.");
		}

		var validationError = ValidateMatch(match);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var existingMatch = await _matchService.GetByIdAsync(id, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		match.Id = id;
		match.CreatedAt = existingMatch.CreatedAt;

		var updatedMatch = await _matchService.UpdateAsync(match, cancellationToken);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		return Ok(updatedMatch);
	}

	[HttpDelete("{id}")]
	public async Task<IActionResult> Delete(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return BadRequest("Match id is required.");
		}

		var deleted = await _matchService.DeleteAsync(id, cancellationToken);

		if (!deleted)
		{
			return NotFound();
		}

		return NoContent();
	}

	[HttpPut("{id}/lineup")]
	public async Task<ActionResult<Match>> SetLineup(
		string id,
		List<SelectedPlayer> selectedPlayers,
		CancellationToken cancellationToken
	)
	{
		var updatedMatch = await _matchService.SetSelectedPlayersAsync(
			id,
			selectedPlayers,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		return Ok(updatedMatch);
	}

	[HttpPut("{id}/formation")]
	public async Task<ActionResult<Match>> SetFormation(
		string id,
		LineupFormation formation,
		CancellationToken cancellationToken
	)
	{
		var updatedMatch = await _matchService.SetLineupFormationAsync(
			id,
			formation,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		return Ok(updatedMatch);
	}

	[HttpPatch("{id}/lineup/toggle-lock")]
	public async Task<ActionResult<Match>> ToggleLineupLocked(
		string id,
		CancellationToken cancellationToken
	)
	{
		var updatedMatch = await _matchService.ToggleLineupLockedAsync(
			id,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		return Ok(updatedMatch);
	}

	[HttpPut("{id}/result")]
	public async Task<ActionResult<Match>> SetResult(
		string id,
		MatchResult result,
		CancellationToken cancellationToken
	)
	{
		if (result.HomeGoals < 0 || result.AwayGoals < 0)
		{
			return BadRequest("Goals cannot be negative.");
		}

		var updatedMatch = await _matchService.SetResultAsync(
			id,
			result,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		return Ok(updatedMatch);
	}

	[HttpDelete("{id}/result")]
	public async Task<ActionResult<Match>> ClearResult(
		string id,
		CancellationToken cancellationToken
	)
	{
		var updatedMatch = await _matchService.ClearResultAsync(id, cancellationToken);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		return Ok(updatedMatch);
	}

	[HttpPut("{id}/player-stats")]
	public async Task<ActionResult<Match>> UpdatePlayerStats(
		string id,
		List<MatchPlayerStat> playerStats,
		CancellationToken cancellationToken
	)
	{
		var updatedMatch = await _matchService.UpdatePlayerStatsAsync(
			id,
			playerStats,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		return Ok(updatedMatch);
	}

	[HttpPut("{id}/notes")]
	public async Task<ActionResult<Match>> UpdateNotes(
		string id,
		MatchNotes notes,
		CancellationToken cancellationToken
	)
	{
		var updatedMatch = await _matchService.UpdateNotesAsync(
			id,
			notes,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		return Ok(updatedMatch);
	}

	[HttpPost("{id}/postpone")]
	public async Task<ActionResult<Match>> Postpone(
		string id,
		PostponeMatchModel model,
		CancellationToken cancellationToken
	)
	{
		if (model.NewDate == default)
		{
			return BadRequest("New date is required.");
		}

		var updatedMatch = await _matchService.PostponeAsync(
			id,
			model.NewDate,
			model.Reason,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		return Ok(updatedMatch);
	}

	[HttpPatch("{id}/restore")]
	public async Task<ActionResult<Match>> Restore(
		string id,
		CancellationToken cancellationToken
	)
	{
		var updatedMatch = await _matchService.RestoreAsync(id, cancellationToken);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		return Ok(updatedMatch);
	}

	private static string? ValidateMatch(Match match)
	{
		if (string.IsNullOrWhiteSpace(match.Opponent))
		{
			return "Opponent is required.";
		}

		if (match.Date == default)
		{
			return "Match date is required.";
		}

		return null;
	}
}