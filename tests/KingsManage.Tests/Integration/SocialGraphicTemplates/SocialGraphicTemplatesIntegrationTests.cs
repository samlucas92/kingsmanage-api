using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KingsManage.Tests.Integration.Auth;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.SocialGraphicTemplates;

[TestFixture]
public sealed class SocialGraphicTemplatesIntegrationTests
{
	private const string TemplateUrl = "/api/social-graphic-templates/upcoming-editorial-gold";
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
	public async Task Get_WithoutToken_ReturnsUnauthorized()
	{
		var response = await factory.CreateClient().GetAsync(TemplateUrl);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task Get_AsPlayer_ReturnsForbidden()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync(TemplateUrl);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task SaveRestoreAndReset_AsCoach_PreservesRevisionHistory()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var emptyResponse = await client.GetAsync(TemplateUrl);
		Assert.That(emptyResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		using (var emptyDocument = JsonDocument.Parse(await emptyResponse.Content.ReadAsStringAsync()))
		{
			Assert.That(emptyDocument.RootElement.GetProperty("customization").ValueKind, Is.EqualTo(JsonValueKind.Null));
		}

		const string firstDefinition = "{\"version\":1,\"theme\":{\"accent\":\"#d7a600\"}}";
		var firstSave = await client.PutAsJsonAsync(TemplateUrl, new
		{
			SchemaVersion = 1,
			DefinitionJson = firstDefinition,
			ExpectedRevision = 0
		});
		Assert.That(firstSave.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		using (var firstDocument = JsonDocument.Parse(await firstSave.Content.ReadAsStringAsync()))
		{
			Assert.That(firstDocument.RootElement.GetProperty("revision").GetInt32(), Is.EqualTo(1));
		}

		const string secondDefinition = "{\"version\":1,\"theme\":{\"accent\":\"#ffffff\"}}";
		var secondSave = await client.PutAsJsonAsync(TemplateUrl, new
		{
			SchemaVersion = 1,
			DefinitionJson = secondDefinition,
			ExpectedRevision = 1
		});
		Assert.That(secondSave.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		var staleSave = await client.PutAsJsonAsync(TemplateUrl, new
		{
			SchemaVersion = 1,
			DefinitionJson = firstDefinition,
			ExpectedRevision = 1
		});
		Assert.That(staleSave.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

		var restore = await client.PostAsJsonAsync(
			$"{TemplateUrl}/revisions/1/restore",
			new { ExpectedRevision = 2 }
		);
		Assert.That(restore.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		using (var restoreDocument = JsonDocument.Parse(await restore.Content.ReadAsStringAsync()))
		{
			Assert.Multiple(() =>
			{
				Assert.That(restoreDocument.RootElement.GetProperty("revision").GetInt32(), Is.EqualTo(3));
				Assert.That(restoreDocument.RootElement.GetProperty("definitionJson").GetString(), Is.EqualTo(firstDefinition));
			});
		}

		var revisions = await client.GetAsync($"{TemplateUrl}/revisions");
		Assert.That(revisions.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		using (var revisionsDocument = JsonDocument.Parse(await revisions.Content.ReadAsStringAsync()))
		{
			Assert.That(
				revisionsDocument.RootElement.EnumerateArray()
					.Select(item => item.GetProperty("revision").GetInt32()),
				Is.EqualTo(new[] { 3, 2, 1 })
			);
		}

		var reset = await client.DeleteAsync($"{TemplateUrl}?expectedRevision=3");
		Assert.That(reset.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
		var afterReset = await client.GetAsync(TemplateUrl);
		using var afterResetDocument = JsonDocument.Parse(await afterReset.Content.ReadAsStringAsync());
		Assert.That(afterResetDocument.RootElement.GetProperty("customization").ValueKind, Is.EqualTo(JsonValueKind.Null));
	}

	[Test]
	public async Task Save_WithInvalidDefinition_ReturnsBadRequest()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PutAsJsonAsync(TemplateUrl, new
		{
			SchemaVersion = 1,
			DefinitionJson = "{not-json}",
			ExpectedRevision = 0
		});

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}
}
