using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/stats")]
public class StatsController : ControllerBase
{
	private readonly IPlayerService playerService;
	private readonly IStatsService statsService;

	public StatsController(
		IPlayerService playerService,
		IStatsService statsService
	)
	{
		this.playerService = playerService;
		this.statsService = statsService;
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

		var players = await playerService.GetAllAsync(cancellationToken);
		var selectedSeasonStats = await statsService.GetSeasonStatsAsync(
			parsedSeasonId,
			cancellationToken
		);
		var allSeasonStats = await statsService.GetAllSeasonStatsAsync(cancellationToken);
		var historicalStats = await statsService.GetHistoricalStatsAsync(cancellationToken);

		var viewModels = players
			.OrderBy(player => player.Name)
			.Select(player => PlayerStatsViewModel.FromStats(
				player,
				selectedSeasonStats,
				allSeasonStats,
				historicalStats.FirstOrDefault(stats => stats.PlayerId == player.Id)
			))
			.ToList();

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
