namespace KingsManage;

public sealed class StoredFileObject
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public string ContentHash { get; set; } = string.Empty;
	public string StorageKey { get; set; } = string.Empty;
	public string StorageProvider { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long SizeBytes { get; set; }
	public StoredFileObjectStatus Status { get; set; } = StoredFileObjectStatus.PendingUpload;
	public int ReferenceCount { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UploadedAt { get; set; }
	public DateTime? OrphanedAt { get; set; }
	public DateTime? QuarantinedAt { get; set; }
	public string QuarantineReason { get; set; } = string.Empty;
	public DateTime? DeletedAt { get; set; }
}
