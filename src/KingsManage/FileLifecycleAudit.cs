namespace KingsManage;

public sealed class FileLifecycleAudit
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid OrganizationId { get; set; }
	public Guid? ClubId { get; set; }
	public Guid? FileId { get; set; }
	public Guid? StoredObjectId { get; set; }
	public Guid? UserId { get; set; }
	public FileLifecycleEventType EventType { get; set; }
	public string Detail { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
