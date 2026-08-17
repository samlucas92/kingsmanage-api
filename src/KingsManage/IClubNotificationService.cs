namespace KingsManage;

public interface IClubNotificationService
{
	Task<IReadOnlyList<ClubNotification>> GetForUserAsync(
		Guid userId,
		bool unreadOnly = false,
		CancellationToken cancellationToken = default
	);

	Task<int> GetUnreadCountAsync(
		Guid userId,
		CancellationToken cancellationToken = default
	);

	Task<ClubNotification> CreateAsync(
		ClubNotification notification,
		CancellationToken cancellationToken = default
	);

	Task<bool> ExistsAsync(
		NotificationType type,
		Guid sourceId,
		Guid userId,
		CancellationToken cancellationToken = default
	) => Task.FromResult(false);

	Task<ClubNotification?> MarkReadAsync(
		Guid notificationId,
		Guid userId,
		CancellationToken cancellationToken = default
	);

	Task<int> MarkAllReadAsync(
		Guid userId,
		CancellationToken cancellationToken = default
	);
}
