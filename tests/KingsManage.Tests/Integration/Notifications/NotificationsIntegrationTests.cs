using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KingsManage;
using KingsManage.Tests.Integration.Auth;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.Notifications;

[TestFixture]
public sealed class NotificationsIntegrationTests
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
	public async Task CreatePost_AsAdmin_CreatesNotificationsForOtherActiveUsers()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/posts",
			new
			{
				Type = "General",
				Title = "Training update",
				Body = "Training starts at 18:30.",
				IsPinned = false
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
		Assert.That(factory.ClubNotificationService.Notifications, Has.Count.EqualTo(1));

		var notification = factory.ClubNotificationService.Notifications.Single();
		var recipientIds = notification.Recipients.Select(recipient => recipient.UserId).ToList();

		Assert.That(notification.Type, Is.EqualTo(NotificationType.NewPost));
		Assert.That(notification.SourceType, Is.EqualTo(NotificationSourceType.Post));
		Assert.That(notification.Title, Is.EqualTo("New post: Training update"));
		Assert.That(notification.ActionPath, Does.StartWith("/posts/"));
		Assert.That(recipientIds, Does.Contain(TestUsers.CoachId));
		Assert.That(recipientIds, Does.Contain(TestUsers.PlayerId));
		Assert.That(recipientIds, Does.Not.Contain(TestUsers.AdminId));
		Assert.That(recipientIds, Does.Not.Contain(TestUsers.InactiveId));
	}

	[Test]
	public async Task GetMine_AsPlayer_ReturnsOnlyPlayerNotifications()
	{
		SeedNotificationForUsers(TestUsers.PlayerId, TestUsers.CoachId);
		SeedNotificationForUsers(TestUsers.CoachId);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync("/api/notifications/mine");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var root = document.RootElement;

		Assert.That(root.GetArrayLength(), Is.EqualTo(1));
		Assert.That(root[0].GetProperty("title").GetString(), Is.EqualTo("Test notification"));
		Assert.That(root[0].GetProperty("status").GetString(), Is.EqualTo("Unread"));
		Assert.That(root[0].GetProperty("isRead").GetBoolean(), Is.False);
	}

	[Test]
	public async Task GetUnreadCount_AsPlayer_ReturnsUnreadNotificationCount()
	{
		SeedNotificationForUsers(TestUsers.PlayerId);
		var readNotification = SeedNotificationForUsers(TestUsers.PlayerId);
		readNotification.Recipients.Single().Status = NotificationStatus.Read;
		readNotification.Recipients.Single().ReadAt = DateTime.UtcNow;

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync("/api/notifications/unread-count");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.That(document.RootElement.GetProperty("unreadCount").GetInt32(), Is.EqualTo(1));
	}

	[Test]
	public async Task MarkRead_AsPlayer_MarksOwnNotificationRead()
	{
		var notification = SeedNotificationForUsers(TestUsers.PlayerId);
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.PostAsync(
			$"/api/notifications/{notification.Id}/mark-read",
			null
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		Assert.That(notification.Recipients.Single().Status, Is.EqualTo(NotificationStatus.Read));
		Assert.That(notification.Recipients.Single().ReadAt, Is.Not.Null);

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("Read"));
		Assert.That(document.RootElement.GetProperty("isRead").GetBoolean(), Is.True);
	}

	[Test]
	public async Task MarkAllRead_AsPlayer_MarksAllOwnNotificationsRead()
	{
		SeedNotificationForUsers(TestUsers.PlayerId);
		SeedNotificationForUsers(TestUsers.PlayerId, TestUsers.CoachId);
		SeedNotificationForUsers(TestUsers.CoachId);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.PostAsync("/api/notifications/mark-all-read", null);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.That(document.RootElement.GetProperty("updatedCount").GetInt32(), Is.EqualTo(2));
		Assert.That(
			factory.ClubNotificationService.Notifications.Count(notification =>
				notification.Recipients.Any(recipient =>
					recipient.UserId == TestUsers.PlayerId &&
					recipient.Status == NotificationStatus.Unread)),
			Is.EqualTo(0)
		);
	}

	[Test]
	public async Task CreateMeetingEvent_AsAdmin_DoesNotNotifyPlayers()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Meeting",
				TeamScope = "Both",
				Title = "Committee meeting",
				Description = "Committee only.",
				StartDateTime = DateTime.UtcNow.AddDays(7),
				EndDateTime = DateTime.UtcNow.AddDays(7).AddHours(1),
				Location = "Clubhouse",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = false,
				CreateMatches = Array.Empty<object>()
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
		Assert.That(factory.ClubNotificationService.Notifications, Has.Count.EqualTo(1));

		var recipientIds = factory.ClubNotificationService.Notifications
			.Single()
			.Recipients
			.Select(recipient => recipient.UserId)
			.ToList();

		Assert.That(recipientIds, Does.Contain(TestUsers.CoachId));
		Assert.That(recipientIds, Does.Not.Contain(TestUsers.PlayerId));
		Assert.That(recipientIds, Does.Not.Contain(TestUsers.AdminId));
	}

	private ClubNotification SeedNotificationForUsers(params Guid[] userIds)
	{
		var notification = new ClubNotification
		{
			Id = Guid.NewGuid(),
			Type = NotificationType.NewPost,
			SourceType = NotificationSourceType.Post,
			SourceId = Guid.NewGuid(),
			Title = "Test notification",
			Message = "This is a test notification.",
			ActionPath = "/posts/test",
			CreatedAt = DateTime.UtcNow,
			Recipients = userIds
				.Select(userId => new ClubNotificationRecipient { UserId = userId })
				.ToList()
		};

		factory.ClubNotificationService.Notifications.Add(notification);

		return notification;
	}
}
