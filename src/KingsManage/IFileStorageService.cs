namespace KingsManage;

public interface IFileStorageService
{
	Task<FileStorageSignedUrl> CreateUploadUrlAsync(
		string storageKey,
		TimeSpan expiresIn,
		CancellationToken cancellationToken = default
	);

	Task<FileStorageSignedUrl> CreateDownloadUrlAsync(
		string storageKey,
		TimeSpan expiresIn,
		CancellationToken cancellationToken = default
	);

	Task<FileStorageValidationResult> ValidateObjectAsync(
		string storageKey,
		string expectedContentHash,
		string expectedContentType,
		long expectedSizeBytes,
		CancellationToken cancellationToken = default
	);

	Task<bool> DeleteObjectAsync(
		string storageKey,
		CancellationToken cancellationToken = default
	);
}
