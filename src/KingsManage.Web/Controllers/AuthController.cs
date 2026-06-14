using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
	private readonly IUserService _userService;
	private readonly IJwtTokenService _jwtTokenService;

	public AuthController(
		IUserService userService,
		IJwtTokenService jwtTokenService
	)
	{
		_userService = userService;
		_jwtTokenService = jwtTokenService;
	}

	[HttpPost("login")]
	[AllowAnonymous]
	public async Task<ActionResult<LoginResponse>> Login(
		LoginRequest request,
		CancellationToken cancellationToken
	)
	{
		if (string.IsNullOrWhiteSpace(request.Email))
		{
			return BadRequest("Email is required.");
		}

		if (string.IsNullOrWhiteSpace(request.Password))
		{
			return BadRequest("Password is required.");
		}

		var user = await _userService.ValidateCredentialsAsync(
			request.Email,
			request.Password,
			cancellationToken
		);

		if (user is null)
		{
			return Unauthorized("Invalid email or password.");
		}

		return Ok(_jwtTokenService.CreateLoginResponse(user));
	}

	[HttpGet("me")]
	[Authorize]
	public async Task<ActionResult<UserViewModel>> GetCurrentUser(CancellationToken cancellationToken)
	{
		var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

		if (!Guid.TryParse(userIdClaim, out var userId))
		{
			return Unauthorized();
		}

		var user = await _userService.GetByIdAsync(userId, cancellationToken);

		if (user is null || !user.IsActive)
		{
			return Unauthorized();
		}

		return Ok(UserViewModel.FromUser(user));
	}

	[HttpPost("change-password")]
	[Authorize]
	public async Task<IActionResult> ChangePassword(
		ChangePasswordRequest request,
		CancellationToken cancellationToken
	)
	{
		var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

		if (!Guid.TryParse(userIdClaim, out var userId))
		{
			return Unauthorized();
		}

		var validationError = ValidateChangePasswordRequest(request);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var changed = await _userService.ChangePasswordAsync(
			userId,
			request.CurrentPassword,
			request.NewPassword,
			cancellationToken
		);

		if (!changed)
		{
			return BadRequest("Current password is incorrect.");
		}

		return NoContent();
	}

	private static string? ValidateChangePasswordRequest(ChangePasswordRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.CurrentPassword))
		{
			return "Current password is required.";
		}

		if (string.IsNullOrWhiteSpace(request.NewPassword))
		{
			return "New password is required.";
		}

		if (request.NewPassword.Length < 8)
		{
			return "New password must be at least 8 characters long.";
		}

		if (request.CurrentPassword == request.NewPassword)
		{
			return "New password must be different to the current password.";
		}

		return null;
	}
}
