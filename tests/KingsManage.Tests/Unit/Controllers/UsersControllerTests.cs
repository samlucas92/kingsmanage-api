using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public class UsersControllerTests
{
	[Test]
	public async Task GetAll_ShouldReturnAllUsers()
	{
		var userService = new FakeUserService();
		userService.Users.Add(CreateUser(UserRole.Admin));
		userService.Users.Add(CreateUser(UserRole.Coach));
		var controller = new UsersController(userService);

		var result = await controller.GetAll(CancellationToken.None);

		var okResult = result.Result as OkObjectResult;

		Assert.That(okResult, Is.Not.Null);

		var users = okResult!.Value as IReadOnlyList<UserViewModel>;

		Assert.That(users, Is.Not.Null);
		Assert.That(users!.Count, Is.EqualTo(2));
	}

	[Test]
	public async Task GetById_WhenIdIsInvalid_ShouldReturnBadRequest()
	{
		var controller = new UsersController(new FakeUserService());

		var result = await controller.GetById("not-a-guid", CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task GetById_WhenUserDoesNotExist_ShouldReturnNotFound()
	{
		var controller = new UsersController(new FakeUserService());

		var result = await controller.GetById(Guid.NewGuid().ToString(), CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task GetById_WhenUserExists_ShouldReturnUser()
	{
		var user = CreateUser(UserRole.Player);
		var userService = new FakeUserService();
		userService.Users.Add(user);
		var controller = new UsersController(userService);

		var result = await controller.GetById(user.Id.ToString(), CancellationToken.None);

		var okResult = result.Result as OkObjectResult;

		Assert.That(okResult, Is.Not.Null);

		var viewModel = okResult!.Value as UserViewModel;

		Assert.That(viewModel, Is.Not.Null);
		Assert.That(viewModel!.Id, Is.EqualTo(user.Id));
	}

	[Test]
	public async Task Create_WhenEmailIsMissing_ShouldReturnBadRequest()
	{
		var controller = new UsersController(new FakeUserService());

		var result = await controller.Create(
			new CreateUserRequest
			{
				Email = string.Empty,
				Password = "password123",
				Role = UserRole.Admin
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Create_WhenPasswordIsTooShort_ShouldReturnBadRequest()
	{
		var controller = new UsersController(new FakeUserService());

		var result = await controller.Create(
			new CreateUserRequest
			{
				Email = "new@test.local",
				Password = "short",
				Role = UserRole.Admin
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Create_WhenEmailAlreadyExists_ShouldReturnBadRequest()
	{
		var existingUser = CreateUser(UserRole.Admin);
		var userService = new FakeUserService();
		userService.Users.Add(existingUser);
		var controller = new UsersController(userService);

		var result = await controller.Create(
			new CreateUserRequest
			{
				Email = existingUser.Email,
				Password = "password123",
				Role = UserRole.Coach
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Create_WhenRequestIsValid_ShouldCreateUser()
	{
		var playerId = Guid.NewGuid();
		var userService = new FakeUserService();
		var controller = new UsersController(userService);

		var result = await controller.Create(
			new CreateUserRequest
			{
				Email = "player@test.local",
				Password = "password123",
				Role = UserRole.Player,
				PlayerId = playerId,
				IsActive = true
			},
			CancellationToken.None
		);

		var createdResult = result.Result as CreatedAtActionResult;

		Assert.That(createdResult, Is.Not.Null);

		var viewModel = createdResult!.Value as UserViewModel;

		Assert.That(viewModel, Is.Not.Null);
		Assert.That(viewModel!.Email, Is.EqualTo("player@test.local"));
		Assert.That(viewModel.Role, Is.EqualTo(UserRole.Player));
		Assert.That(viewModel.PlayerId, Is.EqualTo(playerId));
		Assert.That(userService.Users.Count, Is.EqualTo(1));
	}

	[Test]
	public async Task Update_WhenUserDoesNotExist_ShouldReturnNotFound()
	{
		var controller = new UsersController(new FakeUserService());

		var result = await controller.Update(
			Guid.NewGuid().ToString(),
			new UpdateUserRequest
			{
				Email = "updated@test.local",
				Role = UserRole.Coach,
				IsActive = true
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task Update_WhenEmailBelongsToAnotherUser_ShouldReturnBadRequest()
	{
		var user = CreateUser(UserRole.Admin);
		var otherUser = CreateUser(UserRole.Coach);
		var userService = new FakeUserService();
		userService.Users.Add(user);
		userService.Users.Add(otherUser);
		var controller = new UsersController(userService);

		var result = await controller.Update(
			user.Id.ToString(),
			new UpdateUserRequest
			{
				Email = otherUser.Email,
				Role = UserRole.Admin,
				IsActive = true
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task Update_WhenRequestIsValid_ShouldUpdateUser()
	{
		var user = CreateUser(UserRole.Admin);
		var playerId = Guid.NewGuid();
		var userService = new FakeUserService();
		userService.Users.Add(user);
		var controller = new UsersController(userService);

		var result = await controller.Update(
			user.Id.ToString(),
			new UpdateUserRequest
			{
				Email = "updated@test.local",
				Role = UserRole.Player,
				PlayerId = playerId,
				IsActive = false
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;

		Assert.That(okResult, Is.Not.Null);

		var viewModel = okResult!.Value as UserViewModel;

		Assert.That(viewModel, Is.Not.Null);
		Assert.That(viewModel!.Email, Is.EqualTo("updated@test.local"));
		Assert.That(viewModel.Role, Is.EqualTo(UserRole.Player));
		Assert.That(viewModel.PlayerId, Is.EqualTo(playerId));
		Assert.That(viewModel.IsActive, Is.False);
	}

	[Test]
	public async Task SetActive_WhenUserDoesNotExist_ShouldReturnNotFound()
	{
		var controller = new UsersController(new FakeUserService());

		var result = await controller.SetActive(Guid.NewGuid().ToString(), false, CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task SetActive_WhenUserExists_ShouldUpdateActiveState()
	{
		var user = CreateUser(UserRole.Admin);
		var userService = new FakeUserService();
		userService.Users.Add(user);
		var controller = new UsersController(userService);

		var result = await controller.SetActive(user.Id.ToString(), false, CancellationToken.None);

		var okResult = result.Result as OkObjectResult;

		Assert.That(okResult, Is.Not.Null);

		var viewModel = okResult!.Value as UserViewModel;

		Assert.That(viewModel, Is.Not.Null);
		Assert.That(viewModel!.IsActive, Is.False);
		Assert.That(user.IsActive, Is.False);
	}

	[Test]
	public async Task ResetPassword_WhenIdIsInvalid_ShouldReturnBadRequest()
	{
		var controller = new UsersController(new FakeUserService());

		var result = await controller.ResetPassword(
			"not-a-guid",
			new ResetPasswordRequest
			{
				NewPassword = "NewPassword123!"
			},
			CancellationToken.None
		);

		Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task ResetPassword_WhenPasswordIsTooShort_ShouldReturnBadRequest()
	{
		var controller = new UsersController(new FakeUserService());

		var result = await controller.ResetPassword(
			Guid.NewGuid().ToString(),
			new ResetPasswordRequest
			{
				NewPassword = "short"
			},
			CancellationToken.None
		);

		Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task ResetPassword_WhenUserDoesNotExist_ShouldReturnNotFound()
	{
		var controller = new UsersController(new FakeUserService());

		var result = await controller.ResetPassword(
			Guid.NewGuid().ToString(),
			new ResetPasswordRequest
			{
				NewPassword = "NewPassword123!"
			},
			CancellationToken.None
		);

		Assert.That(result, Is.TypeOf<NotFoundResult>());
	}

	[Test]
	public async Task ResetPassword_WhenUserExists_ShouldResetPassword()
	{
		var user = CreateUser(UserRole.Coach);
		var userService = new FakeUserService();
		userService.Users.Add(user);
		var controller = new UsersController(userService);

		var result = await controller.ResetPassword(
			user.Id.ToString(),
			new ResetPasswordRequest
			{
				NewPassword = "NewPassword123!"
			},
			CancellationToken.None
		);

		Assert.That(result, Is.TypeOf<NoContentResult>());
		Assert.That(userService.LastResetPasswordUserId, Is.EqualTo(user.Id));
		Assert.That(userService.LastResetPassword, Is.EqualTo("NewPassword123!"));
	}

	private static AppUser CreateUser(UserRole role)
	{
		return new AppUser
		{
			Id = Guid.NewGuid(),
			Email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@test.local",
			Role = role,
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
	}

	private sealed class FakeUserService : IUserService
	{
		public List<AppUser> Users { get; } = new();
		public Guid? LastResetPasswordUserId { get; private set; }
		public string? LastResetPassword { get; private set; }

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
			return Task.FromResult(
				Users.FirstOrDefault(user =>
					user.Email.Equals(email, StringComparison.OrdinalIgnoreCase)
				)
			);
		}

		public Task<AppUser> CreateAsync(
			AppUser user,
			string password,
			CancellationToken cancellationToken = default
		)
		{
			user.Id = user.Id == Guid.Empty ? Guid.NewGuid() : user.Id;
			user.CreatedAt = DateTime.UtcNow;
			user.UpdatedAt = DateTime.UtcNow;
			Users.Add(user);

			return Task.FromResult(user);
		}

		public Task<AppUser?> UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
		{
			var existingUser = Users.FirstOrDefault(storedUser => storedUser.Id == user.Id);

			if (existingUser is null)
			{
				return Task.FromResult<AppUser?>(null);
			}

			existingUser.Email = user.Email;
			existingUser.Role = user.Role;
			existingUser.PlayerId = user.PlayerId;
			existingUser.IsActive = user.IsActive;
			existingUser.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<AppUser?>(existingUser);
		}

		public Task<AppUser?> SetActiveAsync(
			Guid id,
			bool isActive,
			CancellationToken cancellationToken = default
		)
		{
			var user = Users.FirstOrDefault(existingUser => existingUser.Id == id);

			if (user is not null)
			{
				user.IsActive = isActive;
				user.UpdatedAt = DateTime.UtcNow;
			}

			return Task.FromResult(user);
		}

		public Task<AppUser?> ValidateCredentialsAsync(
			string email,
			string password,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<AppUser?>(null);
		}

		public Task<bool> ChangePasswordAsync(
			Guid id,
			string currentPassword,
			string newPassword,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(true);
		}

		public Task<bool> ResetPasswordAsync(
			Guid id,
			string newPassword,
			CancellationToken cancellationToken = default
		)
		{
			var user = Users.FirstOrDefault(existingUser => existingUser.Id == id);

			if (user is null)
			{
				return Task.FromResult(false);
			}

			LastResetPasswordUserId = id;
			LastResetPassword = newPassword;

			return Task.FromResult(true);
		}

		public Task<AppUser> EnsureDefaultAdminUserAsync(
			string email,
			string password,
			CancellationToken cancellationToken = default
		)
		{
			var existingUser = Users.FirstOrDefault(user =>
				user.Email.Equals(email, StringComparison.OrdinalIgnoreCase)
			);

			if (existingUser is not null)
			{
				return Task.FromResult(existingUser);
			}

			var adminUser = new AppUser
			{
				Id = Guid.NewGuid(),
				Email = email,
				Role = UserRole.Admin,
				IsActive = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			Users.Add(adminUser);

			return Task.FromResult(adminUser);
		}
	}
}
