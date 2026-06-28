using KingsManage;

namespace KingsManage.Web.Realtime;

public interface IRealtimeNotifier
{
	Task NotificationCreatedAsync(
		ClubNotification notification,
		CancellationToken cancellationToken = default
	);

	Task NotificationsChangedAsync(
		Guid organizationId,
		Guid clubId,
		Guid userId,
		CancellationToken cancellationToken = default
	);

	Task MessageCreatedAsync(
		MessageThread thread,
		Message message,
		CancellationToken cancellationToken = default
	);

	Task MessageDeletedAsync(
		MessageThread thread,
		Message message,
		CancellationToken cancellationToken = default
	);

	Task ThreadChangedAsync(
		MessageThread thread,
		CancellationToken cancellationToken = default
	);
}

public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
	public static NullRealtimeNotifier Instance { get; } = new();

	private NullRealtimeNotifier()
	{
	}

	public Task NotificationCreatedAsync(ClubNotification notification, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	public Task NotificationsChangedAsync(Guid organizationId, Guid clubId, Guid userId, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	public Task MessageCreatedAsync(MessageThread thread, Message message, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	public Task MessageDeletedAsync(MessageThread thread, Message message, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	public Task ThreadChangedAsync(MessageThread thread, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;
}
