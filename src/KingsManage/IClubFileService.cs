namespace KingsManage;

public interface IClubFileService
{
	Task<IReadOnlyList<ClubFile>> GetByLinkedEntityAsync(
		ClubFileLinkedEntityType linkedEntityType,
		Guid linkedEntityId,
		CancellationToken cancellationToken = default
	);

	Task<ClubFile?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task<ClubFile> CreateAsync(
		ClubFile file,
		CancellationToken cancellationToken = default
	);

	Task<ClubFile?> MarkUploadedAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task<ClubFile?> MarkQuarantinedAsync(
		Guid id,
		string reason,
		CancellationToken cancellationToken = default
	);

	Task<bool> SoftDeleteAsync(
		Guid id,
		Guid deletedByUserId,
		CancellationToken cancellationToken = default
	);
}
