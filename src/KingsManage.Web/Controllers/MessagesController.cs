using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
	private const int MaximumMessageLength = 4000;
	private readonly IMessageService _messageService;
	private readonly IUserService _userService;
	private readonly IClubNotificationService _notificationService;
	private readonly IRealtimeNotifier _realtimeNotifier;

	public MessagesController(
		IMessageService messageService,
		IUserService userService,
		IClubNotificationService notificationService,
		IRealtimeNotifier? realtimeNotifier = null
	)
	{
		_messageService = messageService;
		_userService = userService;
		_notificationService = notificationService;
		_realtimeNotifier = realtimeNotifier ?? NullRealtimeNotifier.Instance;
	}

	[HttpGet("users")]
	public async Task<IActionResult> GetAvailableUsers(CancellationToken cancellationToken)
	{
		var currentUserId = GetCurrentUserId();
		if (!currentUserId.HasValue)
		{
			return BadRequest("Current user id was not found in the auth token.");
		}

		var users = await _userService.GetAllAsync(cancellationToken);
		return Ok(users
			.Where(user => user.IsActive && user.Id != currentUserId.Value)
			.Select(user => new { user.Id, user.Email, user.Role, user.PlayerId })
			.OrderBy(user => user.Email));
	}

	[HttpGet("threads")]
	public async Task<IActionResult> GetThreads(CancellationToken cancellationToken)
	{
		var currentUserId = GetCurrentUserId();
		if (!currentUserId.HasValue)
		{
			return BadRequest("Current user id was not found in the auth token.");
		}

		var threads = await _messageService.GetThreadsForUserAsync(currentUserId.Value, cancellationToken);
		var results = new List<object>();

		foreach (var thread in threads)
		{
			var messages = await _messageService.GetMessagesAsync(thread.Id, cancellationToken);
			var participant = thread.Participants.First(item => item.UserId == currentUserId.Value);
			var lastMessage = messages.LastOrDefault();
			var unreadCount = messages.Count(message =>
				message.Status == MessageStatus.Active &&
				message.SenderUserId != currentUserId.Value &&
				(!participant.LastReadAt.HasValue || message.CreatedAt > participant.LastReadAt.Value));

			results.Add(new { Thread = thread, LastMessage = lastMessage, UnreadCount = unreadCount });
		}

		return Ok(results);
	}

	[HttpPost("threads/direct")]
	public async Task<IActionResult> CreateDirectThread(
		CreateDirectMessageThreadModel model,
		CancellationToken cancellationToken
	)
	{
		var currentUserId = GetCurrentUserId();
		if (!currentUserId.HasValue)
		{
			return BadRequest("Current user id was not found in the auth token.");
		}

		if (model.UserId == Guid.Empty || model.UserId == currentUserId.Value)
		{
			return BadRequest("A different target user is required.");
		}

		var targetUser = await _userService.GetByIdAsync(model.UserId, cancellationToken);
		if (targetUser is null || !targetUser.IsActive)
		{
			return BadRequest("The target user is not active.");
		}

		var thread = await _messageService.GetOrCreateDirectThreadAsync(
			currentUserId.Value,
			model.UserId,
			cancellationToken
		);

		return Ok(thread);
	}

	[HttpGet("threads/{threadId}")]
	public async Task<IActionResult> GetThread(string threadId, CancellationToken cancellationToken)
	{
		if (!Guid.TryParse(threadId, out var parsedThreadId))
		{
			return BadRequest("Message thread id is invalid.");
		}

		var currentUserId = GetCurrentUserId();
		if (!currentUserId.HasValue)
		{
			return BadRequest("Current user id was not found in the auth token.");
		}

		var thread = await _messageService.GetThreadForUserAsync(parsedThreadId, currentUserId.Value, cancellationToken);
		if (thread is null)
		{
			return NotFound();
		}

		var messages = await _messageService.GetMessagesAsync(parsedThreadId, cancellationToken);
		return Ok(new { Thread = thread, Messages = messages });
	}

	[HttpPost("threads/{threadId}/messages")]
	public async Task<IActionResult> SendMessage(
		string threadId,
		CreateMessageModel model,
		CancellationToken cancellationToken
	)
	{
		if (!Guid.TryParse(threadId, out var parsedThreadId))
		{
			return BadRequest("Message thread id is invalid.");
		}

		if (string.IsNullOrWhiteSpace(model.Body))
		{
			return BadRequest("Message body is required.");
		}

		if (model.Body.Trim().Length > MaximumMessageLength)
		{
			return BadRequest($"Message body cannot exceed {MaximumMessageLength} characters.");
		}

		var currentUserId = GetCurrentUserId();
		if (!currentUserId.HasValue)
		{
			return BadRequest("Current user id was not found in the auth token.");
		}

		var thread = await _messageService.GetThreadForUserAsync(parsedThreadId, currentUserId.Value, cancellationToken);
		if (thread is null)
		{
			return NotFound();
		}

		var currentUser = await _userService.GetByIdAsync(currentUserId.Value, cancellationToken);
		if (currentUser is null || !currentUser.IsActive)
		{
			return Forbid();
		}

		var message = await _messageService.CreateMessageAsync(new Message
		{
			ThreadId = parsedThreadId,
			SenderUserId = currentUser.Id,
			SenderUserEmail = currentUser.Email,
			Body = model.Body
		}, cancellationToken);

		await CreateMessageNotificationAsync(thread, message, currentUser, cancellationToken);
		await _realtimeNotifier.MessageCreatedAsync(thread, message, cancellationToken);
		return CreatedAtAction(nameof(GetThread), new { threadId = parsedThreadId }, message);
	}

	[HttpPost("threads/{threadId}/mark-read")]
	public async Task<IActionResult> MarkRead(string threadId, CancellationToken cancellationToken)
	{
		if (!Guid.TryParse(threadId, out var parsedThreadId))
		{
			return BadRequest("Message thread id is invalid.");
		}

		var currentUserId = GetCurrentUserId();
		if (!currentUserId.HasValue)
		{
			return BadRequest("Current user id was not found in the auth token.");
		}

		var thread = await _messageService.MarkReadAsync(parsedThreadId, currentUserId.Value, cancellationToken);
		if (thread is null)
		{
			return NotFound();
		}

		await _realtimeNotifier.ThreadChangedAsync(thread, cancellationToken);
		return Ok(thread);
	}

	[HttpDelete("{messageId}")]
	public async Task<IActionResult> DeleteMessage(string messageId, CancellationToken cancellationToken)
	{
		if (!Guid.TryParse(messageId, out var parsedMessageId))
		{
			return BadRequest("Message id is invalid.");
		}

		var currentUserId = GetCurrentUserId();
		if (!currentUserId.HasValue)
		{
			return BadRequest("Current user id was not found in the auth token.");
		}

		var message = await _messageService.DeleteOwnMessageAsync(parsedMessageId, currentUserId.Value, cancellationToken);
		if (message is null)
		{
			return NotFound();
		}

		var thread = await _messageService.GetThreadForUserAsync(
			message.ThreadId,
			currentUserId.Value,
			cancellationToken
		);

		if (thread is not null)
		{
			await _realtimeNotifier.MessageDeletedAsync(thread, message, cancellationToken);
		}

		return NoContent();
	}

	private async Task CreateMessageNotificationAsync(
		MessageThread thread,
		Message message,
		AppUser sender,
		CancellationToken cancellationToken
	)
	{
		var users = await _userService.GetAllAsync(cancellationToken);
		var activeUserIds = users.Where(user => user.IsActive).Select(user => user.Id).ToHashSet();
		var recipients = thread.Participants
			.Where(participant => participant.UserId != sender.Id && activeUserIds.Contains(participant.UserId))
			.Select(participant => new ClubNotificationRecipient { UserId = participant.UserId })
			.ToList();

		if (recipients.Count == 0)
		{
			return;
		}

		var notification = await _notificationService.CreateAsync(new ClubNotification
		{
			Type = NotificationType.NewDirectMessage,
			SourceType = NotificationSourceType.Message,
			SourceId = message.Id,
			Title = $"New message from {sender.Email}",
			Message = message.Body.Length > 120 ? $"{message.Body[..117]}..." : message.Body,
			ActionPath = $"/dashboard?tab=messages&threadId={thread.Id}",
			CreatedByUserId = sender.Id,
			CreatedByUserEmail = sender.Email,
			Recipients = recipients
		}, cancellationToken);

		await _realtimeNotifier.NotificationCreatedAsync(notification, cancellationToken);
	}

	private Guid? GetCurrentUserId()
	{
		var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("id");
		return Guid.TryParse(claim, out var userId) ? userId : null;
	}
}
