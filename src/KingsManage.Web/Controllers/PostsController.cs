using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/posts")]
public class PostsController : ControllerBase
{
	private readonly IClubPostService _postService;

	public PostsController(IClubPostService postService)
	{
		_postService = postService;
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

	[Authorize(Roles = "Admin,Coach")]
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

		var createdPost = await _postService.CreateAsync(
			model.ToClubPost(
				userIdResult.UserId,
				User.FindFirstValue(ClaimTypes.Email) ?? string.Empty
			),
			cancellationToken
		);

		return CreatedAtAction(
			nameof(GetById),
			new { id = createdPost.Id },
			createdPost
		);
	}

	[Authorize(Roles = "Admin,Coach")]
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

		var updatedPost = await _postService.UpdateAsync(
			model.ToClubPost(existingPost),
			cancellationToken
		);

		if (updatedPost is null)
		{
			return NotFound();
		}

		return Ok(updatedPost);
	}

	[Authorize(Roles = "Admin,Coach")]
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

		var deleted = await _postService.DeleteAsync(postId, cancellationToken);

		if (!deleted)
		{
			return NotFound();
		}

		return NoContent();
	}

	private static string? ValidateCreateModel(CreateClubPostModel model)
	{
		if (string.IsNullOrWhiteSpace(model.Title))
		{
			return "Post title is required.";
		}

		if (string.IsNullOrWhiteSpace(model.Body))
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

		if (string.IsNullOrWhiteSpace(model.Body))
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
