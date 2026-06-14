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
		var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

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
}
