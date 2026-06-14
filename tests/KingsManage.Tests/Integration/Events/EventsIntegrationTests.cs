using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KingsManage.Tests.Integration.Auth;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.Events;

[TestFixture]
public sealed class EventsIntegrationTests
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
	public async Task GetEvents_WhenNoTokenIsSent_ShouldReturnUnauthorized()
	{
		var client = _factory.CreateClient();

		var response = await client.GetAsync("/api/events");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task CreateEvent_WhenAdminTokenIsSent_ShouldCreateEvent()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Training",
				Title = "First team training",
				Description = "Bring boots and water.",
				StartDateTime = DateTime.UtcNow.AddDays(3),
				EndDateTime = DateTime.UtcNow.AddDays(3).AddHours(1),
				Location = "Garden Village Recreation Ground",
				Team = "First"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);

		Assert.That(document.RootElement.GetProperty("title").GetString(), Is.EqualTo("First team training"));
		Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo("Training"));
	}

	[Test]
	public async Task CreateEvent_WhenCoachTokenIsSent_ShouldCreateEvent()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Social",
				Title = "Player social",
				Description = "Meet at the club.",
				StartDateTime = DateTime.UtcNow.AddDays(7),
				Location = "Clubhouse"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
	}

	[Test]
	public async Task CreateEvent_WhenPlayerTokenIsSent_ShouldReturnForbidden()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Social",
				Title = "Player social",
				Description = "Meet at the club.",
				StartDateTime = DateTime.UtcNow.AddDays(7),
				Location = "Clubhouse"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task GetEvents_WhenPlayerTokenIsSent_ShouldHideMeetingEvents()
	{
		var meetingEventId = Guid.Parse("30000000-0000-0000-0000-000000000001");

		_factory.ClubEventService.Events.AddRange(
			[
				new()
				{
					Id = meetingEventId,
					Type = ClubEventType.Meeting,
					Title = "Coaches meeting",
					StartDateTime = DateTime.UtcNow.AddDays(1),
					Location = "Clubhouse"
				},
				new()
				{
					Id = Guid.Parse("30000000-0000-0000-0000-000000000002"),
					Type = ClubEventType.Training,
					Title = "Training",
					StartDateTime = DateTime.UtcNow.AddDays(2),
					Location = "Garden Village Recreation Ground"
				}
			]
		);

		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.GetAsync("/api/events");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		var json = await response.Content.ReadAsStringAsync();

		Assert.That(json, Does.Contain("Training"));
		Assert.That(json, Does.Not.Contain("Coaches meeting"));

		var getMeetingResponse = await client.GetAsync($"/api/events/{meetingEventId}");

		Assert.That(getMeetingResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
	}

	[Test]
	public async Task GetEvents_WhenCoachTokenIsSent_ShouldIncludeMeetingEvents()
	{
		_factory.ClubEventService.Events.Add(
			new ClubEvent
			{
				Id = Guid.Parse("30000000-0000-0000-0000-000000000003"),
				Type = ClubEventType.Meeting,
				Title = "Coaches meeting",
				StartDateTime = DateTime.UtcNow.AddDays(1),
				Location = "Clubhouse"
			}
		);

		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.GetAsync("/api/events");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		var json = await response.Content.ReadAsStringAsync();

		Assert.That(json, Does.Contain("Coaches meeting"));
	}

	[Test]
	public async Task CreateMatchEvent_WhenNoMatchOptionIsChosen_ShouldCreateEventOnly()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				Title = "Placeholder fixture",
				Description = "Match event without a linked match yet.",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "TBC",
				Team = "First"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
		Assert.That(_factory.MatchService.Matches, Is.Empty);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);

		Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo("Match"));
		Assert.That(document.RootElement.GetProperty("matchId").ValueKind, Is.EqualTo(JsonValueKind.Null));
	}

	[Test]
	public async Task CreateMatchEvent_WhenLinkedMatchExists_ShouldCreateLinkedEvent()
	{
		var matchId = Guid.Parse("40000000-0000-0000-0000-000000000001");

		_factory.MatchService.Matches.Add(
			new Match
			{
				Id = matchId,
				SeasonId = Guid.Parse("50000000-0000-0000-0000-000000000001"),
				Team = ClubTeam.First,
				Opponent = "Gors AFC",
				Date = DateTime.UtcNow.AddDays(10),
				Venue = MatchVenue.Home
			}
		);

		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				Title = "Kingsbridge Colts vs Gors AFC",
				Description = "League fixture.",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Home pitch",
				MatchId = matchId,
				Team = "First"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);

		Assert.That(document.RootElement.GetProperty("matchId").GetGuid(), Is.EqualTo(matchId));
		Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo("Match"));
	}

	[Test]
	public async Task CreateMatchEvent_WhenLinkedMatchDoesNotExist_ShouldReturnBadRequest()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				Title = "Missing match event",
				Description = "Should fail.",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Home pitch",
				MatchId = Guid.Parse("40000000-0000-0000-0000-000000000099")
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}

	[Test]
	public async Task CreateMatchEvent_WhenCreateLinkedMatchIsTrue_ShouldCreateMatchAndLinkedEvent()
	{
		var seasonId = Guid.Parse("50000000-0000-0000-0000-000000000002");
		var eventStartDate = DateTime.UtcNow.AddDays(14);

		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				Title = "Kingsbridge Colts vs Loughor",
				Description = "League fixture.",
				StartDateTime = eventStartDate,
				Location = "Garden Village Recreation Ground",
				CreateLinkedMatch = true,
				CreateMatch = new
				{
					SeasonId = seasonId,
					Team = "First",
					Opponent = "Loughor",
					Venue = "Home",
					SelectedFormation = "FourThreeThree"
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

		Assert.That(_factory.MatchService.Matches, Has.Count.EqualTo(1));

		var createdMatch = _factory.MatchService.Matches.Single();

		Assert.That(createdMatch.SeasonId, Is.EqualTo(seasonId));
		Assert.That(createdMatch.Team, Is.EqualTo(ClubTeam.First));
		Assert.That(createdMatch.Opponent, Is.EqualTo("Loughor"));
		Assert.That(createdMatch.Date, Is.EqualTo(eventStartDate).Within(TimeSpan.FromSeconds(1)));
		Assert.That(createdMatch.Venue, Is.EqualTo(MatchVenue.Home));

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);

		Assert.That(document.RootElement.GetProperty("matchId").GetGuid(), Is.EqualTo(createdMatch.Id));
		Assert.That(document.RootElement.GetProperty("seasonId").GetGuid(), Is.EqualTo(seasonId));
		Assert.That(document.RootElement.GetProperty("team").GetString(), Is.EqualTo("First"));
	}

	[Test]
	public async Task CreateMatchEvent_WhenCreateMatchIsProvidedButCreateLinkedMatchIsFalse_ShouldReturnBadRequest()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				Title = "Should not silently create match",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Home pitch",
				CreateMatch = new
				{
					Team = "First",
					Opponent = "Loughor",
					Venue = "Home"
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That(_factory.MatchService.Matches, Is.Empty);
	}

	[Test]
	public async Task CreateMatchEvent_WhenMatchIdAndCreateLinkedMatchAreProvided_ShouldReturnBadRequest()
	{
		var matchId = Guid.Parse("40000000-0000-0000-0000-000000000003");

		_factory.MatchService.Matches.Add(
			new Match
			{
				Id = matchId,
				Team = ClubTeam.First,
				Opponent = "Gors AFC",
				Date = DateTime.UtcNow.AddDays(10),
				Venue = MatchVenue.Home
			}
		);

		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				Title = "Invalid match event",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Home pitch",
				MatchId = matchId,
				CreateLinkedMatch = true,
				CreateMatch = new
				{
					Team = "First",
					Opponent = "Loughor",
					Venue = "Home"
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}

	[Test]
	public async Task CreateNonMatchEvent_WhenCreateLinkedMatchIsProvided_ShouldReturnBadRequest()
	{
		var client = await _factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Training",
				Title = "Invalid training event",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Training pitch",
				CreateLinkedMatch = true,
				CreateMatch = new
				{
					Team = "First",
					Opponent = "Loughor",
					Venue = "Home"
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}
}
