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

	Task<StoredFileObject?> MarkQuarantinedAsync(
		Guid id,
		string reason,
		CancellationToken cancellationToken = default
	);

	Task<bool> IncrementReferenceCountAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task DecrementReferenceCountAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);
}
