using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
	private readonly IClubNotificationService _notificationService;

	public NotificationsController(IClubNotificationService notificationService)
	{
		_notificationService = notificationService;
	}

	[HttpGet("mine")]
	public async Task<ActionResult<IReadOnlyList<NotificationViewModel>>> GetMine(
		[FromQuery] bool unreadOnly,
		CancellationToken cancellationToken
	)
	{
		var userIdResult = GetCurrentUserId();

		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		var notifications = await _notificationService.GetForUserAsync(
			userIdResult.UserId,
			unreadOnly,
			cancellationToken
		);

		return Ok(
			notifications
				.Select(notification => NotificationViewModel.FromNotification(notification, userIdResult.UserId))
				.ToList()
		);
	}

	[HttpGet("unread-count")]
	public async Task<ActionResult<UnreadNotificationCountResponse>> GetUnreadCount(
		CancellationToken cancellationToken
	)
	{
		var userIdResult = GetCurrentUserId();

		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		var unreadCount = await _notificationService.GetUnreadCountAsync(
			userIdResult.UserId,
			cancellationToken
		);

		return Ok(new UnreadNotificationCountResponse { UnreadCount = unreadCount });
	}

	[HttpPost("{id}/mark-read")]
	public async Task<ActionResult<NotificationViewModel>> MarkRead(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Notification", out var notificationId, out var errorResult))
		{
			return errorResult!;
		}

		var userIdResult = GetCurrentUserId();

		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		var notification = await _notificationService.MarkReadAsync(
			notificationId,
			userIdResult.UserId,
			cancellationToken
		);

		if (notification is null)
		{
			return NotFound();
		}

		return Ok(NotificationViewModel.FromNotification(notification, userIdResult.UserId));
	}

	[HttpPost("mark-all-read")]
	public async Task<ActionResult<MarkAllNotificationsReadResponse>> MarkAllRead(
		CancellationToken cancellationToken
	)
	{
		var userIdResult = GetCurrentUserId();

		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		var updatedCount = await _notificationService.MarkAllReadAsync(
			userIdResult.UserId,
			cancellationToken
		);

		return Ok(new MarkAllNotificationsReadResponse { UpdatedCount = updatedCount });
	}

	private static bool TryParseGuid(
		string value,
		string label,
		out Guid id,
		out ActionResult? errorResult
	)
	{
		if (Guid.TryParse(value, out id))
		{
			errorResult = null;
			return true;
		}

		errorResult = new BadRequestObjectResult($"{label} id is invalid.");
		return false;
	}

	private CurrentUserIdResult GetCurrentUserId()
	{
		var userIdClaim =
			User.FindFirstValue(ClaimTypes.NameIdentifier) ??
			User.FindFirstValue("sub") ??
			User.FindFirstValue("id");

		if (string.IsNullOrWhiteSpace(userIdClaim))
		{
			return CurrentUserIdResult.Failed("Current user id was not found in the auth token.");
		}

		if (!Guid.TryParse(userIdClaim, out var userId))
		{
			return CurrentUserIdResult.Failed("Current user id in the auth token is invalid.");
		}

		return CurrentUserIdResult.SuccessResult(userId);
	}

	private sealed record CurrentUserIdResult(
		bool Success,
		Guid UserId,
		string ErrorMessage
	)
	{
		public static CurrentUserIdResult SuccessResult(Guid userId) =>
			new(true, userId, string.Empty);

		public static CurrentUserIdResult Failed(string errorMessage) =>
			new(false, Guid.Empty, errorMessage);
	}
}
