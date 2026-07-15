// EVENTS V3 HARD REPLACEMENT
// If grep still finds CreateLinkedMatch singular in this file after copying,
// this file has not replaced the compiled test file.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KingsManage;
using KingsManage.Tests.Integration.Auth;
using NUnit.Framework;

namespace KingsManage.Tests.Integration.Events;

[TestFixture]
public sealed class EventsIntegrationTests
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
	public async Task GetEvents_WithoutToken_ReturnsUnauthorized()
	{
		var client = factory.CreateClient();

		var response = await client.GetAsync("/api/events");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
	}

	[Test]
	public async Task CreateTrainingEvent_AsAdmin_CreatesEvent()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);
		var start = DateTime.UtcNow.AddDays(3);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Training",
				TeamScope = "Both",
				Title = "First team training",
				Description = "Bring boots and water.",
				StartDateTime = start,
				EndDateTime = start.AddHours(1),
				Location = "Garden Village Recreation Ground",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = false,
				CreateMatches = Array.Empty<object>()
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.That(document.RootElement.GetProperty("title").GetString(), Is.EqualTo($"Training {start:dd/MM/yyyy}"));
		Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo("Training"));
		Assert.That(document.RootElement.GetProperty("teamScope").GetString(), Is.EqualTo("Both"));
		Assert.That(document.RootElement.GetProperty("matchLinks").GetArrayLength(), Is.EqualTo(0));
	}

	[Test]
	public async Task CreateTrainingEvent_WithWeeklyRecurrence_CreatesSeries()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);
		var start = new DateTime(2026, 8, 3, 19, 0, 0, DateTimeKind.Utc);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Training",
				TeamScope = "Both",
				Title = "Weekly training",
				Description = "Bring boots and water.",
				StartDateTime = start,
				EndDateTime = start.AddHours(1),
				Location = "Garden Village Recreation Ground",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = false,
				CreateMatches = Array.Empty<object>(),
				Recurrence = new
				{
					IsRecurring = true,
					Interval = 1,
					Unit = "Weeks",
					EndDate = start.AddDays(14).Date
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
		Assert.That(factory.ClubEventService.Events, Has.Count.EqualTo(3));
		Assert.That(
			factory.ClubEventService.Events.Select(clubEvent => clubEvent.Title).ToArray(),
			Is.EqualTo(
				new[]
				{
					"Training 03/08/2026",
					"Training 10/08/2026",
					"Training 17/08/2026"
				}
			)
		);
		Assert.That(
			factory.ClubEventService.Events.Select(clubEvent => clubEvent.StartDateTime).ToArray(),
			Is.EqualTo(new[] { start, start.AddDays(7), start.AddDays(14) })
		);
		Assert.That(
			factory.ClubEventService.Events.Select(clubEvent => clubEvent.RecurrenceSeriesId).Distinct().Count(),
			Is.EqualTo(1)
		);
		Assert.That(
			factory.ClubEventService.Events.All(clubEvent => clubEvent.Recurrence?.TotalOccurrences == 3),
			Is.True
		);
	}

	[Test]
	public async Task CreateSocialEvent_AsCoach_CreatesEvent()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Social",
				TeamScope = "Both",
				Title = "Player social",
				Description = "Meet at the club.",
				StartDateTime = DateTime.UtcNow.AddDays(7),
				Location = "Clubhouse",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = false,
				CreateMatches = Array.Empty<object>()
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
	}

	[Test]
	public async Task CreateSocialEvent_AsPlayer_ReturnsForbidden()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Social",
				TeamScope = "Both",
				Title = "Player social",
				Description = "Meet at the club.",
				StartDateTime = DateTime.UtcNow.AddDays(7),
				Location = "Clubhouse",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = false,
				CreateMatches = Array.Empty<object>()
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
	}

	[Test]
	public async Task GetEvents_AsPlayer_HidesMeetingEvents()
	{
		var meetingEventId = Guid.Parse("30000000-0000-0000-0000-000000000001");

		factory.ClubEventService.Events.AddRange(
			new[]
			{
				new ClubEvent
				{
					Id = meetingEventId,
					Type = ClubEventType.Meeting,
					TeamScope = ClubEventTeamScope.Both,
					Title = "Coaches meeting",
					StartDateTime = DateTime.UtcNow.AddDays(1),
					Location = "Clubhouse"
				},
				new ClubEvent
				{
					Id = Guid.Parse("30000000-0000-0000-0000-000000000002"),
					Type = ClubEventType.Training,
					TeamScope = ClubEventTeamScope.Both,
					Title = "Training",
					StartDateTime = DateTime.UtcNow.AddDays(2),
					Location = "Garden Village Recreation Ground"
				}
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
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
	public async Task GetEvents_AsCoach_IncludesMeetingEvents()
	{
		factory.ClubEventService.Events.Add(
			new ClubEvent
			{
				Id = Guid.Parse("30000000-0000-0000-0000-000000000003"),
				Type = ClubEventType.Meeting,
				TeamScope = ClubEventTeamScope.Both,
				Title = "Coaches meeting",
				StartDateTime = DateTime.UtcNow.AddDays(1),
				Location = "Clubhouse"
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.CoachEmail,
			TestUsers.CoachPassword
		);

		var response = await client.GetAsync("/api/events");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Coaches meeting"));
	}

	[Test]
	public async Task CreateMatchEvent_EventOnly_CreatesEventWithoutCreatingMatches()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				TeamScope = "First",
				Title = "Placeholder fixture",
				Description = "Match event without linked match records yet.",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "TBC",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = false,
				CreateMatches = Array.Empty<object>()
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
		Assert.That(factory.MatchService.Matches, Is.Empty);

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo("Match"));
		Assert.That(document.RootElement.GetProperty("teamScope").GetString(), Is.EqualTo("First"));
		Assert.That(document.RootElement.GetProperty("matchLinks").GetArrayLength(), Is.EqualTo(0));
	}

	[Test]
	public async Task CreateMatchEvent_WithExistingMatchLink_CreatesLinkedEvent()
	{
		var matchId = Guid.Parse("40000000-0000-0000-0000-000000000001");

		factory.MatchService.Matches.Add(
			new Match
			{
				Id = matchId,
				Team = ClubTeam.First,
				Opponent = "Gors AFC",
				Date = DateTime.UtcNow.AddDays(10),
				Venue = MatchVenue.Home
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				TeamScope = "First",
				Title = "Kingsbridge Colts vs Gors AFC",
				Description = "League fixture.",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Home pitch",
				MatchLinks = new[]
				{
					new
					{
						Team = "First",
						MatchId = matchId
					}
				},
				CreateLinkedMatches = false,
				CreateMatches = Array.Empty<object>()
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		var matchLinks = document.RootElement.GetProperty("matchLinks");
		var eventId = document.RootElement.GetProperty("id").GetGuid();
		var linkedMatch = factory.MatchService.Matches.Single(match => match.Id == matchId);

		Assert.That(matchLinks.GetArrayLength(), Is.EqualTo(1));
		Assert.That(matchLinks[0].GetProperty("team").GetString(), Is.EqualTo("First"));
		Assert.That(matchLinks[0].GetProperty("matchId").GetGuid(), Is.EqualTo(matchId));
		Assert.That(linkedMatch.ClubEventId, Is.EqualTo(eventId));
	}

	[Test]
	public async Task CreateMatchEvent_WithExistingMatchLinkForWrongTeam_ReturnsBadRequest()
	{
		var matchId = Guid.Parse("40000000-0000-0000-0000-000000000101");

		factory.MatchService.Matches.Add(
			new Match
			{
				Id = matchId,
				Team = ClubTeam.Second,
				Opponent = "Gors AFC",
				Date = DateTime.UtcNow.AddDays(10),
				Venue = MatchVenue.Home
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				TeamScope = "First",
				Title = "Wrong team link",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Home pitch",
				MatchLinks = new[]
				{
					new
					{
						Team = "First",
						MatchId = matchId
					}
				},
				CreateLinkedMatches = false,
				CreateMatches = Array.Empty<object>()
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}

	[Test]
	public async Task CreateMatchEvent_WithMissingExistingMatch_ReturnsBadRequest()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				TeamScope = "First",
				Title = "Missing match event",
				Description = "Should fail.",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Home pitch",
				MatchLinks = new[]
				{
					new
					{
						Team = "First",
						MatchId = Guid.Parse("40000000-0000-0000-0000-000000000099")
					}
				},
				CreateLinkedMatches = false,
				CreateMatches = Array.Empty<object>()
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}

	[Test]
	public async Task CreateMatchEvent_WithExplicitMatchCreation_CreatesMatchAndLinksEvent()
	{
		var eventStartDate = DateTime.UtcNow.AddDays(14);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				TeamScope = "First",
				Title = "Kingsbridge Colts vs Loughor",
				Description = "League fixture.",
				StartDateTime = eventStartDate,
				Location = "Garden Village Recreation Ground",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = true,
				CreateMatches = new[]
				{
					new
					{
						Team = "First",
						Opponent = "Loughor",
						Competition = "League",
						Venue = "Home",
						Location = "Garden Village Recreation Ground",
						SelectedFormation = "FourThreeThree"
					}
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
		Assert.That(factory.MatchService.Matches, Has.Count.EqualTo(1));

		var createdMatch = factory.MatchService.Matches.Single();

		Assert.That(createdMatch.Team, Is.EqualTo(ClubTeam.First));
		Assert.That(createdMatch.Opponent, Is.EqualTo("Loughor"));
		Assert.That(createdMatch.Competition, Is.EqualTo("League"));
		Assert.That(createdMatch.Date, Is.EqualTo(eventStartDate).Within(TimeSpan.FromSeconds(1)));
		Assert.That(createdMatch.Venue, Is.EqualTo(MatchVenue.Home));
		Assert.That(createdMatch.Location, Is.EqualTo("Garden Village Recreation Ground"));
		Assert.That(createdMatch.SeasonId, Is.EqualTo(TestSeasons.ActiveSeasonId));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		var matchLinks = document.RootElement.GetProperty("matchLinks");
		var eventId = document.RootElement.GetProperty("id").GetGuid();

		Assert.That(matchLinks.GetArrayLength(), Is.EqualTo(1));
		Assert.That(matchLinks[0].GetProperty("team").GetString(), Is.EqualTo("First"));
		Assert.That(matchLinks[0].GetProperty("matchId").GetGuid(), Is.EqualTo(createdMatch.Id));
		Assert.That(createdMatch.ClubEventId, Is.EqualTo(eventId));
	}


	[Test]
	public async Task CreateMatchEvent_WithExplicitMatchCreationAndNoActiveSeason_ReturnsBadRequest()
	{
		factory.SeasonService.Seasons.ForEach(season => season.IsActive = false);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				TeamScope = "First",
				Title = "Kingsbridge Colts vs Loughor",
				Description = "League fixture.",
				StartDateTime = DateTime.UtcNow.AddDays(14),
				Location = "Garden Village Recreation Ground",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = true,
				CreateMatches = new[]
				{
					new
					{
						Team = "First",
						Opponent = "Loughor",
						Competition = "League",
						Venue = "Home",
						Location = "Garden Village Recreation Ground",
						SelectedFormation = "FourThreeThree"
					}
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That(factory.MatchService.Matches, Is.Empty);
	}

	[Test]
	public async Task CreateMatchEvent_WithBothTeamExplicitMatchCreation_CreatesTwoMatchesAndLinksEvent()
	{
		var eventStartDate = DateTime.UtcNow.AddDays(14);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				TeamScope = "Both",
				Title = "Double header",
				Description = "Both teams have fixtures.",
				StartDateTime = eventStartDate,
				Location = "Garden Village Recreation Ground",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = true,
				CreateMatches = new[]
				{
					new
					{
						Team = "First",
						Opponent = "Loughor",
						Competition = "League",
						Venue = "Home",
						Location = "Garden Village Recreation Ground",
						SelectedFormation = "FourThreeThree"
					},
					new
					{
						Team = "Second",
						Opponent = "Gors AFC",
						Competition = "Cup",
						Venue = "Away",
						Location = "Gors Ground",
						SelectedFormation = "FourThreeThree"
					}
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
		Assert.That(factory.MatchService.Matches, Has.Count.EqualTo(2));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		var matchLinks = document.RootElement.GetProperty("matchLinks");
		var eventId = document.RootElement.GetProperty("id").GetGuid();
		var firstTeamMatch = factory.MatchService.Matches.Single(match => match.Team == ClubTeam.First);
		var secondTeamMatch = factory.MatchService.Matches.Single(match => match.Team == ClubTeam.Second);

		Assert.That(matchLinks.GetArrayLength(), Is.EqualTo(2));
		Assert.That(matchLinks.EnumerateArray().Any(matchLink => matchLink.GetProperty("team").GetString() == "First"), Is.True);
		Assert.That(matchLinks.EnumerateArray().Any(matchLink => matchLink.GetProperty("team").GetString() == "Second"), Is.True);
		Assert.That(firstTeamMatch.Venue, Is.EqualTo(MatchVenue.Home));
		Assert.That(firstTeamMatch.Competition, Is.EqualTo("League"));
		Assert.That(firstTeamMatch.Location, Is.EqualTo("Garden Village Recreation Ground"));
		Assert.That(firstTeamMatch.SeasonId, Is.EqualTo(TestSeasons.ActiveSeasonId));
		Assert.That(firstTeamMatch.ClubEventId, Is.EqualTo(eventId));
		Assert.That(secondTeamMatch.Venue, Is.EqualTo(MatchVenue.Away));
		Assert.That(secondTeamMatch.Competition, Is.EqualTo("Cup"));
		Assert.That(secondTeamMatch.Location, Is.EqualTo("Gors Ground"));
		Assert.That(secondTeamMatch.SeasonId, Is.EqualTo(TestSeasons.ActiveSeasonId));
		Assert.That(secondTeamMatch.ClubEventId, Is.EqualTo(eventId));
	}

	[Test]
	public async Task CreateMatchEvent_WithBothTeamCreationMissingOneTeam_ReturnsBadRequest()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				TeamScope = "Both",
				Title = "Incomplete double header",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Home pitch",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = true,
				CreateMatches = new[]
				{
					new
					{
						Team = "First",
						Opponent = "Loughor",
						Venue = "Home"
					}
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That(factory.MatchService.Matches, Is.Empty);
	}

	[Test]
	public async Task CreateMatchEvent_WithCreateMatchesButExplicitCreationFalse_ReturnsBadRequest()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				TeamScope = "First",
				Title = "Should not silently create match",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Home pitch",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = false,
				CreateMatches = new[]
				{
					new
					{
						Team = "First",
						Opponent = "Loughor",
						Venue = "Home"
					}
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That(factory.MatchService.Matches, Is.Empty);
	}

	[Test]
	public async Task CreateMatchEvent_WithExistingLinksAndExplicitCreation_ReturnsBadRequest()
	{
		var matchId = Guid.Parse("40000000-0000-0000-0000-000000000003");

		factory.MatchService.Matches.Add(
			new Match
			{
				Id = matchId,
				Team = ClubTeam.First,
				Opponent = "Gors AFC",
				Date = DateTime.UtcNow.AddDays(10),
				Venue = MatchVenue.Home
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Match",
				TeamScope = "First",
				Title = "Invalid match event",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Home pitch",
				MatchLinks = new[]
				{
					new
					{
						Team = "First",
						MatchId = matchId
					}
				},
				CreateLinkedMatches = true,
				CreateMatches = new[]
				{
					new
					{
						Team = "First",
						Opponent = "Loughor",
						Venue = "Home"
					}
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}

	[Test]
	public async Task CreateTrainingEvent_WithMatchCreationFields_ReturnsBadRequest()
	{
		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.PostAsJsonAsync(
			"/api/events",
			new
			{
				Type = "Training",
				TeamScope = "Both",
				Title = "Invalid training event",
				StartDateTime = DateTime.UtcNow.AddDays(10),
				Location = "Training pitch",
				MatchLinks = Array.Empty<object>(),
				CreateLinkedMatches = true,
				CreateMatches = new[]
				{
					new
					{
						Team = "First",
						Opponent = "Loughor",
						Venue = "Home"
					}
				}
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
	}

	[Test]
	public async Task DeleteMatchEvent_WithLinkedMatches_DeletesEventAndLinkedMatches()
	{
		var eventId = Guid.Parse("50000000-0000-0000-0000-000000000001");
		var firstMatchId = Guid.Parse("50000000-0000-0000-0000-000000000101");
		var secondMatchId = Guid.Parse("50000000-0000-0000-0000-000000000102");
		var unrelatedMatchId = Guid.Parse("50000000-0000-0000-0000-000000000103");

		factory.ClubEventService.Events.Add(
			new ClubEvent
			{
				Id = eventId,
				Type = ClubEventType.Match,
				TeamScope = ClubEventTeamScope.Both,
				Title = "Double header",
				StartDateTime = DateTime.UtcNow.AddDays(5),
				Location = "Garden Village Recreation Ground",
				MatchLinks =
				[
					new ClubEventMatchLink
					{
						Team = ClubTeam.First,
						MatchId = firstMatchId
					},
					new ClubEventMatchLink
					{
						Team = ClubTeam.Second,
						MatchId = secondMatchId
					}
				]
			}
		);

		factory.MatchService.Matches.AddRange(
			new[]
			{
				new Match
				{
					Id = firstMatchId,
					Team = ClubTeam.First,
					Opponent = "Loughor",
					Date = DateTime.UtcNow.AddDays(5),
					Venue = MatchVenue.Home,
					ClubEventId = eventId
				},
				new Match
				{
					Id = secondMatchId,
					Team = ClubTeam.Second,
					Opponent = "Gors AFC",
					Date = DateTime.UtcNow.AddDays(5),
					Venue = MatchVenue.Away,
					ClubEventId = eventId
				},
				new Match
				{
					Id = unrelatedMatchId,
					Team = ClubTeam.First,
					Opponent = "Unrelated",
					Date = DateTime.UtcNow.AddDays(8),
					Venue = MatchVenue.Home
				}
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.AdminEmail,
			TestUsers.AdminPassword
		);

		var response = await client.DeleteAsync($"/api/events/{eventId}");

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
		Assert.That(factory.ClubEventService.Events.Any(clubEvent => clubEvent.Id == eventId), Is.False);
		Assert.That(factory.MatchService.Matches.Any(match => match.Id == firstMatchId), Is.False);
		Assert.That(factory.MatchService.Matches.Any(match => match.Id == secondMatchId), Is.False);
		Assert.That(factory.MatchService.Matches.Any(match => match.Id == unrelatedMatchId), Is.True);
	}

	[Test]
	public async Task MarkSeen_AsLinkedPlayer_AddsSeenStatus()
	{
		var eventId = Guid.Parse("60000000-0000-0000-0000-000000000001");

		factory.ClubEventService.Events.Add(
			new ClubEvent
			{
				Id = eventId,
				Type = ClubEventType.Training,
				TeamScope = ClubEventTeamScope.Both,
				Title = "Training",
				StartDateTime = DateTime.UtcNow.AddDays(2),
				Location = "Garden Village Recreation Ground"
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.PutAsJsonAsync($"/api/events/{eventId}/seen", new { });

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		var seenBy = document.RootElement.GetProperty("seenBy");

		Assert.That(seenBy.GetArrayLength(), Is.EqualTo(1));
		Assert.That(seenBy[0].GetProperty("playerId").GetGuid(), Is.EqualTo(TestUsers.LinkedPlayerId));
	}

	[Test]
	public async Task SetAvailability_AsLinkedPlayer_UpdatesAvailabilityAndMarksSeen()
	{
		var eventId = Guid.Parse("60000000-0000-0000-0000-000000000002");

		factory.ClubEventService.Events.Add(
			new ClubEvent
			{
				Id = eventId,
				Type = ClubEventType.Match,
				TeamScope = ClubEventTeamScope.First,
				Title = "Match event",
				StartDateTime = DateTime.UtcNow.AddDays(2),
				Location = "Home pitch"
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.PutAsJsonAsync(
			$"/api/events/{eventId}/availability",
			new
			{
				Status = "Available"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		var availabilityResponses = document.RootElement.GetProperty("availabilityResponses");
		var seenBy = document.RootElement.GetProperty("seenBy");

		Assert.That(availabilityResponses.GetArrayLength(), Is.EqualTo(1));
		Assert.That(availabilityResponses[0].GetProperty("playerId").GetGuid(), Is.EqualTo(TestUsers.LinkedPlayerId));
		Assert.That(availabilityResponses[0].GetProperty("status").GetString(), Is.EqualTo("Available"));

		Assert.That(seenBy.GetArrayLength(), Is.EqualTo(1));
		Assert.That(seenBy[0].GetProperty("playerId").GetGuid(), Is.EqualTo(TestUsers.LinkedPlayerId));
	}

	[Test]
	public async Task SetAvailability_AsPlayerForMeetingEvent_ReturnsNotFound()
	{
		var eventId = Guid.Parse("60000000-0000-0000-0000-000000000003");

		factory.ClubEventService.Events.Add(
			new ClubEvent
			{
				Id = eventId,
				Type = ClubEventType.Meeting,
				TeamScope = ClubEventTeamScope.Both,
				Title = "Coaches meeting",
				StartDateTime = DateTime.UtcNow.AddDays(2),
				Location = "Clubhouse"
			}
		);

		var client = await factory.CreateAuthenticatedClientAsync(
			TestUsers.PlayerEmail,
			TestUsers.PlayerPassword
		);

		var response = await client.PutAsJsonAsync(
			$"/api/events/{eventId}/availability",
			new
			{
				Status = "Available"
			}
		);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
	}
}
