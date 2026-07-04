using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KingsManage;
using KingsManage.Tests.Integration.Auth;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.Messages;

[TestFixture]
public sealed class MessagesIntegrationTests
{
	private AuthIntegrationTestFactory factory = null!;

	[SetUp]
	public void SetUp()
	{
		factory = new AuthIntegrationTestFactory();
		factory.SeedDefaultUsers();
	}

	[TearDown]
	public void TearDown() => factory.Dispose();

	[Test]
	public async Task CreateDirectThread_Twice_ReusesExistingThread()
	{
		var client = await CreatePlayerClientAsync();
		var first = await client.PostAsJsonAsync("/api/messages/threads/direct", new { UserId = TestUsers.CoachId });
		var second = await client.PostAsJsonAsync("/api/messages/threads/direct", new { UserId = TestUsers.CoachId });

		Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		Assert.That(factory.MessageService.Threads, Has.Count.EqualTo(1));

		using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
		using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
		Assert.That(secondJson.RootElement.GetProperty("id").GetGuid(), Is.EqualTo(firstJson.RootElement.GetProperty("id").GetGuid()));
	}

	[Test]
	public async Task CreateDirectThread_ForInactiveUser_ReturnsBadRequest()
	{
		var client = await CreatePlayerClientAsync();
		var response = await client.PostAsJsonAsync("/api/messages/threads/direct", new { UserId = TestUsers.InactiveId });
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}

	[Test]
	public async Task GetThread_AsNonParticipant_ReturnsNotFound()
	{
		var thread = await factory.MessageService.GetOrCreateDirectThreadAsync(TestUsers.AdminId, TestUsers.CoachId);
		var client = await CreatePlayerClientAsync();
		var response = await client.GetAsync($"/api/messages/threads/{thread.Id}");
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
	}

	[Test]
	public async Task SendMessage_AsNonParticipant_ReturnsNotFound()
	{
		var thread = await factory.MessageService.GetOrCreateDirectThreadAsync(TestUsers.AdminId, TestUsers.CoachId);
		var client = await CreatePlayerClientAsync();
		var response = await client.PostAsJsonAsync($"/api/messages/threads/{thread.Id}/messages", new { Body = "Hello" });
		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
		Assert.That(factory.MessageService.Messages, Is.Empty);
	}

	[Test]
	public async Task SendMessage_NotifiesRecipientButNotSender()
	{
		var thread = await factory.MessageService.GetOrCreateDirectThreadAsync(TestUsers.PlayerId, TestUsers.CoachId);
		var client = await CreatePlayerClientAsync();
		var response = await client.PostAsJsonAsync($"/api/messages/threads/{thread.Id}/messages", new { Body = "Are you free for training?" });

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
		var notification = factory.ClubNotificationService.Notifications.Single();
		Assert.That(notification.Type, Is.EqualTo(NotificationType.NewDirectMessage));
		Assert.That(notification.SourceType, Is.EqualTo(NotificationSourceType.Message));
		Assert.That(notification.Recipients.Select(recipient => recipient.UserId), Is.EqualTo(new[] { TestUsers.CoachId }));
		Assert.That(notification.ActionPath, Does.Contain(thread.Id.ToString()));
	}

	[Test]
	public async Task MarkRead_UpdatesCurrentParticipantReadState()
	{
		var thread = await factory.MessageService.GetOrCreateDirectThreadAsync(TestUsers.PlayerId, TestUsers.CoachId);
		thread.Participants.First(participant => participant.UserId == TestUsers.PlayerId).LastReadAt = null;
		var client = await CreatePlayerClientAsync();
		var response = await client.PostAsync($"/api/messages/threads/{thread.Id}/mark-read", null);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		Assert.That(thread.Participants.First(participant => participant.UserId == TestUsers.PlayerId).LastReadAt, Is.Not.Null);
	}

	private Task<HttpClient> CreatePlayerClientAsync() => factory.CreateAuthenticatedClientAsync(TestUsers.PlayerEmail, TestUsers.PlayerPassword);
}
