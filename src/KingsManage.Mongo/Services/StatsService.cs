using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class StatsService : IStatsService
{
	private readonly IMongoClient _client;
	private readonly IMongoCollection<Match> _matches;
	private readonly IMongoCollection<PlayerSeasonStats> _playerSeasonStats;
	private readonly IMongoCollection<PlayerHistoricalStats> _playerHistoricalStats;

	public StatsService(MongoContext context)
	{
		_client = context.Database.Client;
		_matches = context.Database.GetCollection<Match>("matches");
		_playerSeasonStats = context.Database.GetCollection<PlayerSeasonStats>("playerSeasonStats");
		_playerHistoricalStats = context.Database.GetCollection<PlayerHistoricalStats>("playerHistoricalStats");
	}

	public async Task<List<PlayerSeasonStats>> GetSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		return await _playerSeasonStats
			.Find(stats => stats.SeasonId == seasonId)
			.ToListAsync(cancellationToken);
	}

	public async Task<List<PlayerSeasonStats>> GetAllSeasonStatsAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await _playerSeasonStats
			.Find(_ => true)
			.ToListAsync(cancellationToken);
	}

	public async Task<List<PlayerSeasonStats>> GetPlayerSeasonStatsAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return await _playerSeasonStats
			.Find(stats => stats.PlayerId == playerId)
			.ToListAsync(cancellationToken);
	}

	public async Task<List<PlayerHistoricalStats>> GetHistoricalStatsAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await _playerHistoricalStats
			.Find(_ => true)
			.ToListAsync(cancellationToken);
	}

	public async Task<PlayerHistoricalStats?> GetHistoricalStatsByPlayerIdAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return await _playerHistoricalStats
			.Find(stats => stats.PlayerId == playerId)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<PlayerHistoricalStats> UpsertHistoricalStatsAsync(
		PlayerHistoricalStats stats,
		CancellationToken cancellationToken = default
	)
	{
		var existingStats = await GetHistoricalStatsByPlayerIdAsync(
			stats.PlayerId,
			cancellationToken
		);

		stats.Appearances = NormaliseStat(stats.Appearances);
		stats.Goals = NormaliseStat(stats.Goals);
		stats.UpdatedAt = DateTime.UtcNow;

		if (existingStats is not null)
		{
			stats.Id = existingStats.Id;
			stats.CreatedAt = existingStats.CreatedAt;
		}
		else if (stats.Id == Guid.Empty)
		{
			stats.Id = Guid.NewGuid();
			stats.CreatedAt = DateTime.UtcNow;
		}

		await _playerHistoricalStats.ReplaceOneAsync(
			existingStats => existingStats.PlayerId == stats.PlayerId,
			stats,
			new ReplaceOptions { IsUpsert = true },
			cancellationToken
		);

		return stats;
	}

	public async Task RecalculateSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		var completedMatches = await _matches
			.Find(match => match.SeasonId == seasonId && match.IsCompleted)
			.ToListAsync(cancellationToken);

		var seasonStats = SeasonStatsCalculator.Calculate(seasonId, completedMatches);

		using var session = await _client.StartSessionAsync(
			cancellationToken: cancellationToken
		);

		await session.WithTransactionAsync(
			async (transactionSession, transactionCancellationToken) =>
			{
				await _playerSeasonStats.DeleteManyAsync(
					transactionSession,
					stats => stats.SeasonId == seasonId,
					new DeleteOptions(),
					transactionCancellationToken
				);

				if (seasonStats.Count > 0)
				{
					await _playerSeasonStats.InsertManyAsync(
						transactionSession,
						seasonStats,
						cancellationToken: transactionCancellationToken
					);
				}

				return true;
			},
			cancellationToken: cancellationToken
		);
	}

	private static int NormaliseStat(int value)
	{
		return Math.Max(value, 0);
	}

}
