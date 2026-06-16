using KingsManage;

namespace KingsManage.Web.Models;

public sealed class NotificationViewModel
{
	public Guid Id { get; set; }
	public NotificationType Type { get; set; }
	public NotificationSourceType SourceType { get; set; }
	public Guid? SourceId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string ActionPath { get; set; } = string.Empty;
	public NotificationStatus Status { get; set; }
	public bool IsRead { get; set; }
	public DateTime? ReadAt { get; set; }
	public DateTime CreatedAt { get; set; }
	public string CreatedByUserEmail { get; set; } = string.Empty;

	public static NotificationViewModel FromNotification(
		ClubNotification notification,
		Guid userId
	)
	{
		var recipient = notification.Recipients.FirstOrDefault(currentRecipient => currentRecipient.UserId == userId);
		var status = recipient?.Status ?? NotificationStatus.Unread;

		return new NotificationViewModel
		{
			Id = notification.Id,
			Type = notification.Type,
			SourceType = notification.SourceType,
			SourceId = notification.SourceId,
			Title = notification.Title,
			Message = notification.Message,
			ActionPath = notification.ActionPath,
			Status = status,
			IsRead = status == NotificationStatus.Read,
			ReadAt = recipient?.ReadAt,
			CreatedAt = notification.CreatedAt,
			CreatedByUserEmail = notification.CreatedByUserEmail
		};
	}
}
