using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class UsersController : ControllerBase
{
	private readonly IUserService _userService;

	public UsersController(IUserService userService)
	{
		_userService = userService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<UserViewModel>>> GetAll(CancellationToken cancellationToken)
	{
		var users = await _userService.GetAllAsync(cancellationToken);
		return Ok(users.Select(UserViewModel.FromUser).ToList());
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<UserViewModel>> GetById(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "User", out var userId, out var errorResult))
		{
			return errorResult!;
		}

		var user = await _userService.GetByIdAsync(userId, cancellationToken);

		if (user is null)
		{
			return NotFound();
		}

		return Ok(UserViewModel.FromUser(user));
	}

	[HttpPost]
	public async Task<ActionResult<UserViewModel>> Create(
		CreateUserRequest request,
		CancellationToken cancellationToken
	)
	{
		var validationError = ValidateCreateRequest(request);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var existingUser = await _userService.GetByEmailAsync(request.Email, cancellationToken);

		if (existingUser is not null)
		{
			return BadRequest("A user with this email already exists.");
		}

		var user = new AppUser
		{
			Email = request.Email,
			Role = request.Role,
			PlayerId = request.PlayerId,
			IsActive = request.IsActive
		};

		var createdUser = await _userService.CreateAsync(user, request.Password, cancellationToken);
		var viewModel = UserViewModel.FromUser(createdUser);

		return CreatedAtAction(nameof(GetById), new { id = viewModel.Id }, viewModel);
	}

	[HttpPut("{id}")]
	public async Task<ActionResult<UserViewModel>> Update(
		string id,
		UpdateUserRequest request,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "User", out var userId, out var errorResult))
		{
			return errorResult!;
		}

		var validationError = ValidateUpdateRequest(request);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var existingUser = await _userService.GetByIdAsync(userId, cancellationToken);

		if (existingUser is null)
		{
			return NotFound();
		}

		var userWithSameEmail = await _userService.GetByEmailAsync(request.Email, cancellationToken);

		if (userWithSameEmail is not null && userWithSameEmail.Id != userId)
		{
			return BadRequest("A user with this email already exists.");
		}

		existingUser.Email = request.Email;
		existingUser.Role = request.Role;
		existingUser.PlayerId = request.PlayerId;
		existingUser.IsActive = request.IsActive;

		var updatedUser = await _userService.UpdateAsync(existingUser, cancellationToken);

		if (updatedUser is null)
		{
			return NotFound();
		}

		return Ok(UserViewModel.FromUser(updatedUser));
	}

	[HttpPatch("{id}/active")]
	public async Task<ActionResult<UserViewModel>> SetActive(
		string id,
		[FromBody] bool isActive,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "User", out var userId, out var errorResult))
		{
			return errorResult!;
		}

		var updatedUser = await _userService.SetActiveAsync(userId, isActive, cancellationToken);

		if (updatedUser is null)
		{
			return NotFound();
		}

		return Ok(UserViewModel.FromUser(updatedUser));
	}

	private static string? ValidateCreateRequest(CreateUserRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Email))
		{
			return "Email is required.";
		}

		if (string.IsNullOrWhiteSpace(request.Password))
		{
			return "Password is required.";
		}

		if (request.Password.Length < 8)
		{
			return "Password must be at least 8 characters long.";
		}

		return null;
	}

	private static string? ValidateUpdateRequest(UpdateUserRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Email))
		{
			return "Email is required.";
		}

		return null;
	}

	private bool TryParseGuid(
		string id,
		string entityName,
		out Guid parsedId,
		out BadRequestObjectResult? errorResult
	)
	{
		parsedId = Guid.Empty;
		errorResult = null;

		if (string.IsNullOrWhiteSpace(id))
		{
			errorResult = BadRequest($"{entityName} id is required.");
			return false;
		}

		if (!Guid.TryParse(id, out parsedId))
		{
			errorResult = BadRequest($"{entityName} id must be a valid GUID.");
			return false;
		}

		return true;
	}
}
