using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class AvailabilityReportQueryService : IAvailabilityReportQueryService
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

	public AvailabilityReportQueryService(
		IClubEventService eventService,
		ISeasonService seasonService)
	{
		this.eventService = eventService;
		this.seasonService = seasonService;
	}

	public async Task<AvailabilityReportViewModel?> GetAsync(
		Guid seasonId,
		ClubEventType? eventType,
		CancellationToken cancellationToken = default)
	{
		var season = await seasonService.GetByIdAsync(seasonId, cancellationToken);

		if (season is null)
		{
			return null;
		}

		var events = await eventService.GetAllAsync(cancellationToken);
		var completedEvents = events
			.Where(clubEvent =>
				clubEvent.StartDateTime <= DateTime.UtcNow &&
				clubEvent.StartDateTime >= season.StartDate &&
				clubEvent.StartDateTime <= season.EndDate &&
				(eventType is null || clubEvent.Type == eventType))
			.ToList();

		return BuildReport(completedEvents);
	}

	private static AvailabilityReportViewModel BuildReport(
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
				.GroupBy(clubEvent => ReportDate.MonthStart(clubEvent.StartDateTime))
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
			.SelectMany(clubEvent => clubEvent.AvailabilityResponses)
			.ToList();

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
}
