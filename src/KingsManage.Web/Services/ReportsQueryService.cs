using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class ReportsQueryService : IReportsQueryService
{
	private readonly IAvailabilityReportQueryService availabilityReportQueryService;
	private readonly IFinanceReportQueryService financeReportQueryService;
	private readonly IPlayerReportsQueryService playerReportsQueryService;
	private readonly IPlayerService playerService;
	private readonly ISeasonService seasonService;
	private readonly ITeamPerformanceReportQueryService teamPerformanceReportQueryService;

	public ReportsQueryService(
		IAvailabilityReportQueryService availabilityReportQueryService,
		IFinanceReportQueryService financeReportQueryService,
		IPlayerReportsQueryService playerReportsQueryService,
		IPlayerService playerService,
		ISeasonService seasonService,
		ITeamPerformanceReportQueryService teamPerformanceReportQueryService)
	{
		this.availabilityReportQueryService = availabilityReportQueryService;
		this.financeReportQueryService = financeReportQueryService;
		this.playerReportsQueryService = playerReportsQueryService;
		this.playerService = playerService;
		this.seasonService = seasonService;
		this.teamPerformanceReportQueryService = teamPerformanceReportQueryService;
	}

	public Task<AvailabilityReportViewModel?> GetAvailabilityAsync(
		Guid seasonId,
		ClubEventType? eventType,
		CancellationToken cancellationToken = default)
	{
		return availabilityReportQueryService.GetAsync(
			seasonId,
			eventType,
			cancellationToken);
	}

	public Task<TeamPerformanceReportViewModel> GetTeamPerformanceAsync(
		ReportFilters filters,
		CancellationToken cancellationToken = default)
	{
		return teamPerformanceReportQueryService.GetAsync(
			filters,
			cancellationToken);
	}

	public async Task<OverviewReportViewModel?> GetOverviewAsync(
		ReportFilters filters,
		CancellationToken cancellationToken = default)
	{
		var season = await seasonService.GetByIdAsync(
			filters.SeasonId,
			cancellationToken);

		if (season is null)
		{
			return null;
		}

		var teamPerformance = await teamPerformanceReportQueryService.GetAsync(
			filters,
			cancellationToken);
		var availability = await availabilityReportQueryService.GetAsync(
			filters.SeasonId,
			eventType: null,
			cancellationToken);
		var players = await playerService.GetAllAsync(cancellationToken);
		var topContributors = await playerReportsQueryService.GetTopContributorsAsync(
			filters.SeasonId,
			limit: 5,
			cancellationToken);

		return new OverviewReportViewModel
		{
			TeamPerformance = teamPerformance,
			Availability = availability ?? new AvailabilityReportViewModel(),
			ActivePlayers = players.Count(player => player.IsActive),
			TopContributors = topContributors
		};
	}

	public Task<PlayerReportsViewModel> GetPlayerReportsAsync(
		Guid seasonId,
		Guid? teamId,
		Guid? playerId,
		bool includeFriendlies = true,
		CancellationToken cancellationToken = default)
	{
		return playerReportsQueryService.GetAsync(
			seasonId,
			teamId,
			playerId,
			includeFriendlies,
			cancellationToken);
	}

	public Task<FinanceReportViewModel?> GetFinanceAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default)
	{
		return financeReportQueryService.GetAsync(
			seasonId,
			cancellationToken);
	}
}
