using KingsManage;

namespace KingsManage.Tests.Fakes;

public sealed class FakeStatsService : IStatsService
{
	public List<Guid> RecalculatedSeasonIds { get; } = [];
	public List<PlayerSeasonStats> SeasonStats { get; } = [];
	public List<PlayerHistoricalStats> HistoricalStats { get; } = [];

	public Task<List<PlayerSeasonStats>> GetSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(
			SeasonStats
				.Where(stats => stats.SeasonId == seasonId)
				.ToList()
		);
	}

	public Task<List<PlayerSeasonStats>> GetAllSeasonStatsAsync(
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(SeasonStats.ToList());
	}

	public Task<List<PlayerSeasonStats>> GetPlayerSeasonStatsAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(
			SeasonStats
				.Where(stats => stats.PlayerId == playerId)
				.ToList()
		);
	}

	public Task<List<PlayerHistoricalStats>> GetHistoricalStatsAsync(
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(HistoricalStats.ToList());
	}

	public Task<PlayerHistoricalStats?> GetHistoricalStatsByPlayerIdAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(
			HistoricalStats.FirstOrDefault(stats => stats.PlayerId == playerId)
		);
	}

	public Task<PlayerHistoricalStats> UpsertHistoricalStatsAsync(
		PlayerHistoricalStats stats,
		CancellationToken cancellationToken = default
	)
	{
		var existingIndex = HistoricalStats.FindIndex(existingStats => existingStats.PlayerId == stats.PlayerId);

		if (existingIndex >= 0)
		{
			HistoricalStats[existingIndex] = stats;
		}
		else
		{
			HistoricalStats.Add(stats);
		}

		return Task.FromResult(stats);
	}

	public Task RecalculateSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		RecalculatedSeasonIds.Add(seasonId);
		return Task.CompletedTask;
	}
}
