namespace KingsManage;

public interface IStoredFileObjectService
{
	Task<StoredFileObjectResolution> ResolveAsync(
		StoredFileObject candidate,
		CancellationToken cancellationToken = default
	);

	Task<StoredFileObject?> MarkUploadedAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task IncrementReferenceCountAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task DecrementReferenceCountAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);
}
