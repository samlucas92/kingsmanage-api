using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public sealed class ClubSetupControllerTests
{
	[Test]
	public async Task CreateStaff_CreatesAClubScopedCoach()
	{
		var users = new StubUserService();
		var teamId = Guid.NewGuid();
		var controller = new ClubSetupController(
			users,
			new StubTeamService(teamId),
			new StubTenantContext());

		var result = await controller.CreateStaff(
			new CreateSetupStaffRequest
			{
				Email = "coach@example.com",
				Password = "Temporary123!",
				Role = TenantRole.Coach,
				TeamId = teamId
			},
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<CreatedResult>());
		Assert.Multiple(() =>
		{
			Assert.That(users.Created?.Memberships.Single().ClubId, Is.EqualTo(DefaultTenant.ClubId));
			Assert.That(users.Created?.Memberships.Single().TeamId, Is.EqualTo(teamId));
			Assert.That(users.Created?.Memberships.Single().Role, Is.EqualTo(TenantRole.Coach));
		});
	}

	[Test]
	public async Task CreateStaff_RejectsTeamManagerWithoutATeam()
	{
		var controller = new ClubSetupController(
			new StubUserService(),
			new StubTeamService(Guid.NewGuid()),
			new StubTenantContext());

		var result = await controller.CreateStaff(
			new CreateSetupStaffRequest
			{
				Email = "manager@example.com",
				Password = "Temporary123!",
				Role = TenantRole.TeamManager
			},
			CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	private sealed class StubTenantContext : ITenantContext
	{
		public bool IsAvailable => true;
		public Guid OrganizationId => DefaultTenant.OrganizationId;
		public Guid ClubId => DefaultTenant.ClubId;
	}

	private sealed class StubTeamService(Guid teamId) : IClubTeamService
	{
		public Task<IReadOnlyList<ClubTeamProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<ClubTeamProfile>>([]);
		public Task<ClubTeamProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult<ClubTeamProfile?>(id == teamId ? new ClubTeamProfile { Id = id } : null);
		public Task<ClubTeamProfile> CreateAsync(ClubTeamProfile profile, CancellationToken cancellationToken = default) =>
			Task.FromResult(profile);
		public Task<ClubTeamProfile> UpdateAsync(Guid id, ClubTeamProfile profile, CancellationToken cancellationToken = default) =>
			Task.FromResult(profile);
		public Task<ClubTeamDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult(ClubTeamDeleteResult.Deleted);
	}

	private sealed class StubUserService : IUserService
	{
		public AppUser? Created { get; private set; }
		public Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<AppUser>>([]);
		public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult<AppUser?>(null);
		public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
			Task.FromResult<AppUser?>(null);
		public Task<AppUser> CreateAsync(AppUser user, string password, CancellationToken cancellationToken = default)
		{
			Created = user;
			return Task.FromResult(user);
		}
		public Task<AppUser?> UpdateAsync(AppUser user, CancellationToken cancellationToken = default) =>
			Task.FromResult<AppUser?>(user);
		public Task<AppUser?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) =>
			Task.FromResult<AppUser?>(null);
		public Task<AppUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default) =>
			Task.FromResult<AppUser?>(null);
		public Task<bool> ChangePasswordAsync(Guid id, string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
			Task.FromResult(true);
		public Task<bool> ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default) =>
			Task.FromResult(true);
		public Task<AppUser> EnsureDefaultAdminUserAsync(string email, string password, CancellationToken cancellationToken = default) =>
			Task.FromResult(new AppUser());
	}
}
