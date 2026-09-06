using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class SeasonRolloverStore : ISeasonRolloverStore
{
	private readonly IMongoClient client;
	private readonly IMongoCollection<PlayerHistoricalStats> historicalStats;
	private readonly IMongoCollection<SeasonRollover> rollovers;
	private readonly TenantMongoScope tenant;

	public SeasonRolloverStore(MongoContext context, TenantMongoScope tenant)
	{
		client = context.Database.Client;
		historicalStats = context.Database.GetCollection<PlayerHistoricalStats>(
			"playerHistoricalStats");
		rollovers = context.Database.GetCollection<SeasonRollover>("seasonRollovers");
		this.tenant = tenant;
	}

	public async Task<SeasonRollover?> GetBySeasonIdAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default)
	{
		return await rollovers
			.Find(tenant.Filter<SeasonRollover>(rollover => rollover.SeasonId == seasonId))
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<SeasonRollover> ApplyAsync(
		SeasonRollover rollover,
		CancellationToken cancellationToken = default)
	{
		var existing = await GetBySeasonIdAsync(rollover.SeasonId, cancellationToken);
		if (existing is not null)
		{
			return existing;
		}

		rollover.Id = rollover.Id == Guid.Empty ? Guid.NewGuid() : rollover.Id;
		rollover.RolledOverAt = DateTime.UtcNow;
		tenant.Assign(rollover);

		using var session = await client.StartSessionAsync(
			cancellationToken: cancellationToken);

		return await session.WithTransactionAsync(
			async (transactionSession, transactionCancellationToken) =>
			{
				var transactionExisting = await rollovers
					.Find(
						transactionSession,
						tenant.Filter<SeasonRollover>(item => item.SeasonId == rollover.SeasonId))
					.FirstOrDefaultAsync(transactionCancellationToken);

				if (transactionExisting is not null)
				{
					return transactionExisting;
				}

				foreach (var player in rollover.Players)
				{
					var filter = tenant.Filter<PlayerHistoricalStats>(
						stats => stats.PlayerId == player.PlayerId);
					var update = Builders<PlayerHistoricalStats>.Update
						.Set(stats => stats.Appearances, player.CareerAppsAfter)
						.Set(stats => stats.Goals, player.CareerGoalsAfter)
						.Set(stats => stats.UpdatedAt, rollover.RolledOverAt)
						.SetOnInsert(stats => stats.Id, Guid.NewGuid())
						.SetOnInsert(stats => stats.OrganizationId, rollover.OrganizationId)
						.SetOnInsert(stats => stats.ClubId, rollover.ClubId)
						.SetOnInsert(stats => stats.PlayerId, player.PlayerId)
						.SetOnInsert(stats => stats.CreatedAt, rollover.RolledOverAt);

					await historicalStats.UpdateOneAsync(
						transactionSession,
						filter,
						update,
						new UpdateOptions { IsUpsert = true },
						transactionCancellationToken);
				}

				await rollovers.InsertOneAsync(
					transactionSession,
					rollover,
					cancellationToken: transactionCancellationToken);

				return rollover;
			},
			cancellationToken: cancellationToken);
	}
}
