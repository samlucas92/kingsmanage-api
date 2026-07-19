using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface IMatchQueryService
{
	Task<List<MatchViewModel>> GetMatchesAsync(
		Guid? seasonId,
		CancellationToken cancellationToken = default);

	Task<List<PlayerMatchViewModel>> GetPlayerMatchesAsync(
		Guid playerId,
		Guid? seasonId,
		CancellationToken cancellationToken = default);

	Task<Match?> GetByIdAsync(
		Guid matchId,
		CancellationToken cancellationToken = default);
}
