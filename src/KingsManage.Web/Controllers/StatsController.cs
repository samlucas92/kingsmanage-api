using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/stats")]
public class StatsController : ControllerBase
{
	private readonly IPlayerStatsQueryService playerStatsQueryService;
	private readonly IPlayerService playerService;
	private readonly ISeasonRolloverService? seasonRolloverService;
	private readonly IStatsService statsService;

	public StatsController(
		IPlayerStatsQueryService playerStatsQueryService,
		IPlayerService playerService,
		IStatsService statsService,
		ISeasonRolloverService? seasonRolloverService = null
	)
	{
		this.playerStatsQueryService = playerStatsQueryService;
		this.playerService = playerService;
		this.statsService = statsService;
		this.seasonRolloverService = seasonRolloverService;
	}

	[HttpGet("season/{seasonId}")]
	public async Task<ActionResult<List<PlayerStatsViewModel>>> GetSeasonStats(
		string seasonId,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		var viewModels = await playerStatsQueryService.BuildRowsAsync(
			parsedSeasonId,
			includeFriendlies: false,
			cancellationToken);

		return Ok(viewModels);
	}

	[HttpPost("season/{seasonId}/recalculate")]
	public async Task<IActionResult> RecalculateSeasonStats(
		string seasonId,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		await statsService.RecalculateSeasonStatsAsync(
			parsedSeasonId,
			cancellationToken
		);

		return NoContent();
	}

	[HttpGet("season/{seasonId}/rollover")]
	public async Task<ActionResult<SeasonRolloverPreviewViewModel>> GetSeasonRolloverPreview(
		string seasonId,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		if (seasonRolloverService is null)
		{
			return Problem("Season rollover is unavailable.");
		}

		var preview = await seasonRolloverService.GetPreviewAsync(
			parsedSeasonId,
			cancellationToken);

		return preview is null ? NotFound() : Ok(preview);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost("season/{seasonId}/rollover")]
	public async Task<ActionResult<SeasonRolloverPreviewViewModel>> RollOverSeason(
		string seasonId,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		if (seasonRolloverService is null)
		{
			return Problem("Season rollover is unavailable.");
		}

		try
		{
			var preview = await seasonRolloverService.RollOverAsync(
				parsedSeasonId,
				cancellationToken);

			return preview is null ? NotFound() : Ok(preview);
		}
		catch (InvalidOperationException exception)
		{
			return BadRequest(new { message = exception.Message });
		}
	}

	[HttpGet("season/{seasonId}/players/{playerId}/breakdown")]
	public async Task<ActionResult<PlayerStatsBreakdownViewModel>> GetPlayerStatsBreakdown(
		string seasonId,
		string playerId,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var seasonError))
		{
			return seasonError!;
		}

		if (!TryParseGuid(playerId, "Player", out var parsedPlayerId, out var playerError))
		{
			return playerError!;
		}

		if (seasonRolloverService is null)
		{
			return Problem("Stats breakdown is unavailable.");
		}

		var breakdown = await seasonRolloverService.GetPlayerBreakdownAsync(
			parsedSeasonId,
			parsedPlayerId,
			cancellationToken);

		return breakdown is null ? NotFound() : Ok(breakdown);
	}

	[HttpGet("historical")]
	public async Task<ActionResult<List<PlayerHistoricalStats>>> GetHistoricalStats(
		CancellationToken cancellationToken
	)
	{
		var historicalStats = await statsService.GetHistoricalStatsAsync(cancellationToken);

		return Ok(historicalStats);
	}

	[HttpPut("historical/{playerId}")]
	public async Task<ActionResult<PlayerHistoricalStats>> UpdateHistoricalStats(
		string playerId,
		HistoricalStatsUpdateModel model,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(playerId, "Player", out var parsedPlayerId, out var errorResult))
		{
			return errorResult!;
		}

		var player = await playerService.GetByIdAsync(parsedPlayerId, cancellationToken);
		if (player is null)
		{
			return NotFound();
		}

		if (model.Appearances < 0 || model.Goals < 0)
		{
			return BadRequest("Historical stats cannot be negative.");
		}

		var stats = await statsService.UpsertHistoricalStatsAsync(
			new PlayerHistoricalStats
			{
				PlayerId = parsedPlayerId,
				Appearances = model.Appearances,
				Goals = model.Goals
			},
			cancellationToken
		);

		return Ok(stats);
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
