namespace KingsManage;

public interface IFileLifecycleService
{
	Task<FileStorageUsage> GetUsageAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default
	);

	Task<FileUploadCapacityResult> CheckUploadCapacityAsync(
		Guid organizationId,
		string contentHash,
		long requestedBytes,
		CancellationToken cancellationToken = default
	);

	Task RecordAuditAsync(
		FileLifecycleAudit audit,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<FileLifecycleAudit>> GetAuditAsync(
		Guid organizationId,
		int limit,
		CancellationToken cancellationToken = default
	);

	Task<FileLifecycleRunResult> RunMaintenanceAsync(
		DateTime utcNow,
		CancellationToken cancellationToken = default
	);
}
