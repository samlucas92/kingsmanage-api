namespace KingsManage;

public class ClubFile : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public string OriginalFileName { get; set; } = string.Empty;
	public string StoredFileName { get; set; } = string.Empty;
	public string StorageKey { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long SizeBytes { get; set; }
	public ClubFileVisibility Visibility { get; set; } = ClubFileVisibility.AuthenticatedUsers;
	public ClubFileLinkedEntityType LinkedEntityType { get; set; } = ClubFileLinkedEntityType.Post;
	public Guid LinkedEntityId { get; set; }
	public ClubFileStatus Status { get; set; } = ClubFileStatus.PendingUpload;
	public Guid UploadedByUserId { get; set; }
	public string UploadedByUserEmail { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UploadedAt { get; set; }
	public DateTime? DeletedAt { get; set; }
	public Guid? DeletedByUserId { get; set; }
}
