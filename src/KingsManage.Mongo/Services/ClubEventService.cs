using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubEventService : IClubEventService
{
	private readonly IMongoCollection<ClubEvent> _events;

	public ClubEventService(MongoContext context)
	{
		_events = context.Database.GetCollection<ClubEvent>("events");
	}

	public async Task<IReadOnlyList<ClubEvent>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await _events
			.Find(_ => true)
			.SortBy(clubEvent => clubEvent.StartDateTime)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<ClubEvent>> GetBySeasonAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		return await _events
			.Find(clubEvent => clubEvent.SeasonId == seasonId)
			.SortBy(clubEvent => clubEvent.StartDateTime)
			.ToListAsync(cancellationToken);
	}

	public async Task<ClubEvent?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return await _events
			.Find(clubEvent => clubEvent.Id == id)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<ClubEvent> CreateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	)
	{
		clubEvent.Id = clubEvent.Id == Guid.Empty ? Guid.NewGuid() : clubEvent.Id;
		clubEvent.Title = clubEvent.Title.Trim();
		clubEvent.Description = clubEvent.Description.Trim();
		clubEvent.Location = clubEvent.Location.Trim();
		clubEvent.CreatedAt = DateTime.UtcNow;
		clubEvent.UpdatedAt = DateTime.UtcNow;

		await _events.InsertOneAsync(clubEvent, cancellationToken: cancellationToken);

		return clubEvent;
	}

	public async Task<ClubEvent?> UpdateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	)
	{
		clubEvent.Title = clubEvent.Title.Trim();
		clubEvent.Description = clubEvent.Description.Trim();
		clubEvent.Location = clubEvent.Location.Trim();
		clubEvent.UpdatedAt = DateTime.UtcNow;

		var result = await _events.ReplaceOneAsync(
			existingEvent => existingEvent.Id == clubEvent.Id,
			clubEvent,
			cancellationToken: cancellationToken
		);

		if (result.MatchedCount == 0)
		{
			return null;
		}

		return clubEvent;
	}

	public async Task<bool> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var result = await _events.DeleteOneAsync(
			clubEvent => clubEvent.Id == id,
			cancellationToken
		);

		return result.DeletedCount > 0;
	}
}
