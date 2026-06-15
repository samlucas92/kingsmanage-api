namespace KingsManage;

public interface IClubPostService
{
	Task<IReadOnlyList<ClubPost>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<ClubPost?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task<ClubPost> CreateAsync(
		ClubPost post,
		CancellationToken cancellationToken = default
	);

	Task<ClubPost?> UpdateAsync(
		ClubPost post,
		CancellationToken cancellationToken = default
	);

	Task<bool> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);
}
