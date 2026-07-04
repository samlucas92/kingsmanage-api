using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class PlayerService : IPlayerService
{
	private readonly IMongoCollection<Player> players;
	private readonly TenantMongoScope tenant;

	public PlayerService(MongoContext context, TenantMongoScope tenant)
	{
		players = context.Database.GetCollection<Player>("players");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<Player>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await players
			.Find(tenant.Filter<Player>())
			.SortBy(player => player.Name)
			.ToListAsync(cancellationToken);
	}

	public async Task<Player?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return await players
			.Find(tenant.Filter<Player>(player => player.Id == id))
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<Player> CreateAsync(
		Player player,
		CancellationToken cancellationToken = default
	)
	{
		player.Id = player.Id == Guid.Empty
			? Guid.NewGuid()
			: player.Id;

		player.Name = player.Name.Trim();
		player.CreatedAt = DateTime.UtcNow;
		player.UpdatedAt = DateTime.UtcNow;
		tenant.Assign(player);

		await players.InsertOneAsync(player, cancellationToken: cancellationToken);

		return player;
	}

	public async Task<Player?> UpdateAsync(
		Player player,
		CancellationToken cancellationToken = default
	)
	{
		player.Name = player.Name.Trim();
		player.UpdatedAt = DateTime.UtcNow;
		tenant.Assign(player);

		var result = await players.ReplaceOneAsync(
			tenant.Filter<Player>(existingPlayer => existingPlayer.Id == player.Id),
			player,
			cancellationToken: cancellationToken
		);

		if (result.MatchedCount == 0)
		{
			return null;
		}

		return player;
	}

	public async Task<Player?> SetActiveAsync(
		Guid id,
		bool isActive,
		CancellationToken cancellationToken = default
	)
	{
		var update = Builders<Player>.Update
			.Set(player => player.IsActive, isActive)
			.Set(player => player.UpdatedAt, DateTime.UtcNow);

		return await players.FindOneAndUpdateAsync(
			tenant.Filter<Player>(player => player.Id == id),
			update,
			new FindOneAndUpdateOptions<Player>
			{
				ReturnDocument = ReturnDocument.After
			},
			cancellationToken
		);
	}
}
