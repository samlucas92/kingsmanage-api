using KingsManage;
namespace KingsManage.Tests.Integration.Auth;

public sealed class TestStoredFileObjectService : IStoredFileObjectService
{
	public List<StoredFileObject> Objects { get; } = new();

	public Task<StoredFileObjectResolution> ResolveAsync(
		StoredFileObject candidate,
		CancellationToken cancellationToken = default
	)
	{
		var existing = Objects.FirstOrDefault(item =>
			item.OrganizationId == candidate.OrganizationId &&
			item.ContentHash == candidate.ContentHash &&
			item.Status != StoredFileObjectStatus.Deleted);

		if (existing is not null)
		{
			if (
				existing.SizeBytes != candidate.SizeBytes ||
				!string.Equals(existing.ContentType, candidate.ContentType, StringComparison.OrdinalIgnoreCase)
			)
			{
				throw new InvalidOperationException(
					"The supplied file hash does not match the existing file metadata."
				);
			}

			return Task.FromResult(new StoredFileObjectResolution(existing, false));
		}

		Objects.Add(candidate);
		return Task.FromResult(new StoredFileObjectResolution(candidate, true));
	}

	public Task<StoredFileObject?> MarkUploadedAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var item = Objects.FirstOrDefault(current => current.Id == id);
		if (item is not null)
		{
			item.Status = StoredFileObjectStatus.Uploaded;
			item.UploadedAt = DateTime.UtcNow;
			item.UpdatedAt = DateTime.UtcNow;
		}

		return Task.FromResult(item);
	}

	public Task IncrementReferenceCountAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var item = Objects.FirstOrDefault(current => current.Id == id);
		if (item is not null)
		{
			item.ReferenceCount++;
			item.OrphanedAt = null;
		}

		return Task.CompletedTask;
	}

	public Task DecrementReferenceCountAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var item = Objects.FirstOrDefault(current => current.Id == id);
		if (item is not null)
		{
			item.ReferenceCount = Math.Max(0, item.ReferenceCount - 1);
			item.OrphanedAt = item.ReferenceCount == 0 ? DateTime.UtcNow : null;
		}

		return Task.CompletedTask;
	}
}

public sealed class TestClubFileService : IClubFileService
{
	public List<ClubFile> Files { get; } = new();

	public Task<IReadOnlyList<ClubFile>> GetByLinkedEntityAsync(
		ClubFileLinkedEntityType linkedEntityType,
		Guid linkedEntityId,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult<IReadOnlyList<ClubFile>>(
			Files
				.Where(file =>
					file.LinkedEntityType == linkedEntityType &&
					file.LinkedEntityId == linkedEntityId &&
					file.Status != ClubFileStatus.Deleted)
				.OrderByDescending(file => file.CreatedAt)
				.ToList()
		);
	}

	public Task<ClubFile?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(Files.FirstOrDefault(file => file.Id == id));
	}

	public Task<ClubFile> CreateAsync(
		ClubFile file,
		CancellationToken cancellationToken = default
	)
	{
		if (file.Id == Guid.Empty)
		{
			file.Id = Guid.NewGuid();
		}

		file.OriginalFileName = file.OriginalFileName.Trim();
		file.StoredFileName = file.StoredFileName.Trim();
		file.StorageKey = file.StorageKey.Trim();
		file.ContentType = file.ContentType.Trim();
		file.UploadedByUserEmail = file.UploadedByUserEmail.Trim();
		file.CreatedAt = DateTime.UtcNow;
		file.UpdatedAt = DateTime.UtcNow;

		Files.Add(file);

		return Task.FromResult(file);
	}

	public Task<ClubFile?> MarkUploadedAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var file = Files.FirstOrDefault(currentFile => currentFile.Id == id);

		if (file is null || file.Status == ClubFileStatus.Deleted)
		{
			return Task.FromResult<ClubFile?>(null);
		}

		file.Status = ClubFileStatus.Uploaded;
		file.UploadedAt = DateTime.UtcNow;
		file.UpdatedAt = DateTime.UtcNow;

		return Task.FromResult<ClubFile?>(file);
	}

	public Task<bool> SoftDeleteAsync(
		Guid id,
		Guid deletedByUserId,
		CancellationToken cancellationToken = default
	)
	{
		var file = Files.FirstOrDefault(currentFile => currentFile.Id == id);

		if (file is null || file.Status == ClubFileStatus.Deleted)
		{
			return Task.FromResult(false);
		}

		file.Status = ClubFileStatus.Deleted;
		file.DeletedAt = DateTime.UtcNow;
		file.DeletedByUserId = deletedByUserId;
		file.UpdatedAt = DateTime.UtcNow;

		return Task.FromResult(true);
	}
}

public sealed class TestFileStorageService : IFileStorageService
{
	public Task<FileStorageSignedUrl> CreateUploadUrlAsync(
		string storageKey,
		TimeSpan expiresIn,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(CreateSignedUrl("upload", storageKey, expiresIn));
	}

	public Task<FileStorageSignedUrl> CreateDownloadUrlAsync(
		string storageKey,
		TimeSpan expiresIn,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(CreateSignedUrl("download", storageKey, expiresIn));
	}

	private static FileStorageSignedUrl CreateSignedUrl(
		string operation,
		string storageKey,
		TimeSpan expiresIn
	)
	{
		return new FileStorageSignedUrl
		{
			Url = $"https://storage.test/{operation}/{Uri.EscapeDataString(storageKey)}",
			ExpiresAtUtc = DateTime.UtcNow.Add(expiresIn)
		};
	}
}
