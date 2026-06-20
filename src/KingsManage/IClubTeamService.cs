namespace KingsManage;

public interface IClubTeamService
{
	Task<IReadOnlyList<ClubTeamProfile>> GetAllAsync(
		CancellationToken cancellationToken = default
	);

	Task<ClubTeamProfile?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task<ClubTeamProfile> CreateAsync(
		ClubTeamProfile profile,
		CancellationToken cancellationToken = default
	);

	Task<ClubTeamProfile> UpdateAsync(
		Guid id,
		ClubTeamProfile profile,
		CancellationToken cancellationToken = default
	);

	Task<ClubTeamDeleteResult> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);
}

public enum ClubTeamDeleteResult
{
	Deleted,
	NotFound,
	InUse
}
