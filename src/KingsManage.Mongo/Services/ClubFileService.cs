using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubFileService : IClubFileService
{
	private readonly IMongoCollection<ClubFile> _files;

	static ClubFileService()
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(ClubFile)))
		{
			BsonClassMap.RegisterClassMap<ClubFile>(
				classMap =>
				{
					classMap.AutoMap();
					classMap.SetIgnoreExtraElements(true);
				}
			);
		}
	}

	public ClubFileService(MongoContext context)
	{
		_files = context.Database.GetCollection<ClubFile>("files");
	}

	public async Task<IReadOnlyList<ClubFile>> GetByLinkedEntityAsync(
		ClubFileLinkedEntityType linkedEntityType,
		Guid linkedEntityId,
		CancellationToken cancellationToken = default
	)
	{
		var files = await _files
			.Find(file =>
				file.LinkedEntityType == linkedEntityType &&
				file.LinkedEntityId == linkedEntityId &&
				file.Status != ClubFileStatus.Deleted)
			.SortByDescending(file => file.CreatedAt)
			.ToListAsync(cancellationToken);

		return files.Select(NormaliseFromStorage).ToList();
	}

	public async Task<ClubFile?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var file = await _files
			.Find(currentFile => currentFile.Id == id)
			.FirstOrDefaultAsync(cancellationToken);

		return file is null ? null : NormaliseFromStorage(file);
	}

	public async Task<ClubFile> CreateAsync(
		ClubFile file,
		CancellationToken cancellationToken = default
	)
	{
		file.Id = file.Id == Guid.Empty ? Guid.NewGuid() : file.Id;
		PrepareForSave(file, true);

		await _files.InsertOneAsync(file, cancellationToken: cancellationToken);

		return file;
	}

	public async Task<ClubFile?> MarkUploadedAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var file = await GetByIdAsync(id, cancellationToken);

		if (file is null || file.Status == ClubFileStatus.Deleted)
		{
			return null;
		}

		file.Status = ClubFileStatus.Uploaded;
		file.UploadedAt = DateTime.UtcNow;
		file.UpdatedAt = DateTime.UtcNow;

		var result = await _files.ReplaceOneAsync(
			currentFile => currentFile.Id == id,
			file,
			cancellationToken: cancellationToken
		);

		return result.MatchedCount == 0 ? null : file;
	}

	public async Task<bool> SoftDeleteAsync(
		Guid id,
		Guid deletedByUserId,
		CancellationToken cancellationToken = default
	)
	{
		var file = await GetByIdAsync(id, cancellationToken);

		if (file is null || file.Status == ClubFileStatus.Deleted)
		{
			return false;
		}

		file.Status = ClubFileStatus.Deleted;
		file.DeletedAt = DateTime.UtcNow;
		file.DeletedByUserId = deletedByUserId;
		file.UpdatedAt = DateTime.UtcNow;

		var result = await _files.ReplaceOneAsync(
			currentFile => currentFile.Id == id,
			file,
			cancellationToken: cancellationToken
		);

		return result.MatchedCount > 0;
	}

	private static ClubFile NormaliseFromStorage(ClubFile file)
	{
		file.OriginalFileName ??= string.Empty;
		file.StoredFileName ??= string.Empty;
		file.StorageKey ??= string.Empty;
		file.ContentType ??= string.Empty;
		file.UploadedByUserEmail ??= string.Empty;

		if (file.CreatedAt == default)
		{
			file.CreatedAt = DateTime.UtcNow;
		}

		if (file.UpdatedAt == default)
		{
			file.UpdatedAt = file.CreatedAt;
		}

		return file;
	}

	private static void PrepareForSave(ClubFile file, bool isNew)
	{
		file.OriginalFileName = file.OriginalFileName.Trim();
		file.StoredFileName = file.StoredFileName.Trim();
		file.StorageKey = file.StorageKey.Trim();
		file.ContentType = file.ContentType.Trim();
		file.UploadedByUserEmail = file.UploadedByUserEmail.Trim();

		if (isNew || file.CreatedAt == default)
		{
			file.CreatedAt = DateTime.UtcNow;
		}

		file.UpdatedAt = DateTime.UtcNow;
	}
}
