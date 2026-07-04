using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KingsManage;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.Auth;

[TestFixture]
public sealed class AuthIntegrationTests
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
	public async Task Login_WhenAdminCredentialsAreValid_ShouldReturnOkWithToken()
	{
		var client = factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			"/api/auth/login",
			new
			{
				Email = TestUsers.AdminEmail,
				Password = TestUsers.AdminPassword
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = await ReadJsonDocumentAsync(response);
		var root = document.RootElement;
		var user = root.GetProperty("user");

		Assert.That(root.GetProperty("token").GetString(), Is.Not.Empty);
		Assert.That(user.GetProperty("id").GetGuid(), Is.EqualTo(TestUsers.AdminId));
		Assert.That(user.GetProperty("email").GetString(), Is.EqualTo(TestUsers.AdminEmail));
		Assert.That(user.GetProperty("role").GetString(), Is.EqualTo(nameof(UserRole.Admin)));
	}

	[Test]
	public async Task Login_WhenPasswordIsWrong_ShouldReturnUnauthorized()
	{
		var client = factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			"/api/auth/login",
			new
			{
				Email = TestUsers.AdminEmail,
				Password = "WrongPassword123!"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task Login_WhenEmailDoesNotExist_ShouldReturnUnauthorized()
	{
		var client = factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			"/api/auth/login",
			new
			{
				Email = "missing@test.local",
				Password = "Password123!"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task Login_WhenUserIsInactive_ShouldReturnUnauthorized()
	{
		var client = factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			"/api/auth/login",
			new
			{
				Email = TestUsers.InactiveEmail,
				Password = TestUsers.InactivePassword
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task GetCurrentUser_WhenNoTokenIsSent_ShouldReturnUnauthorized()
	{
		var client = factory.CreateClient();

		var response = await client.GetAsync("/api/auth/me");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task GetCurrentUser_WhenAdminTokenIsSent_ShouldReturnCurrentUser()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.GetAsync("/api/auth/me");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = await ReadJsonDocumentAsync(response);
		var root = document.RootElement;

		Assert.That(root.GetProperty("id").GetGuid(), Is.EqualTo(TestUsers.AdminId));
		Assert.That(root.GetProperty("role").GetString(), Is.EqualTo(nameof(UserRole.Admin)));
	}

	[Test]
	public async Task GetCurrentUser_WhenCoachTokenIsSent_ShouldReturnCurrentUser()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.GetAsync("/api/auth/me");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = await ReadJsonDocumentAsync(response);
		var root = document.RootElement;

		Assert.That(root.GetProperty("id").GetGuid(), Is.EqualTo(TestUsers.CoachId));
		Assert.That(root.GetProperty("role").GetString(), Is.EqualTo(nameof(UserRole.Coach)));
	}

	[Test]
	public async Task GetCurrentUser_WhenPlayerTokenIsSent_ShouldReturnCurrentUser()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync("/api/auth/me");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = await ReadJsonDocumentAsync(response);
		var root = document.RootElement;

		Assert.That(root.GetProperty("id").GetGuid(), Is.EqualTo(TestUsers.PlayerId));
		Assert.That(root.GetProperty("role").GetString(), Is.EqualTo(nameof(UserRole.Player)));
		Assert.That(root.GetProperty("playerId").GetGuid(), Is.EqualTo(TestUsers.LinkedPlayerId));
	}

	private static async Task<JsonDocument> ReadJsonDocumentAsync(HttpResponseMessage response)
	{
		var responseBody = await response.Content.ReadAsStringAsync();

		try
		{
			return JsonDocument.Parse(responseBody);
		}
		catch (JsonException exception)
		{
			throw new AssertionException($"Response was not valid JSON. Response: {responseBody}", exception);
		}
	}
}
