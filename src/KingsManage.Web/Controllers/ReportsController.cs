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
	private readonly IMatchService matchService;
	private readonly ISeasonService seasonService;

	public ReportsController(
		IClubEventService eventService,
		IMatchService matchService,
		ISeasonService seasonService)
	{
		this.eventService = eventService;
		this.matchService = matchService;
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

		Guid? parsedTeamId = null;
		if (!string.IsNullOrWhiteSpace(teamId) && !teamId.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			if (!Guid.TryParse(teamId, out var teamGuid))
			{
				return BadRequest("Team id must be a valid GUID.");
			}

			parsedTeamId = teamGuid;
		}

		var matches = await matchService.GetBySeasonAsync(parsedSeasonId, cancellationToken);
		var completedMatches = matches
			.Where(match =>
				match.IsCompleted &&
				match.State != MatchState.Postponed &&
				match.Result is not null &&
				(parsedTeamId is null || (match.TeamId ?? DefaultClubTeams.FromLegacy(match.Team)) == parsedTeamId.Value) &&
				(string.IsNullOrWhiteSpace(competition) ||
					competition.Equals("all", StringComparison.OrdinalIgnoreCase) ||
					(match.Competition.Trim().Length == 0
						? "No competition"
						: match.Competition.Trim()).Equals(competition, StringComparison.OrdinalIgnoreCase)) &&
				(venue is null || match.Venue == venue.Value) &&
				(dateFrom is null || match.Date >= dateFrom.Value.Date) &&
				(dateTo is null || match.Date <= dateTo.Value.Date.AddDays(1).AddTicks(-1)))
			.OrderBy(match => match.Date)
			.ToList();

		return Ok(BuildTeamPerformanceReport(completedMatches));
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

	private static TeamPerformanceReportViewModel BuildTeamPerformanceReport(
		IReadOnlyList<Match> completedMatches)
	{
		return new TeamPerformanceReportViewModel
		{
			Summary = BuildResultBreakdown(completedMatches),
			HomeAway = new HomeAwayBreakdownViewModel
			{
				Home = BuildResultBreakdown(completedMatches.Where(match => match.Venue == MatchVenue.Home)),
				Away = BuildResultBreakdown(completedMatches.Where(match => match.Venue == MatchVenue.Away))
			},
			Months = completedMatches
				.GroupBy(match => new DateTime(
					match.Date.Year,
					match.Date.Month,
					1,
					0,
					0,
					0,
					DateTimeKind.Utc))
				.OrderBy(group => group.Key)
				.Select(group =>
				{
					var monthMatches = group.ToList();
					var monthBreakdown = BuildResultBreakdown(monthMatches);

					return new MonthlyResultBreakdownViewModel
					{
						Label = group.Key.ToString("MMM"),
						MonthStart = group.Key,
						Wins = monthBreakdown.Won,
						Draws = monthBreakdown.Drawn,
						Losses = monthBreakdown.Lost,
						GoalsFor = monthBreakdown.GoalsFor,
						GoalsAgainst = monthBreakdown.GoalsAgainst
					};
				})
				.ToList(),
			RecentForm = completedMatches
				.TakeLast(10)
				.Select(match =>
				{
					var goals = GetClubGoals(match);

					if (goals.For > goals.Against) return "W";
					return goals.For == goals.Against ? "D" : "L";
				})
				.ToList()
		};
	}

	private static ResultBreakdownViewModel BuildResultBreakdown(
		IEnumerable<Match> matches)
	{
		var played = 0;
		var won = 0;
		var drawn = 0;
		var lost = 0;
		var goalsFor = 0;
		var goalsAgainst = 0;

		foreach (var match in matches.Where(match => match.Result is not null))
		{
			var goals = GetClubGoals(match);
			played += 1;
			goalsFor += goals.For;
			goalsAgainst += goals.Against;

			if (goals.For > goals.Against)
			{
				won += 1;
			}
			else if (goals.For == goals.Against)
			{
				drawn += 1;
			}
			else
			{
				lost += 1;
			}
		}

		return new ResultBreakdownViewModel
		{
			Played = played,
			Won = won,
			Drawn = drawn,
			Lost = lost,
			GoalsFor = goalsFor,
			GoalsAgainst = goalsAgainst,
			GoalDifference = goalsFor - goalsAgainst,
			WinPercentage = played > 0 ? Math.Round((double)won / played * 100, 1) : 0,
			AverageGoalsFor = played > 0 ? Math.Round((double)goalsFor / played, 2) : 0,
			AverageGoalsAgainst = played > 0 ? Math.Round((double)goalsAgainst / played, 2) : 0
		};
	}

	private static (int For, int Against) GetClubGoals(Match match)
	{
		if (match.Result is null)
		{
			return (0, 0);
		}

		return match.Venue == MatchVenue.Home
			? (match.Result.HomeGoals, match.Result.AwayGoals)
			: (match.Result.AwayGoals, match.Result.HomeGoals);
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
