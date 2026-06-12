namespace KingsManage;

public interface IStatsService
{
	Task<IReadOnlyList<PlayerSeasonStats>> GetSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<PlayerSeasonStats>> GetAllSeasonStatsAsync(
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<PlayerSeasonStats>> GetPlayerSeasonStatsAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<PlayerHistoricalStats>> GetHistoricalStatsAsync(
		CancellationToken cancellationToken = default
	);

	Task<PlayerHistoricalStats?> GetHistoricalStatsByPlayerIdAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	);

	Task<PlayerHistoricalStats> UpsertHistoricalStatsAsync(
		PlayerHistoricalStats stats,
		CancellationToken cancellationToken = default
	);

	Task RecalculateSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	);
}
