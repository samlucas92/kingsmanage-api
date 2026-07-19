using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface IPlayerReportsQueryService
{
	Task<PlayerReportsViewModel> GetAsync(
		Guid seasonId,
		Guid? teamId,
		Guid? playerId,
		bool includeFriendlies = true,
		CancellationToken cancellationToken = default);

	Task<List<PlayerContributionViewModel>> GetTopContributorsAsync(
		Guid seasonId,
		int limit,
		bool includeFriendlies = true,
		CancellationToken cancellationToken = default);
}
