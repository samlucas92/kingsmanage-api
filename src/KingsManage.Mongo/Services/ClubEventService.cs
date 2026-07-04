using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubEventService : IClubEventService
{
	private readonly IMongoCollection<ClubEvent> events;
	private readonly TenantMongoScope tenant;
	private readonly ITeamAccessContext teamAccess;

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

	public ClubEventService(
		MongoContext context,
		TenantMongoScope tenant,
		ITeamAccessContext teamAccess)
	{
		events = context.Database.GetCollection<ClubEvent>("events");
		this.tenant = tenant;
		this.teamAccess = teamAccess;
	}

	public async Task<IReadOnlyList<ClubEvent>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		var events = await this.events
			.Find(tenant.Filter<ClubEvent>())
			.SortBy(clubEvent => clubEvent.StartDateTime)
			.ToListAsync(cancellationToken);

		return events
			.Select(NormaliseFromStorage)
			.Where(CanAccessEvent)
			.ToList();
	}

	public async Task<ClubEvent?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var clubEvent = await events
			.Find(tenant.Filter<ClubEvent>(clubEvent => clubEvent.Id == id))
			.FirstOrDefaultAsync(cancellationToken);

		if (clubEvent is null) return null;
		var normalised = NormaliseFromStorage(clubEvent);
		return CanAccessEvent(normalised) ? normalised : null;
	}

	public async Task<ClubEvent> CreateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	)
	{
		if (!CanAccessEvent(clubEvent))
		{
			throw new UnauthorizedAccessException("The event belongs to a team outside this membership.");
		}

		clubEvent.Id = clubEvent.Id == Guid.Empty ? Guid.NewGuid() : clubEvent.Id;
		PrepareForSave(clubEvent, true);
		tenant.Assign(clubEvent);

		await events.InsertOneAsync(clubEvent, cancellationToken: cancellationToken);

		return clubEvent;
	}

	public async Task<ClubEvent?> UpdateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	)
	{
		if (!CanAccessEvent(clubEvent)) return null;

		PrepareForSave(clubEvent, false);
		tenant.Assign(clubEvent);

		var result = await events.ReplaceOneAsync(
			tenant.Filter<ClubEvent>(existingEvent => existingEvent.Id == clubEvent.Id),
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
		if (await GetByIdAsync(id, cancellationToken) is null) return false;

		var result = await events.DeleteOneAsync(
			tenant.Filter<ClubEvent>(clubEvent => clubEvent.Id == id),
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

	private bool CanAccessEvent(ClubEvent clubEvent)
	{
		if (clubEvent.TeamIds.Count > 0)
			return teamAccess.CanAccessAnyTeam(clubEvent.TeamIds);

		return clubEvent.TeamScope switch
		{
			ClubEventTeamScope.First => teamAccess.CanAccessTeam(DefaultClubTeams.FirstTeamId),
			ClubEventTeamScope.Second => teamAccess.CanAccessTeam(DefaultClubTeams.SecondTeamId),
			_ => teamAccess.CanAccessAnyTeam(
				[DefaultClubTeams.FirstTeamId, DefaultClubTeams.SecondTeamId])
		};
	}
}
