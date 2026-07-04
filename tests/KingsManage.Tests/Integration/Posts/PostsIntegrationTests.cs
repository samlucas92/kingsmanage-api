using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KingsManage;
using KingsManage.Tests.Integration.Auth;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.Posts;

[TestFixture]
public sealed class PostsIntegrationTests
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
	public async Task GetPosts_WithoutToken_ReturnsUnauthorized()
	{
		var client = factory.CreateClient();

		var response = await client.GetAsync("/api/posts");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task GetPosts_AsPlayer_ReturnsPosts()
	{
		factory.ClubPostService.Posts.Add(
			new ClubPost
			{
				Id = Guid.Parse("80000000-0000-0000-0000-000000000001"),
				Type = ClubPostType.Announcement,
				Title = "Training update",
				Body = "Training is on as normal.",
				CreatedByUserId = TestUsers.AdminId,
				CreatedByUserEmail = TestUsers.AdminEmail,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync("/api/posts");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Training update"));
	}

	[Test]
	public async Task CreatePost_AsCoach_CreatesPost()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/posts",
			new
			{
				Type = "Announcement",
				Title = "Squad update",
				Body = "Details will follow after training.",
				IsPinned = true
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.That(document.RootElement.GetProperty("title").GetString(), Is.EqualTo("Squad update"));
		Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo("Announcement"));
		Assert.That(document.RootElement.GetProperty("isPinned").GetBoolean(), Is.True);
		Assert.That(document.RootElement.GetProperty("createdByUserEmail").GetString(), Is.EqualTo(TestUsers.CoachEmail));
	}

	[Test]
	public async Task CreatePost_AsPlayer_ReturnsForbidden()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/posts",
			new
			{
				Type = "General",
				Title = "Player post",
				Body = "Players cannot create posts.",
				IsPinned = false
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task UpdatePost_AsAdmin_UpdatesPost()
	{
		var postId = Guid.Parse("80000000-0000-0000-0000-000000000002");

		factory.ClubPostService.Posts.Add(
			new ClubPost
			{
				Id = postId,
				Type = ClubPostType.General,
				Title = "Old title",
				Body = "Old body",
				CreatedByUserId = TestUsers.CoachId,
				CreatedByUserEmail = TestUsers.CoachEmail,
				CreatedAt = DateTime.UtcNow.AddDays(-1),
				UpdatedAt = DateTime.UtcNow.AddDays(-1)
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PutAsJsonAsync(
			$"/api/posts/{postId}",
			new
			{
				Type = "MatchInfo",
				Title = "Updated title",
				Body = "Updated body",
				IsPinned = false
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.That(document.RootElement.GetProperty("title").GetString(), Is.EqualTo("Updated title"));
		Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo("MatchInfo"));
		Assert.That(document.RootElement.GetProperty("createdByUserEmail").GetString(), Is.EqualTo(TestUsers.CoachEmail));
	}

	[Test]
	public async Task DeletePost_AsAdmin_RemovesPost()
	{
		var postId = Guid.Parse("80000000-0000-0000-0000-000000000003");

		factory.ClubPostService.Posts.Add(
			new ClubPost
			{
				Id = postId,
				Type = ClubPostType.Social,
				Title = "Social post",
				Body = "Social details.",
				CreatedByUserId = TestUsers.AdminId,
				CreatedByUserEmail = TestUsers.AdminEmail,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.DeleteAsync($"/api/posts/{postId}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
		Assert.That(factory.ClubPostService.Posts, Is.Empty);
	}
}
