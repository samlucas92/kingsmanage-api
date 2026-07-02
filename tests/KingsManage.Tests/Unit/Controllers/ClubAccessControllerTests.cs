using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using KingsManage.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public sealed class ClubAccessControllerTests
{
	private static readonly Guid OrganizationId = Guid.NewGuid();
	private static readonly Guid CurrentClubId = Guid.NewGuid();

	[Test]
	public async Task GetAvailableClubs_OrganizationMemberCanSeeEveryActiveClub()
	{
		var user = CreateUser(new UserMembership
		{
			OrganizationId = OrganizationId,
			ClubId = null,
			Role = TenantRole.OrganizationAdmin
		});
		var clubs = new StubClubService
		{
			Clubs =
			[
				new SportsClub { Id = CurrentClubId, OrganizationId = OrganizationId, Name = "Football", IsActive = true },
				new SportsClub { Id = Guid.NewGuid(), OrganizationId = OrganizationId, Name = "Rugby", IsActive = true },
				new SportsClub { Id = Guid.NewGuid(), OrganizationId = OrganizationId, Name = "Archived", IsActive = false }
			]
		};
		var controller = CreateController(user, clubs);

		var result = await controller.GetAvailableClubs(CancellationToken.None);

		var ok = result.Result as OkObjectResult;
		var available = ok?.Value as IReadOnlyList<ClubAccessViewModel>;
		Assert.That(available, Has.Count.EqualTo(2));
		Assert.That(available!.Single(club => club.Id == CurrentClubId).IsCurrent, Is.True);
	}

	[Test]
	public async Task SwitchClub_ReturnsForbiddenWhenUserDoesNotHaveAccess()
	{
		var targetClub = new SportsClub { Id = Guid.NewGuid(), OrganizationId = OrganizationId, IsActive = true };
		var user = CreateUser(new UserMembership
		{
			OrganizationId = OrganizationId,
			ClubId = CurrentClubId,
			Role = TenantRole.Player
		});
		var userService = new StubUserService(user) { AllowSwitch = false };
		var controller = CreateController(user, new StubClubService { Clubs = [targetClub] }, userService);

		var result = await controller.SwitchClub(new SwitchClubRequest { ClubId = targetClub.Id }, CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<ForbidResult>());
	}

	[Test]
	public async Task SwitchClub_ReturnsReplacementTokenAndRemembersClub()
	{
		var targetClub = new SportsClub { Id = Guid.NewGuid(), OrganizationId = OrganizationId, IsActive = true };
		var user = CreateUser(new UserMembership
		{
			OrganizationId = OrganizationId,
			ClubId = null,
			Role = TenantRole.OrganizationAdmin
		});
		var userService = new StubUserService(user);
		var controller = CreateController(user, new StubClubService { Clubs = [targetClub] }, userService);

		var result = await controller.SwitchClub(new SwitchClubRequest { ClubId = targetClub.Id }, CancellationToken.None);

		var ok = result.Result as OkObjectResult;
		var response = ok?.Value as LoginResponse;
		Assert.That(response?.Token, Is.EqualTo("switched-token"));
		Assert.That(userService.SelectedClubId, Is.EqualTo(targetClub.Id));
	}

	private static ClubAccessController CreateController(
		AppUser user,
		StubClubService clubs,
		StubUserService? users = null)
	{
		var controller = new ClubAccessController(
			users ?? new StubUserService(user),
			clubs,
			new StubTokenService(),
			new StubTenantContext());
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext
			{
				User = new ClaimsPrincipal(new ClaimsIdentity(
					[new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "Test"))
			}
		};
		return controller;
	}

	private static AppUser CreateUser(UserMembership membership) => new()
	{
		Id = Guid.NewGuid(),
		Email = "user@test.local",
		Role = UserRole.Admin,
		IsActive = true,
		Memberships = [membership]
	};

	private sealed class StubTenantContext : ITenantContext
	{
		public Guid OrganizationId => ClubAccessControllerTests.OrganizationId;
		public Guid ClubId => CurrentClubId;
		public bool IsAvailable => true;
	}

	private sealed class StubClubService : ISportsClubService
	{
		public IReadOnlyList<SportsClub> Clubs { get; init; } = [];
		public Task<IReadOnlyList<SportsClub>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Clubs);
		public Task<SportsClub?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Clubs.FirstOrDefault(club => club.Id == id));
		public Task<SportsClub> CreateAsync(SportsClub club, CancellationToken cancellationToken = default) => Task.FromResult(club);
		public Task<SportsClub?> UpdateAsync(Guid id, SportsClub club, CancellationToken cancellationToken = default) => Task.FromResult<SportsClub?>(club);
		public Task<SportsClub?> SetLogoFileAsync(Guid id, Guid? logoFileId, CancellationToken cancellationToken = default) => Task.FromResult<SportsClub?>(null);
		public Task<SportsClub?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) => Task.FromResult<SportsClub?>(null);
	}

	private sealed class StubUserService(AppUser user) : IUserService
	{
		public bool AllowSwitch { get; init; } = true;
		public Guid? SelectedClubId { get; private set; }
		public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<AppUser?>(user.Id == id ? user : null);
		public Task<AppUser?> SetDefaultClubAsync(Guid id, Guid clubId, CancellationToken cancellationToken = default)
		{
			SelectedClubId = clubId;
			if (AllowSwitch) user.DefaultClubId = clubId;
			return Task.FromResult<AppUser?>(AllowSwitch ? user : null);
		}
		public Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AppUser>>([user]);
		public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult<AppUser?>(user);
		public Task<AppUser> CreateAsync(AppUser value, string password, CancellationToken cancellationToken = default) => Task.FromResult(value);
		public Task<AppUser?> UpdateAsync(AppUser value, CancellationToken cancellationToken = default) => Task.FromResult<AppUser?>(value);
		public Task<AppUser?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) => Task.FromResult<AppUser?>(user);
		public Task<AppUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default) => Task.FromResult<AppUser?>(user);
		public Task<bool> ChangePasswordAsync(Guid id, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => Task.FromResult(true);
		public Task<bool> ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default) => Task.FromResult(true);
		public Task<AppUser> EnsureDefaultAdminUserAsync(string email, string password, CancellationToken cancellationToken = default) => Task.FromResult(user);
	}

	private sealed class StubTokenService : IJwtTokenService
	{
		public LoginResponse CreateLoginResponse(AppUser user) => new()
		{
			Token = "switched-token",
			User = UserViewModel.FromUser(user)
		};
	}
}
