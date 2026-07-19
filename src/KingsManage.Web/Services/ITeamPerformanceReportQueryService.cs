using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface ITeamPerformanceReportQueryService
{
	Task<TeamPerformanceReportViewModel> GetAsync(
		ReportFilters filters,
		CancellationToken cancellationToken = default);
}
