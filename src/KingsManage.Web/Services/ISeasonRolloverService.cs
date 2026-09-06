using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface ISeasonRolloverService
{
	Task<SeasonRolloverPreviewViewModel?> GetPreviewAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default);

	Task<SeasonRolloverPreviewViewModel?> RollOverAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default);

	Task<PlayerStatsBreakdownViewModel?> GetPlayerBreakdownAsync(
		Guid seasonId,
		Guid playerId,
		CancellationToken cancellationToken = default);
}
