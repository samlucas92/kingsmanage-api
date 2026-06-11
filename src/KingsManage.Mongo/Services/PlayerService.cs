using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class PlayerService : IPlayerService
{
	private readonly IMongoCollection<Player> _players;

	public PlayerService(MongoContext context)
	{
		_players = context.Database.GetCollection<Player>("players");
	}

	public async Task<IReadOnlyList<Player>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await _players
			.Find(_ => true)
			.SortBy(player => player.Name)
			.ToListAsync(cancellationToken);
	}

	public async Task<Player?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return await _players
			.Find(player => player.Id == id)
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

		await _players.InsertOneAsync(player, cancellationToken: cancellationToken);

		return player;
	}

	public async Task<Player?> UpdateAsync(
		Player player,
		CancellationToken cancellationToken = default
	)
	{
		player.Name = player.Name.Trim();
		player.UpdatedAt = DateTime.UtcNow;

		var result = await _players.ReplaceOneAsync(
			existingPlayer => existingPlayer.Id == player.Id,
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

		return await _players.FindOneAndUpdateAsync(
			player => player.Id == id,
			update,
			new FindOneAndUpdateOptions<Player>
			{
				ReturnDocument = ReturnDocument.After
			},
			cancellationToken
		);
	}
}
