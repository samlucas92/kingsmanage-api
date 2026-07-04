using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class StatsService : IStatsService
{
	private readonly IMongoClient client;
	private readonly IMongoCollection<Match> matches;
	private readonly IMongoCollection<PlayerSeasonStats> playerSeasonStats;
	private readonly IMongoCollection<PlayerHistoricalStats> playerHistoricalStats;
	private readonly TenantMongoScope tenant;

	public StatsService(MongoContext context, TenantMongoScope tenant)
	{
		client = context.Database.Client;
		matches = context.Database.GetCollection<Match>("matches");
		playerSeasonStats = context.Database.GetCollection<PlayerSeasonStats>("playerSeasonStats");
		playerHistoricalStats = context.Database.GetCollection<PlayerHistoricalStats>("playerHistoricalStats");
		this.tenant = tenant;
	}

	public async Task<List<PlayerSeasonStats>> GetSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		return await playerSeasonStats
			.Find(tenant.Filter<PlayerSeasonStats>(stats => stats.SeasonId == seasonId))
			.ToListAsync(cancellationToken);
	}

	public async Task<List<PlayerSeasonStats>> GetAllSeasonStatsAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await playerSeasonStats
			.Find(tenant.Filter<PlayerSeasonStats>())
			.ToListAsync(cancellationToken);
	}

	public async Task<List<PlayerSeasonStats>> GetPlayerSeasonStatsAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return await playerSeasonStats
			.Find(tenant.Filter<PlayerSeasonStats>(stats => stats.PlayerId == playerId))
			.ToListAsync(cancellationToken);
	}

	public async Task<List<PlayerHistoricalStats>> GetHistoricalStatsAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await playerHistoricalStats
			.Find(tenant.Filter<PlayerHistoricalStats>())
			.ToListAsync(cancellationToken);
	}

	public async Task<PlayerHistoricalStats?> GetHistoricalStatsByPlayerIdAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return await playerHistoricalStats
			.Find(tenant.Filter<PlayerHistoricalStats>(stats => stats.PlayerId == playerId))
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
		tenant.Assign(stats);

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

		await playerHistoricalStats.ReplaceOneAsync(
			tenant.Filter<PlayerHistoricalStats>(existingStats => existingStats.PlayerId == stats.PlayerId),
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
		var completedMatches = await matches
			.Find(tenant.Filter<Match>(match => match.SeasonId == seasonId && match.IsCompleted))
			.ToListAsync(cancellationToken);

		var seasonStats = SeasonStatsCalculator.Calculate(seasonId, completedMatches);
		seasonStats.ForEach(stats => tenant.Assign(stats));

		using var session = await client.StartSessionAsync(
			cancellationToken: cancellationToken
		);

		await session.WithTransactionAsync(
			async (transactionSession, transactionCancellationToken) =>
			{
				await playerSeasonStats.DeleteManyAsync(
					transactionSession,
					tenant.Filter<PlayerSeasonStats>(stats => stats.SeasonId == seasonId),
					new DeleteOptions(),
					transactionCancellationToken
				);

				if (seasonStats.Count > 0)
				{
					await playerSeasonStats.InsertManyAsync(
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
