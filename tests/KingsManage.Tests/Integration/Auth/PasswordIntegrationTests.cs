using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.Auth;

[TestFixture]
public sealed class PasswordIntegrationTests
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
	public async Task ChangePassword_WhenNoTokenIsSent_ShouldReturnUnauthorized()
	{
		var client = _factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			"/api/auth/change-password",
			new
			{
				CurrentPassword = TestUsers.AdminPassword,
				NewPassword = "NewAdmin123!"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task ChangePassword_WhenCurrentPasswordIsCorrect_ShouldUpdatePassword()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/auth/change-password",
			new
			{
				CurrentPassword = TestUsers.AdminPassword,
				NewPassword = "NewAdmin123!"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

		var oldPasswordLoginResponse = await _factory.CreateClient().PostAsJsonAsync(
			"/api/auth/login",
			new
			{
				Email = TestUsers.AdminEmail,
				Password = TestUsers.AdminPassword
			}
		);

		Assert.That(oldPasswordLoginResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

		var newPasswordLoginResponse = await _factory.CreateClient().PostAsJsonAsync(
			"/api/auth/login",
			new
			{
				Email = TestUsers.AdminEmail,
				Password = "NewAdmin123!"
			}
		);

		Assert.That(newPasswordLoginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task ChangePassword_WhenCurrentPasswordIsWrong_ShouldReturnBadRequestAndKeepOldPassword()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/auth/change-password",
			new
			{
				CurrentPassword = "WrongPassword123!",
				NewPassword = "NewAdmin123!"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

		var oldPasswordLoginResponse = await _factory.CreateClient().PostAsJsonAsync(
			"/api/auth/login",
			new
			{
				Email = TestUsers.AdminEmail,
				Password = TestUsers.AdminPassword
			}
		);

		Assert.That(oldPasswordLoginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task ChangePassword_WhenNewPasswordIsTooShort_ShouldReturnBadRequest()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/auth/change-password",
			new
			{
				CurrentPassword = TestUsers.AdminPassword,
				NewPassword = "short"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}

	[Test]
	public async Task ResetPassword_WhenNoTokenIsSent_ShouldReturnUnauthorized()
	{
		var client = _factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			$"/api/users/{TestUsers.CoachId}/reset-password",
			new
			{
				NewPassword = "ResetCoach123!"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task ResetPassword_WhenAdminTokenIsSent_ShouldUpdatePassword()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			$"/api/users/{TestUsers.CoachId}/reset-password",
			new
			{
				NewPassword = "ResetCoach123!"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

		var oldPasswordLoginResponse = await _factory.CreateClient().PostAsJsonAsync(
			"/api/auth/login",
			new
			{
				Email = TestUsers.CoachEmail,
				Password = TestUsers.CoachPassword
			}
		);

		Assert.That(oldPasswordLoginResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

		var newPasswordLoginResponse = await _factory.CreateClient().PostAsJsonAsync(
			"/api/auth/login",
			new
			{
				Email = TestUsers.CoachEmail,
				Password = "ResetCoach123!"
			}
		);

		Assert.That(newPasswordLoginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
	}

	[Test]
	public async Task ResetPassword_WhenCoachTokenIsSent_ShouldReturnForbidden()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.PostAsJsonAsync(
			$"/api/users/{TestUsers.PlayerId}/reset-password",
			new
			{
				NewPassword = "ResetPlayer123!"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task ResetPassword_WhenPlayerTokenIsSent_ShouldReturnForbidden()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.PostAsJsonAsync(
			$"/api/users/{TestUsers.CoachId}/reset-password",
			new
			{
				NewPassword = "ResetCoach123!"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task ResetPassword_WhenUserDoesNotExist_ShouldReturnNotFound()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			$"/api/users/{Guid.NewGuid()}/reset-password",
			new
			{
				NewPassword = "ResetMissing123!"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
	}

	[Test]
	public async Task ResetPassword_WhenNewPasswordIsTooShort_ShouldReturnBadRequest()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			$"/api/users/{TestUsers.CoachId}/reset-password",
			new
			{
				NewPassword = "short"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}
}
