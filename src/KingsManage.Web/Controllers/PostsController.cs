using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Realtime;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/posts")]
public class PostsController : ControllerBase
{
	private readonly IClubPostService _postService;
	private readonly IClubNotificationService _notificationService;
	private readonly IUserService _userService;
	private readonly IRealtimeNotifier _realtimeNotifier;
	private readonly RichTextAssetService _richTextAssets;

	public PostsController(
		IClubPostService postService,
		IClubNotificationService notificationService,
		IUserService userService,
		IRealtimeNotifier? realtimeNotifier = null,
		RichTextAssetService? richTextAssets = null
	)
	{
		_postService = postService;
		_notificationService = notificationService;
		_userService = userService;
		_realtimeNotifier = realtimeNotifier ?? NullRealtimeNotifier.Instance;
		_richTextAssets = richTextAssets ?? throw new ArgumentNullException(nameof(richTextAssets));
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<ClubPost>>> GetAll(
		CancellationToken cancellationToken
	)
	{
		var posts = await _postService.GetAllAsync(cancellationToken);

		return Ok(posts);
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<ClubPost>> GetById(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Post", out var postId, out var errorResult))
		{
			return errorResult!;
		}

		var post = await _postService.GetByIdAsync(postId, cancellationToken);

		if (post is null)
		{
			return NotFound();
		}

		return Ok(post);
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpPost]
	public async Task<ActionResult<ClubPost>> Create(
		CreateClubPostModel model,
		CancellationToken cancellationToken
	)
	{
		var validationError = ValidateCreateModel(model);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var userIdResult = GetCurrentUserId();

		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		var post = model.ToClubPost(
				userIdResult.UserId,
				User.FindFirstValue(ClaimTypes.Email) ?? string.Empty
			);
		post.Id = Guid.NewGuid();
		try
		{
			post.Body = await _richTextAssets.SynchronizeAsync(
				post.Body,
				null,
				ClubFileLinkedEntityType.Post,
				post.Id,
				userIdResult.UserId,
				post.CreatedByUserEmail,
				cancellationToken
			);
		}
		catch (InvalidOperationException exception)
		{
			return BadRequest(exception.Message);
		}

		var createdPost = await _postService.CreateAsync(
			post,
			cancellationToken
		);

		await CreatePostNotificationAsync(
			createdPost,
			userIdResult.UserId,
			cancellationToken
		);

		return CreatedAtAction(
			nameof(GetById),
			new { id = createdPost.Id },
			createdPost
		);
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpPut("{id}")]
	public async Task<ActionResult<ClubPost>> Update(
		string id,
		UpdateClubPostModel model,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Post", out var postId, out var errorResult))
		{
			return errorResult!;
		}

		var validationError = ValidateUpdateModel(model);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var existingPost = await _postService.GetByIdAsync(postId, cancellationToken);

		if (existingPost is null)
		{
			return NotFound();
		}

		var previousBody = existingPost.Body;
		var updated = model.ToClubPost(existingPost);
		try
		{
			updated.Body = await _richTextAssets.SynchronizeAsync(
				updated.Body,
				previousBody,
				ClubFileLinkedEntityType.Post,
				existingPost.Id,
				existingPost.CreatedByUserId,
				existingPost.CreatedByUserEmail,
				cancellationToken
			);
		}
		catch (InvalidOperationException exception)
		{
			return BadRequest(exception.Message);
		}

		var updatedPost = await _postService.UpdateAsync(
			updated,
			cancellationToken
		);

		if (updatedPost is null)
		{
			return NotFound();
		}

		return Ok(updatedPost);
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpDelete("{id}")]
	public async Task<IActionResult> Delete(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Post", out var postId, out var errorResult))
		{
			return errorResult!;
		}

		var existingPost = await _postService.GetByIdAsync(postId, cancellationToken);
		if (existingPost is null)
		{
			return NotFound();
		}

		var userIdResult = GetCurrentUserId();
		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		var deleted = await _postService.DeleteAsync(postId, cancellationToken);

		if (!deleted)
		{
			return NotFound();
		}

		await _richTextAssets.DeleteAllAsync(
			existingPost.Body,
			ClubFileLinkedEntityType.Post,
			postId,
			userIdResult.UserId,
			cancellationToken
		);

		return NoContent();
	}

	private async Task CreatePostNotificationAsync(
		ClubPost post,
		Guid createdByUserId,
		CancellationToken cancellationToken
	)
	{
		var recipients = await GetActiveUsersExceptAsync(
			createdByUserId,
			cancellationToken
		);

		if (recipients.Count == 0)
		{
			return;
		}

		var notification = await _notificationService.CreateAsync(
			new ClubNotification
			{
				Type = NotificationType.NewPost,
				SourceType = NotificationSourceType.Post,
				SourceId = post.Id,
				Title = $"New post: {post.Title}",
				Message = "A new club post has been published.",
				ActionPath = $"/posts/{post.Id}",
				CreatedByUserId = createdByUserId,
				CreatedByUserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
				Recipients = recipients
					.Select(user => new ClubNotificationRecipient { UserId = user.Id })
					.ToList()
			},
			cancellationToken
		);

		await _realtimeNotifier.NotificationCreatedAsync(notification, cancellationToken);
	}

	private async Task<List<AppUser>> GetActiveUsersExceptAsync(
		Guid excludedUserId,
		CancellationToken cancellationToken
	)
	{
		var users = await _userService.GetAllAsync(cancellationToken);

		return users
			.Where(user => user.IsActive)
			.Where(user => user.Id != excludedUserId)
			.ToList();
	}

	private static string? ValidateCreateModel(CreateClubPostModel model)
	{
		if (string.IsNullOrWhiteSpace(model.Title))
		{
			return "Post title is required.";
		}

		if (string.IsNullOrWhiteSpace(RichTextBody.ToPlainText(model.Body)))
		{
			return "Post body is required.";
		}

		return null;
	}

	private static string? ValidateUpdateModel(UpdateClubPostModel model)
	{
		if (string.IsNullOrWhiteSpace(model.Title))
		{
			return "Post title is required.";
		}

		if (string.IsNullOrWhiteSpace(RichTextBody.ToPlainText(model.Body)))
		{
			return "Post body is required.";
		}

		return null;
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
