using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
	private readonly IReportsQueryService reportsQueryService;

	public ReportsController(IReportsQueryService reportsQueryService)
	{
		this.reportsQueryService = reportsQueryService;
	}

	[HttpGet("availability")]
	public async Task<ActionResult<AvailabilityReportViewModel>> GetAvailability(
		[FromQuery] string seasonId,
		[FromQuery] ClubEventType? eventType,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		var report = await reportsQueryService.GetAvailabilityAsync(
			parsedSeasonId,
			eventType,
			cancellationToken);

		return report is null
			? NotFound("Season not found.")
			: Ok(report);
	}

	[HttpGet("team-performance")]
	public async Task<ActionResult<TeamPerformanceReportViewModel>> GetTeamPerformance(
		[FromQuery] string seasonId,
		[FromQuery] string? teamId,
		[FromQuery] string? competition,
		[FromQuery] MatchVenue? venue,
		[FromQuery] DateTime? dateFrom,
		[FromQuery] DateTime? dateTo,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		if (!TryParseOptionalGuid(teamId, "Team", out var parsedTeamId, out errorResult))
		{
			return errorResult!;
		}

		var report = await reportsQueryService.GetTeamPerformanceAsync(
			new ReportFilters(
				parsedSeasonId,
				parsedTeamId,
				competition,
				venue,
				dateFrom,
				dateTo),
			cancellationToken);

		return Ok(report);
	}

	[HttpGet("overview")]
	public async Task<ActionResult<OverviewReportViewModel>> GetOverview(
		[FromQuery] string seasonId,
		[FromQuery] string? teamId,
		[FromQuery] string? competition,
		[FromQuery] MatchVenue? venue,
		[FromQuery] DateTime? dateFrom,
		[FromQuery] DateTime? dateTo,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		var report = await reportsQueryService.GetOverviewAsync(
			new ReportFilters(
				parsedSeasonId,
				ParseOptionalGuid(teamId),
				competition,
				venue,
				dateFrom,
				dateTo),
			cancellationToken);

		return report is null
			? NotFound("Season not found.")
			: Ok(report);
	}

	[HttpGet("players")]
	public async Task<ActionResult<PlayerReportsViewModel>> GetPlayerReports(
		[FromQuery] string seasonId,
		[FromQuery] string? teamId,
		[FromQuery] string? playerId,
		CancellationToken cancellationToken,
		[FromQuery] bool includeFriendlies = true)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		var report = await reportsQueryService.GetPlayerReportsAsync(
			parsedSeasonId,
			ParseOptionalGuid(teamId),
			ParseOptionalGuid(playerId),
			includeFriendlies,
			cancellationToken);

		return Ok(report);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpGet("finance")]
	public async Task<ActionResult<FinanceReportViewModel>> GetFinance(
		[FromQuery] string seasonId,
		CancellationToken cancellationToken)
	{
		if (!TryParseGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		var report = await reportsQueryService.GetFinanceAsync(
			parsedSeasonId,
			cancellationToken);

		return report is null
			? NotFound("Season not found.")
			: Ok(report);
	}

	private static Guid? ParseOptionalGuid(string? id)
	{
		if (string.IsNullOrWhiteSpace(id) || id.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		return Guid.TryParse(id, out var parsedId) ? parsedId : null;
	}

	private bool TryParseOptionalGuid(
		string? id,
		string entityName,
		out Guid? parsedId,
		out BadRequestObjectResult? errorResult)
	{
		parsedId = null;
		errorResult = null;

		if (string.IsNullOrWhiteSpace(id) || id.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (!Guid.TryParse(id, out var parsedGuid))
		{
			errorResult = BadRequest($"{entityName} id must be a valid GUID.");
			return false;
		}

		parsedId = parsedGuid;
		return true;
	}

	private bool TryParseGuid(
		string id,
		string entityName,
		out Guid parsedId,
		out BadRequestObjectResult? errorResult)
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
