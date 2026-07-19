using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface IAvailabilityReportQueryService
{
	Task<AvailabilityReportViewModel?> GetAsync(
		Guid seasonId,
		ClubEventType? eventType,
		CancellationToken cancellationToken = default);
}
