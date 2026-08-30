using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
	private readonly IMatchQueryService matchQueryService;
	private readonly IMatchService matchService;
	private readonly IStatsService statsService;
	private readonly IClubEventService eventService;

	public MatchesController(
		IMatchQueryService matchQueryService,
		IMatchService matchService,
		IStatsService statsService,
		IClubEventService eventService
	)
	{
		this.matchQueryService = matchQueryService;
		this.matchService = matchService;
		this.statsService = statsService;
		this.eventService = eventService;
	}

	[HttpPost("bulk-import")]
	public async Task<ActionResult<BulkMatchImportResultModel>> BulkImport(
		BulkMatchImportModel model,
		CancellationToken cancellationToken
	)
	{
		model.Matches ??= [];
		var validationError = await ValidateBulkImportAsync(model, cancellationToken);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var createdMatches = new List<Match>();
		var createdEvents = new List<ClubEvent>();

		try
		{
			foreach (var item in model.Matches)
			{
				var eventId = model.CreateEvents ? Guid.NewGuid() : (Guid?)null;
				var createdMatch = await matchService.CreateAsync(
					item.ToMatch(model.SeasonId, eventId),
					cancellationToken
				);
				createdMatches.Add(createdMatch);

				if (!model.CreateEvents || !eventId.HasValue)
				{
					continue;
				}

				var createdEvent = await eventService.CreateAsync(
					BuildImportedMatchEvent(item, createdMatch, eventId.Value),
					cancellationToken
				);
				createdEvents.Add(createdEvent);
			}
		}
		catch
		{
			foreach (var clubEvent in createdEvents.AsEnumerable().Reverse())
			{
				await eventService.DeleteAsync(clubEvent.Id, CancellationToken.None);
			}

			foreach (var match in createdMatches.AsEnumerable().Reverse())
			{
				await matchService.DeleteAsync(match.Id, CancellationToken.None);
			}

			throw;
		}

		await statsService.RecalculateSeasonStatsAsync(model.SeasonId, cancellationToken);

		return Ok(new BulkMatchImportResultModel
		{
			MatchCount = createdMatches.Count,
			EventCount = createdEvents.Count
		});
	}

	[HttpGet]
	public async Task<ActionResult<List<MatchViewModel>>> GetAll(
		[FromQuery] string? seasonId,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseOptionalGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		return Ok(await matchQueryService.GetMatchesAsync(
			parsedSeasonId,
			cancellationToken));
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

		if (!TryParseOptionalGuid(seasonId, "Season", out var parsedSeasonId, out var seasonErrorResult))
		{
			return seasonErrorResult!;
		}

		return Ok(await matchQueryService.GetPlayerMatchesAsync(
			parsedPlayerId,
			parsedSeasonId,
			cancellationToken));
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

		var match = await matchQueryService.GetByIdAsync(matchId, cancellationToken);

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

		var createdMatch = await matchService.CreateAsync(match, cancellationToken);
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

		var existingMatch = await matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		match.Id = matchId;
		match.CreatedAt = existingMatch.CreatedAt;

		var updatedMatch = await matchService.UpdateAsync(match, cancellationToken);

		if (updatedMatch is null)
		{
			return NotFound();
		}

		await RecalculateAffectedSeasonsAsync(existingMatch, updatedMatch, cancellationToken);

		return Ok(updatedMatch);
	}

	private static string? ValidatePlayerStats(Match match, List<MatchPlayerStats> playerStats)
	{
		if (playerStats.Any(stats => stats.PlayerId == Guid.Empty))
		{
			return "Every player report must have a valid player id.";
		}

		if (playerStats.GroupBy(stats => stats.PlayerId).Any(group => group.Count() > 1))
		{
			return "A player can only appear once in a match report.";
		}

		var selectedPlayerIds = match.SelectedPlayers
			.Select(selectedPlayer => selectedPlayer.PlayerId)
			.ToHashSet();
		if (playerStats.Any(stats => !selectedPlayerIds.Contains(stats.PlayerId)))
		{
			return "Match reports can only contain players selected in the lineup.";
		}

		if (playerStats.Any(stats =>
			stats.Goals < 0 ||
			stats.Assists < 0 ||
			stats.YellowCards < 0 ||
			stats.RedCards < 0 ||
			stats.Minutes is < 0 or > 180
		))
		{
			return "Stats cannot be negative and minutes cannot exceed 180.";
		}

		if (playerStats.Any(stats => !Enum.IsDefined(stats.AppearanceType)))
		{
			return "The appearance type is invalid.";
		}

		if (playerStats.Any(stats =>
			stats.AppearanceType == MatchAppearanceType.UnusedSubstitute &&
			(stats.Goals > 0 ||
			 stats.Assists > 0 ||
			 stats.YellowCards > 0 ||
			 stats.RedCards > 0 ||
			 stats.Minutes > 0 ||
			 stats.IsMOTM)
		))
		{
			return "Unused substitutes cannot have playing stats.";
		}

		return null;
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

		var existingMatch = await matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var deleted = await matchService.DeleteAsync(matchId, cancellationToken);

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

		var existingMatch = await matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await matchService.SetSelectedPlayersAsync(
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

		var updatedMatch = await matchService.SetLineupFormationAsync(
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

	[HttpPut("{id}/formation-key")]
	public async Task<ActionResult<Match>> SetFormationKey(string id, SetFormationKeyRequest request, CancellationToken cancellationToken)
	{
		if (!TryParseGuid(id, "Match", out var matchId, out var errorResult))
		{
			return errorResult!;
		}
		if (string.IsNullOrWhiteSpace(request.FormationKey))
		{
			return BadRequest("Formation key is required.");
		}
		var updatedMatch = await matchService.SetLineupFormationKeyAsync(matchId, request.FormationKey, cancellationToken);
		return updatedMatch is null ? NotFound() : Ok(updatedMatch);
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

		var updatedMatch = await matchService.ToggleLineupLockedAsync(
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

		var existingMatch = await matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await matchService.SetResultAsync(
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

		var existingMatch = await matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await matchService.ClearResultAsync(
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

		var existingMatch = await matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var validationError = ValidatePlayerStats(existingMatch, playerStats);
		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var updatedMatch = await matchService.UpdatePlayerStatsAsync(
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

		var updatedMatch = await matchService.UpdateNotesAsync(
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

		if (model.NewDate.Kind != DateTimeKind.Utc)
		{
			return BadRequest("New date must include a UTC timezone.");
		}

		var existingMatch = await matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await matchService.PostponeAsync(
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

		var existingMatch = await matchService.GetByIdAsync(matchId, cancellationToken);

		if (existingMatch is null)
		{
			return NotFound();
		}

		var updatedMatch = await matchService.RestoreAsync(matchId, cancellationToken);

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
			await statsService.RecalculateSeasonStatsAsync(
				seasonId,
				cancellationToken
			);
		}
	}

	private async Task<string?> ValidateBulkImportAsync(
		BulkMatchImportModel model,
		CancellationToken cancellationToken
	)
	{
		if (model.SeasonId == Guid.Empty)
		{
			return "Season is required.";
		}

		if (model.Matches.Count == 0)
		{
			return "Add at least one match to import.";
		}

		if (model.Matches.Count > 100)
		{
			return "A maximum of 100 matches can be imported at once.";
		}

		for (var index = 0; index < model.Matches.Count; index++)
		{
			var item = model.Matches[index];

			if (item.TeamId == Guid.Empty)
			{
				return $"Row {index + 1}: Team is required.";
			}

			var matchValidationError = ValidateMatch(item.ToMatch(model.SeasonId));

			if (matchValidationError is not null)
			{
				return $"Row {index + 1}: {matchValidationError}";
			}
		}

		var duplicateRows = model.Matches
			.Select((match, index) => new
			{
				Index = index,
				Key = BuildImportDuplicateKey(match.TeamId, match.Opponent, match.Date)
			})
			.GroupBy(item => item.Key)
			.FirstOrDefault(group => group.Count() > 1);

		if (duplicateRows is not null)
		{
			var rows = string.Join(", ", duplicateRows.Select(item => item.Index + 1));
			return $"Rows {rows} describe the same match.";
		}

		var existingMatches = await matchQueryService.GetMatchesAsync(
			model.SeasonId,
			cancellationToken
		);
		var existingKeys = existingMatches
			.Select(match => BuildImportDuplicateKey(match.TeamId, match.Opponent, match.Date))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		for (var index = 0; index < model.Matches.Count; index++)
		{
			var item = model.Matches[index];

			if (existingKeys.Contains(BuildImportDuplicateKey(item.TeamId, item.Opponent, item.Date)))
			{
				return $"Row {index + 1}: This match already exists in the selected season.";
			}
		}

		return null;
	}

	private static string BuildImportDuplicateKey(
		Guid teamId,
		string opponent,
		DateTime date
	)
	{
		return $"{teamId:N}|{opponent.Trim().ToUpperInvariant()}|{date:O}";
	}

	private static ClubEvent BuildImportedMatchEvent(
		BulkMatchImportItemModel item,
		Match match,
		Guid eventId
	)
	{
		var teamName = string.IsNullOrWhiteSpace(item.TeamName)
			? "Match"
			: item.TeamName.Trim();
		var fixtureTitle = item.Venue == MatchVenue.Home
			? $"{teamName} vs {item.Opponent.Trim()}"
			: $"{teamName} at {item.Opponent.Trim()}";

		return new ClubEvent
		{
			Id = eventId,
			Type = ClubEventType.Match,
			TeamScope = item.Team == ClubTeam.Second
				? ClubEventTeamScope.Second
				: ClubEventTeamScope.First,
			TeamIds = [item.TeamId],
			Title = fixtureTitle,
			Description = item.Competition.Trim(),
			StartDateTime = item.Date,
			EndDateTime = item.Date.AddHours(2),
			Location = item.Location.Trim(),
			MatchLinks =
			[
				new ClubEventMatchLink
				{
					TeamId = item.TeamId,
					Team = item.Team,
					MatchId = match.Id
				}
			]
		};
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

		if (match.Date.Kind != DateTimeKind.Utc)
		{
			return "Match date must include a UTC timezone.";
		}

		if (string.IsNullOrWhiteSpace(match.Competition))
		{
			return "Competition is required.";
		}

		if (string.IsNullOrWhiteSpace(match.Location))
		{
			return "Location is required.";
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

	private bool TryParseOptionalGuid(
		string? id,
		string entityName,
		out Guid? parsedId,
		out BadRequestObjectResult? errorResult
	)
	{
		parsedId = null;
		errorResult = null;

		if (string.IsNullOrWhiteSpace(id))
		{
			return true;
		}

		if (!Guid.TryParse(id, out var value))
		{
			errorResult = BadRequest($"{entityName} id must be a valid GUID.");
			return false;
		}

		parsedId = value;
		return true;
	}
}
