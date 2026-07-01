using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class StoredFileObjectService : IStoredFileObjectService
{
	private readonly IMongoCollection<StoredFileObject> _objects;
	private readonly ITenantContext _tenant;

	static StoredFileObjectService()
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(StoredFileObject)))
		{
			BsonClassMap.RegisterClassMap<StoredFileObject>(classMap =>
			{
				classMap.AutoMap();
				classMap.SetIgnoreExtraElements(true);
			});
		}
	}

	public StoredFileObjectService(MongoContext context, ITenantContext tenant)
	{
		_objects = context.Database.GetCollection<StoredFileObject>("storedFileObjects");
		_tenant = tenant;
	}

	public async Task<StoredFileObjectResolution> ResolveAsync(
		StoredFileObject candidate,
		CancellationToken cancellationToken = default
	)
	{
		var contentHash = candidate.ContentHash.Trim().ToLowerInvariant();
		var existing = await FindByHashAsync(contentHash, cancellationToken);

		if (existing is not null)
		{
			ValidateMetadata(existing, candidate);
			return new StoredFileObjectResolution(existing, false);
		}

		candidate.Id = candidate.Id == Guid.Empty ? Guid.NewGuid() : candidate.Id;
		candidate.OrganizationId = _tenant.OrganizationId;
		candidate.ContentHash = contentHash;
		candidate.StorageKey = candidate.StorageKey.Trim();
		candidate.ContentType = candidate.ContentType.Trim();
		candidate.CreatedAt = DateTime.UtcNow;
		candidate.UpdatedAt = candidate.CreatedAt;

		try
		{
			await _objects.InsertOneAsync(candidate, cancellationToken: cancellationToken);
			return new StoredFileObjectResolution(candidate, true);
		}
		catch (MongoWriteException exception)
			when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			existing = await FindByHashAsync(contentHash, cancellationToken);
			if (existing is null)
			{
				throw;
			}

			ValidateMetadata(existing, candidate);
			return new StoredFileObjectResolution(existing, false);
		}
	}

	public async Task<StoredFileObject?> MarkUploadedAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var now = DateTime.UtcNow;
		return await _objects.FindOneAndUpdateAsync(
			OrganizationFilter() & Builders<StoredFileObject>.Filter.Eq(item => item.Id, id),
			Builders<StoredFileObject>.Update
				.Set(item => item.Status, StoredFileObjectStatus.Uploaded)
				.Set(item => item.UploadedAt, now)
				.Set(item => item.UpdatedAt, now)
				.Set(item => item.OrphanedAt, null),
			new FindOneAndUpdateOptions<StoredFileObject>
			{
				ReturnDocument = ReturnDocument.After
			},
			cancellationToken
		);
	}

	public Task IncrementReferenceCountAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return UpdateReferenceCountAsync(id, 1, cancellationToken);
	}

	public async Task DecrementReferenceCountAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var now = DateTime.UtcNow;
		var filter =
			OrganizationFilter()
			& Builders<StoredFileObject>.Filter.Eq(item => item.Id, id)
			& Builders<StoredFileObject>.Filter.Gt(item => item.ReferenceCount, 0);
		var update = Builders<StoredFileObject>.Update
			.Inc(item => item.ReferenceCount, -1)
			.Set(item => item.UpdatedAt, now);

		var storedObject = await _objects.FindOneAndUpdateAsync(
			filter,
			update,
			new FindOneAndUpdateOptions<StoredFileObject>
			{
				ReturnDocument = ReturnDocument.After,
			},
			cancellationToken
		);

		if (storedObject?.ReferenceCount != 0)
		{
			return;
		}

		var orphanFilter =
			OrganizationFilter()
			& Builders<StoredFileObject>.Filter.Eq(item => item.Id, id)
			& Builders<StoredFileObject>.Filter.Eq(item => item.ReferenceCount, 0);
		await _objects.UpdateOneAsync(
			orphanFilter,
			Builders<StoredFileObject>.Update
				.Set(item => item.OrphanedAt, now)
				.Set(item => item.UpdatedAt, now),
			cancellationToken: cancellationToken
		);
	}

	private async Task UpdateReferenceCountAsync(
		Guid id,
		int amount,
		CancellationToken cancellationToken
	)
	{
		await _objects.UpdateOneAsync(
			OrganizationFilter() & Builders<StoredFileObject>.Filter.Eq(item => item.Id, id),
			Builders<StoredFileObject>.Update
				.Inc(item => item.ReferenceCount, amount)
				.Set(item => item.OrphanedAt, null)
				.Set(item => item.UpdatedAt, DateTime.UtcNow),
			cancellationToken: cancellationToken
		);
	}

	private async Task<StoredFileObject?> FindByHashAsync(
		string contentHash,
		CancellationToken cancellationToken
	)
	{
		return await _objects
			.Find(OrganizationFilter() &
				Builders<StoredFileObject>.Filter.Eq(item => item.ContentHash, contentHash) &
				Builders<StoredFileObject>.Filter.Ne(item => item.Status, StoredFileObjectStatus.Deleted))
			.FirstOrDefaultAsync(cancellationToken);
	}

	private FilterDefinition<StoredFileObject> OrganizationFilter()
	{
		return Builders<StoredFileObject>.Filter.Eq(
			item => item.OrganizationId,
			_tenant.OrganizationId
		);
	}

	private static void ValidateMetadata(
		StoredFileObject existing,
		StoredFileObject candidate
	)
	{
		if (
			existing.SizeBytes != candidate.SizeBytes ||
			!string.Equals(existing.ContentType, candidate.ContentType.Trim(), StringComparison.OrdinalIgnoreCase)
		)
		{
			throw new InvalidOperationException(
				"The supplied file hash does not match the existing file metadata."
			);
		}
	}
}
