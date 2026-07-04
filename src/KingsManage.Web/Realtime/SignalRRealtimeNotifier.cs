using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.SignalR;

namespace KingsManage.Web.Realtime;

public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
	private readonly IHubContext<ClubHub> hubContext;

	public SignalRRealtimeNotifier(IHubContext<ClubHub> hubContext)
	{
		this.hubContext = hubContext;
	}

	public async Task NotificationCreatedAsync(
		ClubNotification notification,
		CancellationToken cancellationToken = default
	)
	{
		foreach (var recipient in notification.Recipients)
		{
			await hubContext.Clients
				.Group(RealtimeGroups.User(
					notification.OrganizationId,
					notification.ClubId,
					recipient.UserId
				))
				.SendAsync(
					"NotificationReceived",
					NotificationViewModel.FromNotification(notification, recipient.UserId),
					cancellationToken
				);
		}
	}

	public Task NotificationsChangedAsync(
		Guid organizationId,
		Guid clubId,
		Guid userId,
		CancellationToken cancellationToken = default
	) =>
		hubContext.Clients
			.Group(RealtimeGroups.User(organizationId, clubId, userId))
			.SendAsync("NotificationsChanged", cancellationToken: cancellationToken);

	public Task MessageCreatedAsync(
		MessageThread thread,
		Message message,
		CancellationToken cancellationToken = default
	) =>
		SendToParticipantsAsync(
			thread,
			"MessageReceived",
			message,
			cancellationToken
		);

	public Task MessageDeletedAsync(
		MessageThread thread,
		Message message,
		CancellationToken cancellationToken = default
	) =>
		SendToParticipantsAsync(
			thread,
			"MessageDeleted",
			new
			{
				MessageId = message.Id,
				message.ThreadId,
				message.DeletedAt
			},
			cancellationToken
		);

	public Task ThreadChangedAsync(
		MessageThread thread,
		CancellationToken cancellationToken = default
	) =>
		SendToParticipantsAsync(
			thread,
			"ThreadChanged",
			new { ThreadId = thread.Id },
			cancellationToken
		);

	private Task SendToParticipantsAsync(
		MessageThread thread,
		string eventName,
		object payload,
		CancellationToken cancellationToken
	)
	{
		var groups = thread.Participants
			.Select(participant => RealtimeGroups.User(
				thread.OrganizationId,
				thread.ClubId,
				participant.UserId
			))
			.Distinct()
			.ToList();

		return hubContext.Clients
			.Groups(groups)
			.SendAsync(eventName, payload, cancellationToken);
	}
}
