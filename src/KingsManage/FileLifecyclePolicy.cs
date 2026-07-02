namespace KingsManage;

public static class FileLifecyclePolicy
{
	public static bool IsPendingUploadExpired(
		ClubFile file,
		DateTime utcNow,
		TimeSpan retention
	)
	{
		return file.Status == ClubFileStatus.PendingUpload &&
			file.CreatedAt <= utcNow.Subtract(retention);
	}

	public static bool IsQuarantineExpired(
		ClubFile file,
		DateTime utcNow,
		TimeSpan retention
	)
	{
		return file.Status == ClubFileStatus.Quarantined &&
			file.QuarantinedAt is DateTime quarantinedAt &&
			quarantinedAt <= utcNow.Subtract(retention);
	}

	public static bool IsOrphanReadyForDeletion(
		StoredFileObject storedObject,
		DateTime utcNow,
		TimeSpan retention
	)
	{
		return storedObject.ReferenceCount == 0 &&
			storedObject.OrphanedAt is DateTime orphanedAt &&
			orphanedAt <= utcNow.Subtract(retention) &&
			storedObject.Status != StoredFileObjectStatus.Deleted;
	}

	public static IReadOnlyDictionary<Guid, int> CountActiveReferences(
		IEnumerable<Guid?> storedObjectIds
	)
	{
		return storedObjectIds
			.Where(id => id.HasValue)
			.GroupBy(id => id!.Value)
			.ToDictionary(group => group.Key, group => group.Count());
	}
}
