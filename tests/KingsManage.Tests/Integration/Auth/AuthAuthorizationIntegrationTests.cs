using System.Net;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.Auth;

[TestFixture]
public sealed class AuthAuthorizationIntegrationTests
{
	private AuthIntegrationTestFactory _factory = null!;

	[SetUp]
	public void SetUp()
	{
		_factory = new AuthIntegrationTestFactory();
		_factory.SeedDefaultUsers();
	}

	[TearDown]
	public void TearDown()
	{
		_factory.Dispose();
	}

	[Test]
	public async Task UsersEndpoint_WhenNoTokenIsSent_ShouldReturnUnauthorized()
	{
		var client = _factory.CreateClient();

		var response = await client.GetAsync("/api/users");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task UsersEndpoint_WhenAdminTokenIsSent_ShouldReturnOk()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.GetAsync("/api/users");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task UsersEndpoint_WhenCoachTokenIsSent_ShouldReturnForbidden()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.GetAsync("/api/users");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task UsersEndpoint_WhenPlayerTokenIsSent_ShouldReturnForbidden()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync("/api/users");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task StatsEndpoint_WhenNoTokenIsSent_ShouldReturnUnauthorized()
	{
		var client = _factory.CreateClient();

		var response = await client.GetAsync($"/api/stats/season/{Guid.NewGuid()}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task StatsEndpoint_WhenAdminTokenIsSent_ShouldReturnOk()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.GetAsync($"/api/stats/season/{Guid.NewGuid()}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task StatsEndpoint_WhenCoachTokenIsSent_ShouldReturnOk()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.GetAsync($"/api/stats/season/{Guid.NewGuid()}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task StatsEndpoint_WhenPlayerTokenIsSent_ShouldReturnForbidden()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync($"/api/stats/season/{Guid.NewGuid()}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}
}
