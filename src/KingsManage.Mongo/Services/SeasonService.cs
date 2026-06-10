using KingsManage;
using KingsManage.Mongo;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class SeasonService : ISeasonService
{
	private readonly IMongoCollection<Season> _seasons;

	public SeasonService(MongoContext context)
	{
		_seasons = context.Database.GetCollection<Season>("seasons");
	}

	public async Task<IReadOnlyList<Season>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await _seasons
			.Find(_ => true)
			.SortByDescending(season => season.StartDate)
			.ToListAsync(cancellationToken);
	}

	public async Task<Season?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken = default
	)
	{
		return await _seasons
			.Find(season => season.Id == id)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<Season?> GetActiveAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await _seasons
			.Find(season => season.IsActive)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<Season> CreateAsync(
		Season season,
		CancellationToken cancellationToken = default
	)
	{
		season.Id = string.IsNullOrWhiteSpace(season.Id)
			? Guid.NewGuid().ToString()
			: season.Id;

		season.Name = season.Name.Trim();
		season.CreatedAt = DateTime.UtcNow;
		season.UpdatedAt = DateTime.UtcNow;

		if (season.IsActive)
		{
			await DeactivateAllAsync(cancellationToken);
		}

		await _seasons.InsertOneAsync(season, cancellationToken: cancellationToken);

		return season;
	}

	public async Task<Season?> UpdateAsync(
		Season season,
		CancellationToken cancellationToken = default
	)
	{
		season.Name = season.Name.Trim();
		season.UpdatedAt = DateTime.UtcNow;

		if (season.IsActive)
		{
			await DeactivateAllExceptAsync(season.Id, cancellationToken);
		}

		var result = await _seasons.ReplaceOneAsync(
			existingSeason => existingSeason.Id == season.Id,
			season,
			cancellationToken: cancellationToken
		);

		if (result.MatchedCount == 0)
		{
			return null;
		}

		return season;
	}

	public async Task<Season?> SetActiveAsync(
		string id,
		CancellationToken cancellationToken = default
	)
	{
		var season = await GetByIdAsync(id, cancellationToken);

		if (season is null)
		{
			return null;
		}

		await DeactivateAllAsync(cancellationToken);

		var update = Builders<Season>.Update
			.Set(currentSeason => currentSeason.IsActive, true)
			.Set(currentSeason => currentSeason.UpdatedAt, DateTime.UtcNow);

		return await _seasons.FindOneAndUpdateAsync(
			currentSeason => currentSeason.Id == id,
			update,
			new FindOneAndUpdateOptions<Season>
			{
				ReturnDocument = ReturnDocument.After
			},
			cancellationToken
		);
	}

	private async Task DeactivateAllAsync(CancellationToken cancellationToken)
	{
		var update = Builders<Season>.Update
			.Set(season => season.IsActive, false)
			.Set(season => season.UpdatedAt, DateTime.UtcNow);

		await _seasons.UpdateManyAsync(
			_ => true,
			update,
			cancellationToken: cancellationToken
		);
	}

	private async Task DeactivateAllExceptAsync(
		string activeSeasonId,
		CancellationToken cancellationToken
	)
	{
		var update = Builders<Season>.Update
			.Set(season => season.IsActive, false)
			.Set(season => season.UpdatedAt, DateTime.UtcNow);

		await _seasons.UpdateManyAsync(
			season => season.Id != activeSeasonId,
			update,
			cancellationToken: cancellationToken
		);
	}
}