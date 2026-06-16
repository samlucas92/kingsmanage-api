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
}
