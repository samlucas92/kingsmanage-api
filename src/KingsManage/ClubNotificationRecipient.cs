namespace KingsManage;

public class ClubNotificationRecipient
{
	public Guid UserId { get; set; }
	public NotificationStatus Status { get; set; } = NotificationStatus.Unread;
	public DateTime? ReadAt { get; set; }
}
