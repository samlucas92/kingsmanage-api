namespace KingsManage;

public class ClubNotification : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public NotificationType Type { get; set; }
	public NotificationSourceType SourceType { get; set; }
	public Guid? SourceId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string ActionPath { get; set; } = string.Empty;
	public Guid? CreatedByUserId { get; set; }
	public string CreatedByUserEmail { get; set; } = string.Empty;
	public List<ClubNotificationRecipient> Recipients { get; set; } = [];
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
