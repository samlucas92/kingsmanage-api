using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface IFinanceReportQueryService
{
	Task<FinanceReportViewModel?> GetAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default);
}
