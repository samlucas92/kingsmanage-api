using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubEventService : IClubEventService
{
	private readonly IMongoCollection<ClubEvent> _events;
	private readonly TenantMongoScope _tenant;

	static ClubEventService()
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(ClubEvent)))
		{
			BsonClassMap.RegisterClassMap<ClubEvent>(
				classMap =>
				{
					classMap.AutoMap();
					classMap.SetIgnoreExtraElements(true);
				}
			);
		}
	}

	public ClubEventService(MongoContext context, TenantMongoScope tenant)
	{
		_events = context.Database.GetCollection<ClubEvent>("events");
		_tenant = tenant;
	}

	public async Task<IReadOnlyList<ClubEvent>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		var events = await _events
			.Find(_tenant.Filter<ClubEvent>())
			.SortBy(clubEvent => clubEvent.StartDateTime)
			.ToListAsync(cancellationToken);

		return events.Select(NormaliseFromStorage).ToList();
	}

	public async Task<ClubEvent?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var clubEvent = await _events
			.Find(_tenant.Filter<ClubEvent>(clubEvent => clubEvent.Id == id))
			.FirstOrDefaultAsync(cancellationToken);

		return clubEvent is null ? null : NormaliseFromStorage(clubEvent);
	}

	public async Task<ClubEvent> CreateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	)
	{
		clubEvent.Id = clubEvent.Id == Guid.Empty ? Guid.NewGuid() : clubEvent.Id;
		PrepareForSave(clubEvent, true);
		_tenant.Assign(clubEvent);

		await _events.InsertOneAsync(clubEvent, cancellationToken: cancellationToken);

		return clubEvent;
	}

	public async Task<ClubEvent?> UpdateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	)
	{
		PrepareForSave(clubEvent, false);
		_tenant.Assign(clubEvent);

		var result = await _events.ReplaceOneAsync(
			_tenant.Filter<ClubEvent>(existingEvent => existingEvent.Id == clubEvent.Id),
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
			_tenant.Filter<ClubEvent>(clubEvent => clubEvent.Id == id),
			cancellationToken
		);

		return result.DeletedCount > 0;
	}

	public async Task<ClubEvent?> MarkSeenAsync(
		Guid eventId,
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		var clubEvent = await GetByIdAsync(eventId, cancellationToken);

		if (clubEvent is null)
		{
			return null;
		}

		var existingSeenStatus = clubEvent.SeenBy.FirstOrDefault(seen => seen.PlayerId == playerId);

		if (existingSeenStatus is null)
		{
			clubEvent.SeenBy.Add(
				new ClubEventSeenStatus
				{
					PlayerId = playerId,
					SeenAt = DateTime.UtcNow
				}
			);
		}
		else
		{
			existingSeenStatus.SeenAt = DateTime.UtcNow;
		}

		clubEvent.UpdatedAt = DateTime.UtcNow;

		return await UpdateAsync(clubEvent, cancellationToken);
	}

	public async Task<ClubEvent?> SetAvailabilityAsync(
		Guid eventId,
		Guid playerId,
		ClubEventAvailabilityStatus status,
		CancellationToken cancellationToken = default
	)
	{
		var clubEvent = await GetByIdAsync(eventId, cancellationToken);

		if (clubEvent is null)
		{
			return null;
		}

		var existingAvailability = clubEvent.AvailabilityResponses.FirstOrDefault(
			response => response.PlayerId == playerId
		);

		if (existingAvailability is null)
		{
			clubEvent.AvailabilityResponses.Add(
				new ClubEventAvailabilityResponse
				{
					PlayerId = playerId,
					Status = status,
					UpdatedAt = DateTime.UtcNow
				}
			);
		}
		else
		{
			existingAvailability.Status = status;
			existingAvailability.UpdatedAt = DateTime.UtcNow;
		}

		var existingSeenStatus = clubEvent.SeenBy.FirstOrDefault(seen => seen.PlayerId == playerId);

		if (existingSeenStatus is null)
		{
			clubEvent.SeenBy.Add(
				new ClubEventSeenStatus
				{
					PlayerId = playerId,
					SeenAt = DateTime.UtcNow
				}
			);
		}
		else
		{
			existingSeenStatus.SeenAt = DateTime.UtcNow;
		}

		clubEvent.UpdatedAt = DateTime.UtcNow;

		return await UpdateAsync(clubEvent, cancellationToken);
	}

	private static ClubEvent NormaliseFromStorage(ClubEvent clubEvent)
	{
		clubEvent.Title ??= string.Empty;
		clubEvent.Description ??= string.Empty;
		clubEvent.Location ??= string.Empty;
		clubEvent.MatchLinks ??= [];
		clubEvent.AvailabilityResponses ??= [];
		clubEvent.SeenBy ??= [];

		if (clubEvent.CreatedAt == default)
		{
			clubEvent.CreatedAt = DateTime.UtcNow;
		}

		if (clubEvent.UpdatedAt == default)
		{
			clubEvent.UpdatedAt = clubEvent.CreatedAt;
		}

		return clubEvent;
	}

	private static void PrepareForSave(ClubEvent clubEvent, bool isNew)
	{
		clubEvent.Title = clubEvent.Title.Trim();
		clubEvent.Description = clubEvent.Description.Trim();
		clubEvent.Location = clubEvent.Location.Trim();
		clubEvent.MatchLinks ??= [];
		clubEvent.AvailabilityResponses ??= [];
		clubEvent.SeenBy ??= [];

		if (isNew || clubEvent.CreatedAt == default)
		{
			clubEvent.CreatedAt = DateTime.UtcNow;
		}

		clubEvent.UpdatedAt = DateTime.UtcNow;
	}
}
