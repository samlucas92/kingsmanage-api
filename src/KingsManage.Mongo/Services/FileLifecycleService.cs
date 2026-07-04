using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class FileLifecycleService : IFileLifecycleService
{
	private readonly IMongoCollection<StoredFileObject> objects;
	private readonly IMongoCollection<ClubFile> files;
	private readonly IMongoCollection<FileLifecycleAudit> audit;
	private readonly IFileStorageService storage;
	private readonly FileLifecycleSettings settings;

	public FileLifecycleService(
		MongoContext context,
		IFileStorageService storage,
		FileLifecycleSettings settings
	)
	{
		objects = context.Database.GetCollection<StoredFileObject>("storedFileObjects");
		files = context.Database.GetCollection<ClubFile>("files");
		audit = context.Database.GetCollection<FileLifecycleAudit>("fileLifecycleAudit");
		this.storage = storage;
		this.settings = settings;
	}

	public async Task<FileStorageUsage> GetUsageAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default
	)
	{
		var objects = await this.objects
			.Find(item =>
				item.OrganizationId == organizationId &&
				item.Status != StoredFileObjectStatus.Deleted)
			.ToListAsync(cancellationToken);
		var usedBytes = objects.Sum(item => item.SizeBytes);
		var quotaBytes = Math.Max(0, settings.DefaultOrganizationQuotaBytes);
		var usedPercent = quotaBytes == 0
			? 100
			: Math.Round((double)usedBytes / quotaBytes * 100, 2);

		return new FileStorageUsage
		{
			OrganizationId = organizationId,
			UsedBytes = usedBytes,
			QuotaBytes = quotaBytes,
			RemainingBytes = Math.Max(0, quotaBytes - usedBytes),
			UsedPercent = usedPercent,
			IsNearLimit = usedPercent >= Math.Clamp(settings.QuotaWarningPercent, 1, 100),
			IsAtLimit = quotaBytes == 0 || usedBytes >= quotaBytes,
			StoredObjectCount = objects.Count(item => item.Status == StoredFileObjectStatus.Uploaded),
			PendingObjectCount = objects.Count(item =>
				item.Status is StoredFileObjectStatus.PendingUpload or StoredFileObjectStatus.Quarantined),
			OrphanedObjectCount = objects.Count(item => item.ReferenceCount == 0)
		};
	}

	public async Task<FileUploadCapacityResult> CheckUploadCapacityAsync(
		Guid organizationId,
		string contentHash,
		long requestedBytes,
		CancellationToken cancellationToken = default
	)
	{
		var normalizedHash = contentHash.Trim().ToLowerInvariant();
		var reusesExistingObject =
			!string.IsNullOrWhiteSpace(normalizedHash) &&
			await objects.Find(item =>
					item.OrganizationId == organizationId &&
					item.ContentHash == normalizedHash &&
					item.Status != StoredFileObjectStatus.Deleted &&
					item.Status != StoredFileObjectStatus.Deleting)
				.AnyAsync(cancellationToken);
		var usage = await GetUsageAsync(organizationId, cancellationToken);
		var additionalBytes = reusesExistingObject ? 0 : Math.Max(0, requestedBytes);
		var projectedBytes = usage.UsedBytes + additionalBytes;
		usage.UsedPercent = usage.QuotaBytes == 0
			? 100
			: Math.Round((double)projectedBytes / usage.QuotaBytes * 100, 2);
		usage.IsNearLimit = usage.UsedPercent >=
			Math.Clamp(settings.QuotaWarningPercent, 1, 100);
		usage.IsAtLimit = usage.QuotaBytes == 0 || projectedBytes >= usage.QuotaBytes;

		return new FileUploadCapacityResult
		{
			IsAllowed = usage.QuotaBytes > 0 &&
				projectedBytes <= usage.QuotaBytes,
			ReusesExistingObject = reusesExistingObject,
			Usage = usage
		};
	}

	public async Task RecordAuditAsync(
		FileLifecycleAudit audit,
		CancellationToken cancellationToken = default
	)
	{
		audit.Id = audit.Id == Guid.Empty ? Guid.NewGuid() : audit.Id;
		audit.CreatedAt = audit.CreatedAt == default ? DateTime.UtcNow : audit.CreatedAt;
		audit.Detail = audit.Detail.Trim();
		await this.audit.InsertOneAsync(audit, cancellationToken: cancellationToken);
	}

	public async Task<IReadOnlyList<FileLifecycleAudit>> GetAuditAsync(
		Guid organizationId,
		int limit,
		CancellationToken cancellationToken = default
	)
	{
		return await audit
			.Find(item => item.OrganizationId == organizationId)
			.SortByDescending(item => item.CreatedAt)
			.Limit(Math.Clamp(limit, 1, 500))
			.ToListAsync(cancellationToken);
	}

	public async Task<FileLifecycleRunResult> RunMaintenanceAsync(
		DateTime utcNow,
		CancellationToken cancellationToken = default
	)
	{
		var result = new FileLifecycleRunResult();
		await DeleteAbandonedReferencesAsync(utcNow, result, cancellationToken);
		await ReconcileAndDeleteObjectsAsync(utcNow, result, cancellationToken);
		return result;
	}

	private async Task DeleteAbandonedReferencesAsync(
		DateTime utcNow,
		FileLifecycleRunResult result,
		CancellationToken cancellationToken
	)
	{
		var pendingCutoff = utcNow.AddHours(-Math.Max(1, settings.PendingUploadRetentionHours));
		var quarantineCutoff = utcNow.AddHours(-Math.Max(1, settings.QuarantineRetentionHours));
		var expiredFilter =
			Builders<ClubFile>.Filter.And(
				Builders<ClubFile>.Filter.Eq(item => item.Status, ClubFileStatus.PendingUpload),
				Builders<ClubFile>.Filter.Lte(item => item.CreatedAt, pendingCutoff)
			)
			| Builders<ClubFile>.Filter.And(
				Builders<ClubFile>.Filter.Eq(
					item => item.LinkedEntityType,
					ClubFileLinkedEntityType.RichTextDraft
				),
				Builders<ClubFile>.Filter.Eq(item => item.Status, ClubFileStatus.Uploaded),
				Builders<ClubFile>.Filter.Lte(item => item.CreatedAt, pendingCutoff)
			)
			| Builders<ClubFile>.Filter.And(
				Builders<ClubFile>.Filter.Eq(item => item.Status, ClubFileStatus.Quarantined),
				Builders<ClubFile>.Filter.Lte(item => item.QuarantinedAt, quarantineCutoff)
			);
		var expired = await files.Find(expiredFilter).ToListAsync(cancellationToken);

		foreach (var file in expired)
		{
			var update = Builders<ClubFile>.Update
				.Set(item => item.Status, ClubFileStatus.Deleted)
				.Set(item => item.DeletedAt, utcNow)
				.Set(item => item.UpdatedAt, utcNow);
			var updated = await files.UpdateOneAsync(
				item => item.Id == file.Id && item.Status != ClubFileStatus.Deleted,
				update,
				cancellationToken: cancellationToken
			);

			if (updated.ModifiedCount == 0)
			{
				continue;
			}

			result.AbandonedReferencesDeleted++;
			await RecordAuditAsync(
				new FileLifecycleAudit
				{
					OrganizationId = file.OrganizationId,
					ClubId = file.ClubId,
					FileId = file.Id,
					StoredObjectId = file.StoredObjectId,
					EventType = FileLifecycleEventType.AbandonedUploadDeleted,
					Detail = $"Expired {file.Status} reference removed.",
					CreatedAt = utcNow
				},
				cancellationToken
			);
		}
	}

	private async Task ReconcileAndDeleteObjectsAsync(
		DateTime utcNow,
		FileLifecycleRunResult result,
		CancellationToken cancellationToken
	)
	{
		var activeReferences = await files
			.Find(item =>
				item.StoredObjectId != null &&
				item.Status != ClubFileStatus.Deleted)
			.Project(item => item.StoredObjectId)
			.ToListAsync(cancellationToken);
		var referenceCounts = FileLifecyclePolicy.CountActiveReferences(activeReferences);
		var objects = await this.objects
			.Find(item => item.Status != StoredFileObjectStatus.Deleted)
			.ToListAsync(cancellationToken);

		foreach (var storedObject in objects)
		{
			var referenceCount = referenceCounts.GetValueOrDefault(storedObject.Id);
			DateTime? orphanedAt = referenceCount == 0
				? storedObject.OrphanedAt ?? utcNow
				: null;

			if (
				storedObject.ReferenceCount != referenceCount ||
				storedObject.OrphanedAt != orphanedAt
			)
			{
				await this.objects.UpdateOneAsync(
					item => item.Id == storedObject.Id,
					Builders<StoredFileObject>.Update
						.Set(item => item.ReferenceCount, referenceCount)
						.Set(item => item.OrphanedAt, orphanedAt)
						.Set(item => item.UpdatedAt, utcNow),
					cancellationToken: cancellationToken
				);
				result.ReferenceCountsReconciled++;
				await RecordAuditAsync(
					new FileLifecycleAudit
					{
						OrganizationId = storedObject.OrganizationId,
						StoredObjectId = storedObject.Id,
						EventType = FileLifecycleEventType.ReferenceCountReconciled,
						Detail = $"Reference count reconciled to {referenceCount}.",
						CreatedAt = utcNow
					},
					cancellationToken
				);
			}

			var orphanCutoff = utcNow.AddHours(-Math.Max(1, settings.OrphanRetentionHours));
			if (referenceCount != 0 || orphanedAt is null || orphanedAt > orphanCutoff)
			{
				continue;
			}

			var claim = await this.objects.UpdateOneAsync(
				item =>
					item.Id == storedObject.Id &&
					item.ReferenceCount == 0 &&
					item.Status != StoredFileObjectStatus.Deleted &&
					item.Status != StoredFileObjectStatus.Deleting,
				Builders<StoredFileObject>.Update
					.Set(item => item.Status, StoredFileObjectStatus.Deleting)
					.Set(item => item.UpdatedAt, utcNow),
				cancellationToken: cancellationToken
			);
			if (claim.ModifiedCount == 0 && storedObject.Status != StoredFileObjectStatus.Deleting)
			{
				continue;
			}

			var deleted = false;
			try
			{
				deleted = await storage.DeleteObjectAsync(
					storedObject.StorageKey,
					cancellationToken
				);
			}
			catch
			{
				deleted = false;
			}

			if (!deleted)
			{
				await this.objects.UpdateOneAsync(
					item =>
						item.Id == storedObject.Id &&
						item.Status == StoredFileObjectStatus.Deleting,
					Builders<StoredFileObject>.Update
						.Set(item => item.Status, storedObject.Status)
						.Set(item => item.UpdatedAt, utcNow),
					cancellationToken: cancellationToken
				);
				result.DeletionFailures++;
				await RecordAuditAsync(
					new FileLifecycleAudit
					{
						OrganizationId = storedObject.OrganizationId,
						StoredObjectId = storedObject.Id,
						EventType = FileLifecycleEventType.ExternalObjectDeletionFailed,
						Detail = "External object deletion failed and will be retried.",
						CreatedAt = utcNow
					},
					cancellationToken
				);
				continue;
			}

			var metadataDeletion = await this.objects.DeleteOneAsync(
				item =>
					item.Id == storedObject.Id &&
					item.ReferenceCount == 0 &&
					item.Status == StoredFileObjectStatus.Deleting,
				cancellationToken
			);
			if (metadataDeletion.DeletedCount == 0)
			{
				continue;
			}

			result.ExternalObjectsDeleted++;
			await RecordAuditAsync(
				new FileLifecycleAudit
				{
					OrganizationId = storedObject.OrganizationId,
					StoredObjectId = storedObject.Id,
					EventType = FileLifecycleEventType.ExternalObjectDeleted,
					Detail = "Unreferenced external object deleted after retention period.",
					CreatedAt = utcNow
				},
				cancellationToken
			);
		}
	}
}
