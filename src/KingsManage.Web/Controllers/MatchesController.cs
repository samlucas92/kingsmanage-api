using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Coach")]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
	private readonly IMatchService _matchService;
	private readonly IStatsService _statsService;

	public MatchesController(
		IMatchService matchService,
		IStatsService statsService
	)
	{
		_matchService = matchService;
		_statsService = statsService;
	}

	[HttpGet]
	public async Task<ActionResult<List<MatchViewModel>>> GetAll(
		[FromQuery] string? seasonId,
		CancellationToken cancellationToken
	)
	{
		IReadOnlyList<Match> matches;

		if (!string.IsNullOrWhiteSpace(seasonId))
		{
			if (!Guid.TryParse(seasonId, out var parsedSeasonId))
			{
				return BadRequest("Season id must be a valid GUID.");
			}

			matches = await _matchService.GetBySeasonAsync(
				parsedSeasonId,
				cancellationToken
			);
		}
		else
		{
			matches = await _matchService.GetAllAsync(cancellationToken);
		}

		return Ok(matches.Select(MatchViewModel.FromMatch).ToList());
	}

	[HttpGet("player/{playerId}")]
	public async Task<ActionResult<List<PlayerMatchViewModel>>> GetPlayerMatches(
		string playerId,
		[FromQuery] string? seasonId,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(playerId, "Player", out var parsedPlayerId, out var playerErrorResult))
		{
			return playerErrorResult!;
		}

		IReadOnlyList<Match> matches;

		if (!string.IsNullOrWhiteSpace(seasonId))
		{
			if (!Guid.TryParse(seasonId, out var parsedSeasonId))
			{
				return BadRequest("Season id must be a valid GUID.");
			}

			matches = await _matchService.GetBySeasonAsync(
				parsedSeasonId,
				cancellationToken
			);
		}
		else
		{
			matches = await _matchService.GetAllAsync(cancellationToken);
		}

		var playerMatches = matches
			.Where(match =>
				match.IsCompleted &&
				match.SelectedPlayers.Any(selectedPlayer => selectedPlayer.PlayerId == parsedPlayerId)
			)
			.OrderByDescending(match => match.Date)
			.Select(match => PlayerMatchViewModel.FromMatch(match, parsedPlayerId))
			.ToList();

		return Ok(playerMatches);
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<Match>> GetById(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		var match = await _matchService.GetByIdAsync(matchId, cancellationToken);

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
		await RecalculateAffectedSeasonsAsync(null, createdMatch, cancellationToken);

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
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		var validationError = ValidateMatch(match);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var existingMatch = await _matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		match.Id = matchId;
		match.CreatedAt = existingMatch.CreatedAt;

		var updatedMatch = await _matchService.UpdateAsync(match, cancellationToken);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		await RecalculateAffectedSeasonsAsync(existingMatch, updatedMatch, cancellationToken);

		return Ok(updatedMatch);
	}

	[HttpDelete("{id}")]
	public async Task<IActionResult> Delete(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		var existingMatch = await _matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var deleted = await _matchService.DeleteAsync(matchId, cancellationToken);

		if (!deleted)
		{
			return NotFound();
		}

		await RecalculateAffectedSeasonsAsync(existingMatch, null, cancellationToken);

		return NoContent();
	}

	[HttpPut("{id}/lineup")]
	public async Task<ActionResult<Match>> SetLineup(
		string id,
		List<SelectedPlayer> selectedPlayers,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		var existingMatch = await _matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await _matchService.SetSelectedPlayersAsync(
			matchId,
			selectedPlayers,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		await RecalculateAffectedSeasonsAsync(existingMatch, updatedMatch, cancellationToken);

		return Ok(updatedMatch);
	}

	[HttpPut("{id}/formation")]
	public async Task<ActionResult<Match>> SetFormation(
		string id,
		LineupFormation formation,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		var updatedMatch = await _matchService.SetLineupFormationAsync(
			matchId,
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
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		var updatedMatch = await _matchService.ToggleLineupLockedAsync(
			matchId,
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
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		if (result.HomeGoals < 0 || result.AwayGoals < 0)
		{
			return BadRequest("Goals cannot be negative.");
		}

		var existingMatch = await _matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await _matchService.SetResultAsync(
			matchId,
			result,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		await RecalculateAffectedSeasonsAsync(existingMatch, updatedMatch, cancellationToken);

		return Ok(updatedMatch);
	}

	[HttpDelete("{id}/result")]
	public async Task<ActionResult<Match>> ClearResult(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		var existingMatch = await _matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await _matchService.ClearResultAsync(
			matchId,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		await RecalculateAffectedSeasonsAsync(existingMatch, updatedMatch, cancellationToken);

		return Ok(updatedMatch);
	}

	[HttpPut("{id}/player-stats")]
	public async Task<ActionResult<Match>> UpdatePlayerStats(
		string id,
		List<MatchPlayerStats> playerStats,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		var existingMatch = await _matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await _matchService.UpdatePlayerStatsAsync(
			matchId,
			playerStats,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		await RecalculateAffectedSeasonsAsync(existingMatch, updatedMatch, cancellationToken);

		return Ok(updatedMatch);
	}

	[HttpPut("{id}/notes")]
	public async Task<ActionResult<Match>> UpdateNotes(
		string id,
		MatchNotes notes,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		var updatedMatch = await _matchService.UpdateNotesAsync(
			matchId,
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
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		if (model.NewDate == default)
		{
			return BadRequest("New date is required.");
		}

		var existingMatch = await _matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await _matchService.PostponeAsync(
			matchId,
			model.NewDate,
			model.Reason,
			cancellationToken
		);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		await RecalculateAffectedSeasonsAsync(existingMatch, updatedMatch, cancellationToken);

		return Ok(updatedMatch);
	}

	[HttpPatch("{id}/restore")]
	public async Task<ActionResult<Match>> Restore(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}

		var existingMatch = await _matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await _matchService.RestoreAsync(matchId, cancellationToken);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		await RecalculateAffectedSeasonsAsync(existingMatch, updatedMatch, cancellationToken);

		return Ok(updatedMatch);
	}

	private async Task RecalculateAffectedSeasonsAsync(
		Match? existingMatch,
		Match? updatedMatch,
		CancellationToken cancellationToken
	)
	{
		var affectedSeasonIds = new[]
		{
			existingMatch?.SeasonId,
			updatedMatch?.SeasonId
		}
			.Where(seasonId => seasonId.HasValue)
			.Select(seasonId => seasonId!.Value)
			.Distinct()
			.ToList();

		foreach (var seasonId in affectedSeasonIds)
		{
			await _statsService.RecalculateSeasonStatsAsync(
				seasonId,
				cancellationToken
			);
		}
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

	private bool TryParseGuid(
		string id,
		string entityName,
		out Guid parsedId,
		out BadRequestObjectResult? errorResult
	)
	{
		parsedId = Guid.Empty;
		errorResult = null;

		if (string.IsNullOrWhiteSpace(id))
		{
			errorResult = BadRequest($"{entityName} id is required.");
			return false;
		}

		if (!Guid.TryParse(id, out parsedId))
		{
			errorResult = BadRequest($"{entityName} id must be a valid GUID.");
			return false;
		}

		return true;
	}
}
