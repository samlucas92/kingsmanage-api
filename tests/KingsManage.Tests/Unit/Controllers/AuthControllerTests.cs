using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using KingsManage.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public class AuthControllerTests
{
	[Test]
	public async Task Login_WhenEmailIsMissing_ShouldReturnBadRequest()
	{
		var controller = CreateController();

		var result = await controller.Login(
			new LoginRequest
			{
				Email = string.Empty,
				Password = "password123"
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Login_WhenPasswordIsMissing_ShouldReturnBadRequest()
	{
		var controller = CreateController();

		var result = await controller.Login(
			new LoginRequest
			{
				Email = "admin@test.local",
				Password = string.Empty
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Login_WhenCredentialsAreInvalid_ShouldReturnUnauthorized()
	{
		var userService = new FakeUserService();
		var controller = CreateController(userService);

		var result = await controller.Login(
			new LoginRequest
			{
				Email = "admin@test.local",
				Password = "wrong-password"
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<UnauthorizedObjectResult>());
	}

	[Test]
	public async Task Login_WhenCredentialsAreValid_ShouldReturnLoginResponse()
	{
		var user = CreateUser(UserRole.Admin);
		var userService = new FakeUserService();
		userService.ValidCredentialsUser = user;
		var controller = CreateController(userService);

		var result = await controller.Login(
			new LoginRequest
			{
				Email = user.Email,
				Password = "password123"
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		Assert.That(okResult, Is.Not.Null);

		var response = okResult!.Value as LoginResponse;
		Assert.That(response, Is.Not.Null);
		Assert.That(response!.Token, Is.EqualTo("test-token"));
		Assert.That(response.User.Id, Is.EqualTo(user.Id));
		Assert.That(response.User.Role, Is.EqualTo(UserRole.Admin));
	}

	[Test]
	public async Task GetCurrentUser_WhenClaimIsMissing_ShouldReturnUnauthorized()
	{
		var controller = CreateController();
		SetUser(controller, new ClaimsPrincipal(new ClaimsIdentity()));

		var result = await controller.GetCurrentUser(CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<UnauthorizedResult>());
	}

	[Test]
	public async Task GetCurrentUser_WhenUserDoesNotExist_ShouldReturnUnauthorized()
	{
		var userId = Guid.NewGuid();
		var userService = new FakeUserService();
		var controller = CreateController(userService);
		SetUser(controller, CreatePrincipal(userId));

		var result = await controller.GetCurrentUser(CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<UnauthorizedResult>());
	}

	[Test]
	public async Task GetCurrentUser_WhenUserIsInactive_ShouldReturnUnauthorized()
	{
		var user = CreateUser(UserRole.Admin);
		user.IsActive = false;
		var userService = new FakeUserService();
		userService.Users.Add(user);
		var controller = CreateController(userService);
		SetUser(controller, CreatePrincipal(user.Id));

		var result = await controller.GetCurrentUser(CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<UnauthorizedResult>());
	}

	[Test]
	public async Task GetCurrentUser_WhenUserExistsAndIsActive_ShouldReturnCurrentUser()
	{
		var user = CreateUser(UserRole.Coach);
		var userService = new FakeUserService();
		userService.Users.Add(user);
		var controller = CreateController(userService);
		SetUser(controller, CreatePrincipal(user.Id));

		var result = await controller.GetCurrentUser(CancellationToken.None);

		var okResult = result.Result as OkObjectResult;
		Assert.That(okResult, Is.Not.Null);

		var viewModel = okResult!.Value as UserViewModel;
		Assert.That(viewModel, Is.Not.Null);
		Assert.That(viewModel!.Id, Is.EqualTo(user.Id));
		Assert.That(viewModel.Role, Is.EqualTo(UserRole.Coach));
	}

	private static AuthController CreateController(FakeUserService? userService = null)
	{
		return new AuthController(
			userService ?? new FakeUserService(),
			new FakeJwtTokenService()
		);
	}

	private static AppUser CreateUser(UserRole role)
	{
		return new AppUser
		{
			Id = Guid.NewGuid(),
			Email = $"{role.ToString().ToLowerInvariant()}@test.local",
			Role = role,
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
	}

	private static ClaimsPrincipal CreatePrincipal(Guid userId)
	{
		return new ClaimsPrincipal(
			new ClaimsIdentity(
				new[]
				{
					new Claim(ClaimTypes.NameIdentifier, userId.ToString())
				},
				"TestAuth"
			)
		);
	}

	private static void SetUser(ControllerBase controller, ClaimsPrincipal principal)
	{
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext
			{
				User = principal
			}
		};
	}

	private sealed class FakeJwtTokenService : IJwtTokenService
	{
		public LoginResponse CreateLoginResponse(AppUser user)
		{
			return new LoginResponse
			{
				Token = "test-token",
				ExpiresAt = DateTime.UtcNow.AddHours(1),
				User = UserViewModel.FromUser(user)
			};
		}
	}

	private sealed class FakeUserService : IUserService
	{
		public List<AppUser> Users { get; } = new();

		public AppUser? ValidCredentialsUser { get; set; }

		public Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<AppUser>>(Users);
		}

		public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(Users.FirstOrDefault(user => user.Id == id));
		}

		public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(Users.FirstOrDefault(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
		}

		public Task<AppUser> CreateAsync(AppUser user, string password, CancellationToken cancellationToken = default)
		{
			Users.Add(user);
			return Task.FromResult(user);
		}

		public Task<AppUser?> UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<AppUser?>(user);
		}

		public Task<AppUser?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
		{
			var user = Users.FirstOrDefault(existingUser => existingUser.Id == id);

			if (user is not null)
			{
				user.IsActive = isActive;
			}

			return Task.FromResult(user);
		}

		public Task<AppUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(ValidCredentialsUser);
		}

		public Task<AppUser> EnsureDefaultAdminUserAsync(string email, string password, CancellationToken cancellationToken = default)
		{
			var user = CreateUser(UserRole.Admin);
			user.Email = email;
			Users.Add(user);
			return Task.FromResult(user);
		}
	}
}
