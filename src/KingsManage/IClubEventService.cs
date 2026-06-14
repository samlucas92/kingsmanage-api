namespace KingsManage;

public interface IClubEventService
{
	Task<IReadOnlyList<ClubEvent>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<ClubEvent?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task<ClubEvent> CreateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	);

	Task<ClubEvent?> UpdateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	);

	Task<bool> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task<ClubEvent?> MarkSeenAsync(
		Guid eventId,
		Guid playerId,
		CancellationToken cancellationToken = default
	);

	Task<ClubEvent?> SetAvailabilityAsync(
		Guid eventId,
		Guid playerId,
		ClubEventAvailabilityStatus status,
		CancellationToken cancellationToken = default
	);
}
