using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface IReportsQueryService
{
	Task<AvailabilityReportViewModel?> GetAvailabilityAsync(
		Guid seasonId,
		ClubEventType? eventType,
		CancellationToken cancellationToken = default);

	Task<TeamPerformanceReportViewModel> GetTeamPerformanceAsync(
		ReportFilters filters,
		CancellationToken cancellationToken = default);

	Task<OverviewReportViewModel?> GetOverviewAsync(
		ReportFilters filters,
		CancellationToken cancellationToken = default);

	Task<PlayerReportsViewModel> GetPlayerReportsAsync(
		Guid seasonId,
		Guid? teamId,
		Guid? playerId,
		bool includeFriendlies = true,
		CancellationToken cancellationToken = default);

	Task<FinanceReportViewModel?> GetFinanceAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default);
}
