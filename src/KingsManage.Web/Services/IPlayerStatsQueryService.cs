using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface IPlayerStatsQueryService
{
	Task<List<PlayerStatsViewModel>> BuildRowsAsync(
		Guid seasonId,
		bool includeFriendlies = true,
		CancellationToken cancellationToken = default);
}
