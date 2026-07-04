using System.Net;
using KingsManage;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.Auth;

[TestFixture]
public sealed class AuthAuthorizationIntegrationTests
{
	private AuthIntegrationTestFactory factory = null!;

	[SetUp]
	public void SetUp()
	{
		factory = new AuthIntegrationTestFactory();
		factory.SeedDefaultUsers();
	}

	[TearDown]
	public void TearDown()
	{
		factory.Dispose();
	}

	[Test]
	public async Task UsersEndpoint_WhenNoTokenIsSent_ShouldReturnUnauthorized()
	{
		var client = factory.CreateClient();

		var response = await client.GetAsync("/api/users");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task UsersEndpoint_WhenAdminTokenIsSent_ShouldReturnOk()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.GetAsync("/api/users");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task UsersEndpoint_WhenCoachTokenIsSent_ShouldReturnForbidden()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.GetAsync("/api/users");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task UsersEndpoint_WhenPlayerTokenIsSent_ShouldReturnForbidden()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync("/api/users");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task StatsEndpoint_WhenNoTokenIsSent_ShouldReturnUnauthorized()
	{
		var client = factory.CreateClient();

		var response = await client.GetAsync($"/api/stats/season/{Guid.NewGuid()}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task StatsEndpoint_WhenAdminTokenIsSent_ShouldReturnOk()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.GetAsync($"/api/stats/season/{Guid.NewGuid()}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task StatsEndpoint_WhenCoachTokenIsSent_ShouldReturnOk()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.GetAsync($"/api/stats/season/{Guid.NewGuid()}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task StatsEndpoint_WhenPlayerTokenIsSent_ShouldReturnForbidden()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync($"/api/stats/season/{Guid.NewGuid()}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task PlatformOrganizations_WhenOrganizationAdminIsSent_ShouldReturnForbidden()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.GetAsync("/api/platform/organizations");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task PlatformOrganizations_WhenSiteAdminIsSent_ShouldReturnOk()
	{
		const string email = "site-admin@test.local";
		const string password = "SiteAdmin123!";
		AddTenantUser(email, password, TenantRole.OrganizationAdmin, isPlatformAdmin: true);
		var client = await factory.CreateAuthenticatedClientAsync(email, password);

		var response = await client.GetAsync("/api/platform/organizations");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task ClubAdmin_CanReadOrganizationButCannotManageOrganizationUsers()
	{
		const string email = "club-admin@test.local";
		const string password = "ClubAdmin123!";
		AddTenantUser(email, password, TenantRole.ClubAdmin);
		var client = await factory.CreateAuthenticatedClientAsync(email, password);

		var organizationResponse = await client.GetAsync("/api/organization");
		var usersResponse = await client.GetAsync("/api/users");

		Assert.Multiple(() =>
		{
			Assert.That(organizationResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(usersResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
		});
	}

	[Test]
	public async Task TeamManager_CanReadTeamStatsButCannotReadClubFinances()
	{
		const string email = "team-manager@test.local";
		const string password = "TeamManager123!";
		AddTenantUser(
			email,
			password,
			TenantRole.TeamManager,
			DefaultClubTeams.FirstTeamId
		);
		var client = await factory.CreateAuthenticatedClientAsync(email, password);

		var statsResponse = await client.GetAsync($"/api/stats/season/{Guid.NewGuid()}");
		var financeResponse = await client.GetAsync("/api/finance");

		Assert.Multiple(() =>
		{
			Assert.That(statsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(financeResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
		});
	}

	[Test]
	public async Task Player_CanReadPlayersAndEventsButCannotReadTeamManagementStats()
	{
		const string email = "scoped-player@test.local";
		const string password = "ScopedPlayer123!";
		AddTenantUser(
			email,
			password,
			TenantRole.Player,
			DefaultClubTeams.FirstTeamId
		);
		var client = await factory.CreateAuthenticatedClientAsync(email, password);

		var playersResponse = await client.GetAsync("/api/players");
		var eventsResponse = await client.GetAsync("/api/events");
		var statsResponse = await client.GetAsync($"/api/stats/season/{Guid.NewGuid()}");

		Assert.Multiple(() =>
		{
			Assert.That(playersResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(eventsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(statsResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
		});
	}

	private void AddTenantUser(
		string email,
		string password,
		TenantRole role,
		Guid? teamId = null,
		bool isPlatformAdmin = false)
	{
		factory.UserService.AddUser(
			new AppUser
			{
				Id = Guid.NewGuid(),
				Email = email,
				Role = role is TenantRole.TeamManager or TenantRole.Coach
					? UserRole.Coach
					: role == TenantRole.Player ? UserRole.Player : UserRole.Admin,
				IsPlatformAdmin = isPlatformAdmin,
				DefaultOrganizationId = DefaultTenant.OrganizationId,
				DefaultClubId = DefaultTenant.ClubId,
				Memberships =
				[
					new UserMembership
					{
						OrganizationId = DefaultTenant.OrganizationId,
						ClubId = DefaultTenant.ClubId,
						TeamId = teamId,
						Role = role
					}
				],
				IsActive = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			},
			password
		);
	}
}
