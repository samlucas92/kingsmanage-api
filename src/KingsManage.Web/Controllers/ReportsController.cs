using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
	private static readonly ClubEventType[] EventTypes =
	[
		ClubEventType.Match,
		ClubEventType.Training,
		ClubEventType.Social,
		ClubEventType.Meeting
	];

	private readonly IClubEventService eventService;
	private readonly ISeasonService seasonService;

	public ReportsController(
		IClubEventService eventService,
		ISeasonService seasonService)
	{
		this.eventService = eventService;
		this.seasonService = seasonService;
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

		var season = await seasonService.GetByIdAsync(parsedSeasonId, cancellationToken);

		if (season is null)
		{
			return NotFound("Season not found.");
		}

		var events = await eventService.GetAllAsync(cancellationToken);
		var completedEvents = events
			.Where(clubEvent =>
				clubEvent.StartDateTime <= DateTime.UtcNow &&
				clubEvent.StartDateTime >= season.StartDate &&
				clubEvent.StartDateTime <= season.EndDate &&
				(eventType is null || clubEvent.Type == eventType))
			.ToList();

		return Ok(BuildAvailabilityReport(completedEvents));
	}

	private static AvailabilityReportViewModel BuildAvailabilityReport(
		IReadOnlyList<ClubEvent> completedEvents)
	{
		var totals = BuildTotals(completedEvents);
		var totalResponses = (int)(totals.Available + totals.Declined + totals.Unanswered);

		return new AvailabilityReportViewModel
		{
			CompletedEvents = completedEvents.Count,
			TotalResponses = totalResponses,
			AvailablePercentage = totalResponses > 0
				? (int)Math.Round(totals.Available / totalResponses * 100)
				: 0,
			Totals = totals,
			Averages = BuildAverages(totals, completedEvents.Count),
			EventTypes = EventTypes
				.Select(type =>
				{
					var typeEvents = completedEvents
						.Where(clubEvent => clubEvent.Type == type)
						.ToList();
					var typeTotals = BuildTotals(typeEvents);

					return new EventTypeAvailabilityBreakdownViewModel
					{
						Type = type,
						CompletedEvents = typeEvents.Count,
						Totals = typeTotals,
						Averages = BuildAverages(typeTotals, typeEvents.Count)
					};
				})
				.ToList(),
			Months = completedEvents
				.GroupBy(clubEvent => new DateTime(
					clubEvent.StartDateTime.Year,
					clubEvent.StartDateTime.Month,
					1,
					0,
					0,
					0,
					DateTimeKind.Utc))
				.OrderBy(group => group.Key)
				.Select(group =>
				{
					var monthEvents = group.ToList();
					var monthTotals = BuildTotals(monthEvents);

					return new MonthlyAvailabilityBreakdownViewModel
					{
						Label = group.Key.ToString("MMM"),
						MonthStart = group.Key,
						CompletedEvents = monthEvents.Count,
						Totals = monthTotals,
						Averages = BuildAverages(monthTotals, monthEvents.Count)
					};
				})
				.ToList()
		};
	}

	private static AvailabilityStatusBreakdownViewModel BuildTotals(
		IEnumerable<ClubEvent> events)
	{
		var responses = events
			.SelectMany(clubEvent => clubEvent.AvailabilityResponses);

		return new AvailabilityStatusBreakdownViewModel
		{
			Available = responses.Count(response => response.Status == ClubEventAvailabilityStatus.Available),
			Declined = responses.Count(response => response.Status == ClubEventAvailabilityStatus.Declined),
			Unanswered = responses.Count(response => response.Status == ClubEventAvailabilityStatus.Unanswered)
		};
	}

	private static AvailabilityStatusBreakdownViewModel BuildAverages(
		AvailabilityStatusBreakdownViewModel totals,
		int eventCount)
	{
		return new AvailabilityStatusBreakdownViewModel
		{
			Available = Average(totals.Available, eventCount),
			Declined = Average(totals.Declined, eventCount),
			Unanswered = Average(totals.Unanswered, eventCount)
		};
	}

	private static double Average(double total, int count)
	{
		return count > 0 ? Math.Round(total / count, 1) : 0;
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
