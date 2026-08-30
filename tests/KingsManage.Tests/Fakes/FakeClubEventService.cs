using KingsManage;

namespace KingsManage.Tests.Fakes;

public sealed class FakeClubEventService : IClubEventService
{
	public List<ClubEvent> Events { get; } = [];

	public Task<IReadOnlyList<ClubEvent>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult<IReadOnlyList<ClubEvent>>(Events);
	}

	public Task<ClubEvent?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(Events.FirstOrDefault(clubEvent => clubEvent.Id == id));
	}

	public Task<ClubEvent> CreateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	)
	{
		clubEvent.Id = clubEvent.Id == Guid.Empty ? Guid.NewGuid() : clubEvent.Id;
		Events.Add(clubEvent);
		return Task.FromResult(clubEvent);
	}

	public Task<ClubEvent?> UpdateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	)
	{
		var index = Events.FindIndex(item => item.Id == clubEvent.Id);

		if (index < 0)
		{
			return Task.FromResult<ClubEvent?>(null);
		}

		Events[index] = clubEvent;
		return Task.FromResult<ClubEvent?>(clubEvent);
	}

	public Task<bool> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(Events.RemoveAll(clubEvent => clubEvent.Id == id) > 0);
	}

	public Task<ClubEvent?> MarkSeenAsync(
		Guid eventId,
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return GetByIdAsync(eventId, cancellationToken);
	}

	public Task<ClubEvent?> SetAvailabilityAsync(
		Guid eventId,
		Guid playerId,
		ClubEventAvailabilityStatus status,
		CancellationToken cancellationToken = default
	)
	{
		return GetByIdAsync(eventId, cancellationToken);
	}
}
