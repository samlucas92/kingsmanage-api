namespace KingsManage;

public interface IClubEventService
{
	Task<IReadOnlyList<ClubEvent>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<ClubEvent>> GetBySeasonAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	);

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
}
