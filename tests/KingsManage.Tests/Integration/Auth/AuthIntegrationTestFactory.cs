using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KingsManage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KingsManage.Tests.Integration.Auth;

public sealed class AuthIntegrationTestFactory : WebApplicationFactory<Program>
{
	public TestUserService UserService { get; } = new();
	public TestPlayerService PlayerService { get; } = new();
	public TestMatchService MatchService { get; } = new();
	public TestSeasonService SeasonService { get; } = new();
	public TestStatsService StatsService { get; } = new();
	public TestClubEventService ClubEventService { get; } = new();
	public TestClubPostService ClubPostService { get; } = new();
	public TestClubNotificationService ClubNotificationService { get; } = new();
	public TestMessageService MessageService { get; } = new();
	public TestClubFileService ClubFileService { get; } = new();
	public TestStoredFileObjectService StoredFileObjectService { get; } = new();
	public TestFileStorageService FileStorageService { get; } = new();
	public TestFileLifecycleService FileLifecycleService { get; } = new();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureAppConfiguration(configurationBuilder =>
		{
			var testConfiguration = new Dictionary<string, string?>
			{
				["MongoDb:ConnectionString"] = "mongodb://localhost:27017",
				["MongoDb:DatabaseName"] = "KingsManageIntegrationTests",
				["Jwt:Issuer"] = "KingsManage.Tests",
				["Jwt:Audience"] = "KingsManage.Tests",
				["Jwt:Secret"] = "integration-test-secret-that-is-long-enough-for-hmac-signing",
				["Jwt:ExpiryMinutes"] = "60",
				["DefaultAdmin:Email"] = "default-admin@test.local",
				["DefaultAdmin:Password"] = "DefaultAdmin123!",
				["Tenancy:RunStartupMigration"] = "false",
				["FileLifecycle:Enabled"] = "false"
			};

			configurationBuilder.AddInMemoryCollection(testConfiguration);
		});

		builder.ConfigureTestServices(services =>
		{
			services.RemoveAll<IUserService>();
			services.RemoveAll<IPlayerService>();
			services.RemoveAll<IMatchService>();
			services.RemoveAll<ISeasonService>();
			services.RemoveAll<IStatsService>();
			services.RemoveAll<IClubEventService>();
			services.RemoveAll<IClubPostService>();
			services.RemoveAll<IClubNotificationService>();
			services.RemoveAll<IMessageService>();
			services.RemoveAll<IClubFileService>();
			services.RemoveAll<IStoredFileObjectService>();
			services.RemoveAll<IFileStorageService>();
			services.RemoveAll<IFileLifecycleService>();

			services.AddSingleton<IUserService>(UserService);
			services.AddSingleton<IPlayerService>(PlayerService);
			services.AddSingleton<IMatchService>(MatchService);
			services.AddSingleton<ISeasonService>(SeasonService);
			services.AddSingleton<IStatsService>(StatsService);
			services.AddSingleton<IClubEventService>(ClubEventService);
			services.AddSingleton<IClubPostService>(ClubPostService);
			services.AddSingleton<IClubNotificationService>(ClubNotificationService);
			services.AddSingleton<IMessageService>(MessageService);
			services.AddSingleton<IClubFileService>(ClubFileService);
			services.AddSingleton<IStoredFileObjectService>(StoredFileObjectService);
			services.AddSingleton<IFileStorageService>(FileStorageService);
			services.AddSingleton<IFileLifecycleService>(FileLifecycleService);
		});
	}

	public void SeedDefaultUsers()
	{
		UserService.Clear();

		UserService.AddUser(
			new AppUser
			{
				Id = TestUsers.AdminId,
				Email = TestUsers.AdminEmail,
				Role = UserRole.Admin,
				IsActive = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			},
			TestUsers.AdminPassword
		);

		UserService.AddUser(
			new AppUser
			{
				Id = TestUsers.CoachId,
				Email = TestUsers.CoachEmail,
				Role = UserRole.Coach,
				IsActive = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			},
			TestUsers.CoachPassword
		);

		UserService.AddUser(
			new AppUser
			{
				Id = TestUsers.PlayerId,
				Email = TestUsers.PlayerEmail,
				Role = UserRole.Player,
				IsActive = true,
				PlayerId = TestUsers.LinkedPlayerId,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			},
			TestUsers.PlayerPassword
		);

		UserService.AddUser(
			new AppUser
			{
				Id = TestUsers.InactiveId,
				Email = TestUsers.InactiveEmail,
				Role = UserRole.Player,
				IsActive = false,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			},
			TestUsers.InactivePassword
		);
	}

	public async Task<HttpClient> CreateAuthenticatedClientAsync(
		string email,
		string password
	)
	{
		var client = CreateClient();

		var loginResponse = await client.PostAsJsonAsync(
			"/api/auth/login",
			new
			{
				Email = email,
				Password = password
			}
		);

		loginResponse.EnsureSuccessStatusCode();

		var json = await loginResponse.Content.ReadAsStringAsync();

		using var document = JsonDocument.Parse(json);

		var token = document.RootElement.GetProperty("token").GetString();

		if (string.IsNullOrWhiteSpace(token))
		{
			throw new InvalidOperationException("Login did not return a JWT token.");
		}

		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
			"Bearer",
			token
		);

		return client;
	}
}

public static class TestUsers
{
	public static readonly Guid AdminId = Guid.Parse("10000000-0000-0000-0000-000000000001");
	public static readonly Guid CoachId = Guid.Parse("10000000-0000-0000-0000-000000000002");
	public static readonly Guid PlayerId = Guid.Parse("10000000-0000-0000-0000-000000000003");
	public static readonly Guid InactiveId = Guid.Parse("10000000-0000-0000-0000-000000000004");
	public static readonly Guid LinkedPlayerId = Guid.Parse("20000000-0000-0000-0000-000000000001");

	public const string AdminEmail = "admin@test.local";
	public const string CoachEmail = "coach@test.local";
	public const string PlayerEmail = "player@test.local";
	public const string InactiveEmail = "inactive@test.local";

	public const string AdminPassword = "Admin123!";
	public const string CoachPassword = "Coach123!";
	public const string PlayerPassword = "Player123!";
	public const string InactivePassword = "Inactive123!";
}


public static class TestSeasons
{
	public static readonly Guid ActiveSeasonId = Guid.Parse("70000000-0000-0000-0000-000000000001");
}

public sealed class TestMessageService : IMessageService
{
	public List<MessageThread> Threads { get; } = [];
	public List<Message> Messages { get; } = [];

	public Task<IReadOnlyList<MessageThread>> GetThreadsForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<MessageThread>>(Threads
			.Where(thread => thread.Participants.Any(participant => participant.UserId == userId))
			.OrderByDescending(thread => thread.UpdatedAt)
			.ToList());

	public Task<MessageThread?> GetThreadForUserAsync(Guid threadId, Guid userId, CancellationToken cancellationToken = default) =>
		Task.FromResult(Threads.FirstOrDefault(thread =>
			thread.Id == threadId && thread.Participants.Any(participant => participant.UserId == userId)));

	public Task<MessageThread> GetOrCreateDirectThreadAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default)
	{
		var ids = new[] { firstUserId, secondUserId }.OrderBy(id => id).ToArray();
		var pairKey = $"{ids[0]}:{ids[1]}";
		var existing = Threads.FirstOrDefault(thread => thread.DirectPairKey == pairKey);
		if (existing is not null)
		{
			return Task.FromResult(existing);
		}

		var now = DateTime.UtcNow;
		var thread = new MessageThread
		{
			Id = Guid.NewGuid(),
			DirectPairKey = pairKey,
			Participants =
			[
				new MessageThreadParticipant { UserId = firstUserId, JoinedAt = now, LastReadAt = now },
				new MessageThreadParticipant { UserId = secondUserId, JoinedAt = now, LastReadAt = now }
			],
			CreatedAt = now,
			UpdatedAt = now
		};
		Threads.Add(thread);
		return Task.FromResult(thread);
	}

	public Task<IReadOnlyList<Message>> GetMessagesAsync(Guid threadId, CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<Message>>(Messages.Where(message => message.ThreadId == threadId).OrderBy(message => message.CreatedAt).ToList());

	public Task<Message> CreateMessageAsync(Message message, CancellationToken cancellationToken = default)
	{
		message.Id = message.Id == Guid.Empty ? Guid.NewGuid() : message.Id;
		message.Body = message.Body.Trim();
		message.Status = MessageStatus.Active;
		message.CreatedAt = DateTime.UtcNow;
		Messages.Add(message);
		var thread = Threads.Single(item => item.Id == message.ThreadId);
		thread.UpdatedAt = message.CreatedAt;
		return Task.FromResult(message);
	}

	public Task<MessageThread?> MarkReadAsync(Guid threadId, Guid userId, CancellationToken cancellationToken = default)
	{
		var thread = Threads.FirstOrDefault(item => item.Id == threadId && item.Participants.Any(participant => participant.UserId == userId));
		if (thread is not null)
		{
			thread.Participants.First(participant => participant.UserId == userId).LastReadAt = DateTime.UtcNow;
		}
		return Task.FromResult(thread);
	}

	public Task<Message?> DeleteOwnMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default)
	{
		var message = Messages.FirstOrDefault(item => item.Id == messageId && item.SenderUserId == userId && item.Status == MessageStatus.Active);
		if (message is not null)
		{
			message.Status = MessageStatus.Deleted;
			message.Body = string.Empty;
			message.DeletedAt = DateTime.UtcNow;
		}
		return Task.FromResult(message);
	}
}

public sealed class TestUserService : IUserService
{
	private readonly List<AppUser> _users = new();
	private readonly Dictionary<Guid, string> _passwordsByUserId = new();

	public IReadOnlyList<AppUser> Users => _users;

	public void Clear()
	{
		_users.Clear();
		_passwordsByUserId.Clear();
	}

	public void AddUser(AppUser user, string password)
	{
		if (user.Id == Guid.Empty)
		{
			user.Id = Guid.NewGuid();
		}

		user.Email = NormaliseEmail(user.Email);

		if (user.CreatedAt == default)
		{
			user.CreatedAt = DateTime.UtcNow;
		}

		user.UpdatedAt = DateTime.UtcNow;

		_users.Add(user);
		_passwordsByUserId[user.Id] = password;
	}

	public Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<AppUser>>(_users.ToList());
	}

	public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(_users.FirstOrDefault(user => user.Id == id));
	}

	public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
	{
		var normalisedEmail = NormaliseEmail(email);

		return Task.FromResult(
			_users.FirstOrDefault(user =>
				user.Email.Equals(normalisedEmail, StringComparison.OrdinalIgnoreCase)
			)
		);
	}

	public Task<AppUser> CreateAsync(
		AppUser user,
		string password,
		CancellationToken cancellationToken = default
	)
	{
		AddUser(user, password);

		return Task.FromResult(user);
	}

	public Task<AppUser?> UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
	{
		var existingUser = _users.FirstOrDefault(currentUser => currentUser.Id == user.Id);

		if (existingUser is null)
		{
			return Task.FromResult<AppUser?>(null);
		}

		existingUser.Email = NormaliseEmail(user.Email);
		existingUser.Role = user.Role;
		existingUser.PlayerId = user.PlayerId;
		existingUser.IsActive = user.IsActive;
		existingUser.UpdatedAt = DateTime.UtcNow;

		return Task.FromResult<AppUser?>(existingUser);
	}

	public Task<AppUser?> SetActiveAsync(
		Guid id,
		bool isActive,
		CancellationToken cancellationToken = default
	)
	{
		var user = _users.FirstOrDefault(currentUser => currentUser.Id == id);

		if (user is null)
		{
			return Task.FromResult<AppUser?>(null);
		}

		user.IsActive = isActive;
		user.UpdatedAt = DateTime.UtcNow;

		return Task.FromResult<AppUser?>(user);
	}

	public Task<AppUser?> ValidateCredentialsAsync(
		string email,
		string password,
		CancellationToken cancellationToken = default
	)
	{
		var normalisedEmail = NormaliseEmail(email);

		var user = _users.FirstOrDefault(currentUser =>
			currentUser.Email.Equals(normalisedEmail, StringComparison.OrdinalIgnoreCase)
		);

		if (user is null || !user.IsActive)
		{
			return Task.FromResult<AppUser?>(null);
		}

		if (!_passwordsByUserId.TryGetValue(user.Id, out var savedPassword))
		{
			return Task.FromResult<AppUser?>(null);
		}

		if (!savedPassword.Equals(password, StringComparison.Ordinal))
		{
			return Task.FromResult<AppUser?>(null);
		}

		user.LastLoginAt = DateTime.UtcNow;
		user.UpdatedAt = DateTime.UtcNow;

		return Task.FromResult<AppUser?>(user);
	}

	public Task<bool> ChangePasswordAsync(
		Guid id,
		string currentPassword,
		string newPassword,
		CancellationToken cancellationToken = default
	)
	{
		var user = _users.FirstOrDefault(currentUser => currentUser.Id == id);

		if (user is null || !user.IsActive)
		{
			return Task.FromResult(false);
		}

		if (!_passwordsByUserId.TryGetValue(user.Id, out var savedPassword))
		{
			return Task.FromResult(false);
		}

		if (!savedPassword.Equals(currentPassword, StringComparison.Ordinal))
		{
			return Task.FromResult(false);
		}

		_passwordsByUserId[user.Id] = newPassword;
		user.UpdatedAt = DateTime.UtcNow;

		return Task.FromResult(true);
	}

	public Task<bool> ResetPasswordAsync(
		Guid id,
		string newPassword,
		CancellationToken cancellationToken = default
	)
	{
		var user = _users.FirstOrDefault(currentUser => currentUser.Id == id);

		if (user is null)
		{
			return Task.FromResult(false);
		}

		_passwordsByUserId[user.Id] = newPassword;
		user.UpdatedAt = DateTime.UtcNow;

		return Task.FromResult(true);
	}

	public Task<AppUser> EnsureDefaultAdminUserAsync(
		string email,
		string password,
		CancellationToken cancellationToken = default
	)
	{
		if (_users.Any())
		{
			return Task.FromResult(_users.First());
		}

		var user = new AppUser
		{
			Id = Guid.NewGuid(),
			Email = email,
			Role = UserRole.Admin,
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		AddUser(user, password);

		return Task.FromResult(user);
	}

	private static string NormaliseEmail(string email)
	{
		return email.Trim().ToLowerInvariant();
	}
}

public sealed class TestPlayerService : IPlayerService
{
	public List<Player> Players { get; } = new();

	public Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<Player>>(Players.ToList());
	}

	public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(Players.FirstOrDefault(player => player.Id == id));
	}

	public Task<Player> CreateAsync(Player player, CancellationToken cancellationToken = default)
	{
		if (player.Id == Guid.Empty)
		{
			player.Id = Guid.NewGuid();
		}

		Players.Add(player);

		return Task.FromResult(player);
	}

	public Task<Player?> UpdateAsync(Player player, CancellationToken cancellationToken = default)
	{
		var existingPlayer = Players.FirstOrDefault(currentPlayer => currentPlayer.Id == player.Id);

		if (existingPlayer is null)
		{
			return Task.FromResult<Player?>(null);
		}

		var index = Players.IndexOf(existingPlayer);
		Players[index] = player;

		return Task.FromResult<Player?>(player);
	}

	public Task<Player?> SetActiveAsync(
		Guid id,
		bool isActive,
		CancellationToken cancellationToken = default
	)
	{
		var player = Players.FirstOrDefault(currentPlayer => currentPlayer.Id == id);

		if (player is null)
		{
			return Task.FromResult<Player?>(null);
		}

		player.IsActive = isActive;

		return Task.FromResult<Player?>(player);
	}
}

public sealed class TestMatchService : IMatchService
{
	public List<Match> Matches { get; } = new();

	public Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<Match>>(Matches.OrderBy(match => match.Date).ToList());
	}

	public Task<IReadOnlyList<Match>> GetBySeasonAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult<IReadOnlyList<Match>>(
			Matches
				.Where(match => match.SeasonId == seasonId)
				.OrderBy(match => match.Date)
				.ToList()
		);
	}

	public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(Matches.FirstOrDefault(match => match.Id == id));
	}

	public Task<Match> CreateAsync(Match match, CancellationToken cancellationToken = default)
	{
		if (match.Id == Guid.Empty)
		{
			match.Id = Guid.NewGuid();
		}

		match.CreatedAt = DateTime.UtcNow;
		match.UpdatedAt = DateTime.UtcNow;
		Matches.Add(match);

		return Task.FromResult(match);
	}

	public Task<Match?> UpdateAsync(Match match, CancellationToken cancellationToken = default)
	{
		var existingMatch = Matches.FirstOrDefault(currentMatch => currentMatch.Id == match.Id);

		if (existingMatch is null)
		{
			return Task.FromResult<Match?>(null);
		}

		var index = Matches.IndexOf(existingMatch);
		Matches[index] = match;

		return Task.FromResult<Match?>(match);
	}

	public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

		if (match is null)
		{
			return Task.FromResult(false);
		}

		Matches.Remove(match);

		return Task.FromResult(true);
	}

	public Task<Match?> SetResultAsync(
		Guid id,
		MatchResult result,
		CancellationToken cancellationToken = default
	)
	{
		var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

		if (match is null)
		{
			return Task.FromResult<Match?>(null);
		}

		match.Result = result;
		match.IsCompleted = true;

		return Task.FromResult<Match?>(match);
	}

	public Task<Match?> ClearResultAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

		if (match is null)
		{
			return Task.FromResult<Match?>(null);
		}

		match.Result = null;
		match.IsCompleted = false;

		return Task.FromResult<Match?>(match);
	}

	public Task<Match?> SetSelectedPlayersAsync(
		Guid id,
		List<SelectedPlayer> selectedPlayers,
		CancellationToken cancellationToken = default
	)
	{
		var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

		if (match is null)
		{
			return Task.FromResult<Match?>(null);
		}

		match.SelectedPlayers = selectedPlayers;

		return Task.FromResult<Match?>(match);
	}

	public Task<Match?> SetLineupFormationAsync(
		Guid id,
		LineupFormation formation,
		CancellationToken cancellationToken = default
	)
	{
		var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

		if (match is null)
		{
			return Task.FromResult<Match?>(null);
		}

		match.SelectedFormation = formation;

		return Task.FromResult<Match?>(match);
	}

	public Task<Match?> ToggleLineupLockedAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

		if (match is null)
		{
			return Task.FromResult<Match?>(null);
		}

		match.IsLineupLocked = !match.IsLineupLocked;

		return Task.FromResult<Match?>(match);
	}

	public Task<Match?> UpdateNotesAsync(
		Guid id,
		MatchNotes notes,
		CancellationToken cancellationToken = default
	)
	{
		var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

		if (match is null)
		{
			return Task.FromResult<Match?>(null);
		}

		match.Notes = notes;

		return Task.FromResult<Match?>(match);
	}

	public Task<Match?> UpdatePlayerStatsAsync(
		Guid id,
		List<MatchPlayerStats> playerStats,
		CancellationToken cancellationToken = default
	)
	{
		var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

		if (match is null)
		{
			return Task.FromResult<Match?>(null);
		}

		match.PlayerStats = playerStats;

		return Task.FromResult<Match?>(match);
	}

	public Task<Match?> PostponeAsync(
		Guid id,
		DateTime newDate,
		string? reason,
		CancellationToken cancellationToken = default
	)
	{
		var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

		if (match is null)
		{
			return Task.FromResult<Match?>(null);
		}

		match.Date = newDate;
		match.State = MatchState.Postponed;

		return Task.FromResult<Match?>(match);
	}

	public Task<Match?> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

		if (match is null)
		{
			return Task.FromResult<Match?>(null);
		}

		match.State = MatchState.Upcoming;

		return Task.FromResult<Match?>(match);
	}
}

public sealed class TestClubEventService : IClubEventService
{
	public List<ClubEvent> Events { get; } = new();

	public Task<IReadOnlyList<ClubEvent>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<ClubEvent>>(
			Events.OrderBy(clubEvent => clubEvent.StartDateTime).ToList()
		);
	}

	public Task<ClubEvent?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(Events.FirstOrDefault(clubEvent => clubEvent.Id == id));
	}

	public Task<ClubEvent> CreateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	)
	{
		if (clubEvent.Id == Guid.Empty)
		{
			clubEvent.Id = Guid.NewGuid();
		}

		clubEvent.CreatedAt = DateTime.UtcNow;
		clubEvent.UpdatedAt = DateTime.UtcNow;
		clubEvent.MatchLinks ??= [];
		clubEvent.AvailabilityResponses ??= [];
		clubEvent.SeenBy ??= [];

		Events.Add(clubEvent);

		return Task.FromResult(clubEvent);
	}

	public Task<ClubEvent?> UpdateAsync(
		ClubEvent clubEvent,
		CancellationToken cancellationToken = default
	)
	{
		var existingEvent = Events.FirstOrDefault(currentEvent => currentEvent.Id == clubEvent.Id);

		if (existingEvent is null)
		{
			return Task.FromResult<ClubEvent?>(null);
		}

		clubEvent.UpdatedAt = DateTime.UtcNow;
		clubEvent.MatchLinks ??= [];
		clubEvent.AvailabilityResponses ??= [];
		clubEvent.SeenBy ??= [];

		var index = Events.IndexOf(existingEvent);
		Events[index] = clubEvent;

		return Task.FromResult<ClubEvent?>(clubEvent);
	}

	public Task<bool> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var existingEvent = Events.FirstOrDefault(currentEvent => currentEvent.Id == id);

		if (existingEvent is null)
		{
			return Task.FromResult(false);
		}

		Events.Remove(existingEvent);

		return Task.FromResult(true);
	}

	public Task<ClubEvent?> MarkSeenAsync(
		Guid eventId,
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		var clubEvent = Events.FirstOrDefault(currentEvent => currentEvent.Id == eventId);

		if (clubEvent is null)
		{
			return Task.FromResult<ClubEvent?>(null);
		}

		clubEvent.SeenBy ??= [];

		var existingSeenStatus = clubEvent.SeenBy.FirstOrDefault(seen => seen.PlayerId == playerId);

		if (existingSeenStatus is null)
		{
			clubEvent.SeenBy.Add(
				new ClubEventSeenStatus
				{
					PlayerId = playerId,
					SeenAt = DateTime.UtcNow
				}
			);
		}
		else
		{
			existingSeenStatus.SeenAt = DateTime.UtcNow;
		}

		clubEvent.UpdatedAt = DateTime.UtcNow;

		return Task.FromResult<ClubEvent?>(clubEvent);
	}

	public Task<ClubEvent?> SetAvailabilityAsync(
		Guid eventId,
		Guid playerId,
		ClubEventAvailabilityStatus status,
		CancellationToken cancellationToken = default
	)
	{
		var clubEvent = Events.FirstOrDefault(currentEvent => currentEvent.Id == eventId);

		if (clubEvent is null)
		{
			return Task.FromResult<ClubEvent?>(null);
		}

		clubEvent.AvailabilityResponses ??= [];
		clubEvent.SeenBy ??= [];

		var existingAvailability = clubEvent.AvailabilityResponses.FirstOrDefault(
			response => response.PlayerId == playerId
		);

		if (existingAvailability is null)
		{
			clubEvent.AvailabilityResponses.Add(
				new ClubEventAvailabilityResponse
				{
					PlayerId = playerId,
					Status = status,
					UpdatedAt = DateTime.UtcNow
				}
			);
		}
		else
		{
			existingAvailability.Status = status;
			existingAvailability.UpdatedAt = DateTime.UtcNow;
		}

		var existingSeenStatus = clubEvent.SeenBy.FirstOrDefault(seen => seen.PlayerId == playerId);

		if (existingSeenStatus is null)
		{
			clubEvent.SeenBy.Add(
				new ClubEventSeenStatus
				{
					PlayerId = playerId,
					SeenAt = DateTime.UtcNow
				}
			);
		}
		else
		{
			existingSeenStatus.SeenAt = DateTime.UtcNow;
		}

		clubEvent.UpdatedAt = DateTime.UtcNow;

		return Task.FromResult<ClubEvent?>(clubEvent);
	}
}


public sealed class TestSeasonService : ISeasonService
{
	public List<Season> Seasons { get; } =
	[
		new Season
		{
			Id = TestSeasons.ActiveSeasonId,
			Name = "2025-2026",
			StartDate = new DateTime(2025, 7, 1),
			EndDate = new DateTime(2026, 6, 30),
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		}
	];

	public Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<Season>>(Seasons.ToList());
	}

	public Task<Season?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(Seasons.FirstOrDefault(season => season.Id == id));
	}

	public Task<Season?> GetActiveAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult(Seasons.FirstOrDefault(season => season.IsActive));
	}

	public Task<Season> CreateAsync(Season season, CancellationToken cancellationToken = default)
	{
		if (season.Id == Guid.Empty)
		{
			season.Id = Guid.NewGuid();
		}

		if (season.IsActive)
		{
			foreach (var existingSeason in Seasons)
			{
				existingSeason.IsActive = false;
			}
		}

		season.CreatedAt = DateTime.UtcNow;
		season.UpdatedAt = DateTime.UtcNow;

		Seasons.Add(season);

		return Task.FromResult(season);
	}

	public Task<Season?> UpdateAsync(Season season, CancellationToken cancellationToken = default)
	{
		var existingSeason = Seasons.FirstOrDefault(currentSeason => currentSeason.Id == season.Id);

		if (existingSeason is null)
		{
			return Task.FromResult<Season?>(null);
		}

		var index = Seasons.IndexOf(existingSeason);
		Seasons[index] = season;

		return Task.FromResult<Season?>(season);
	}

	public Task<Season?> SetActiveAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var season = Seasons.FirstOrDefault(currentSeason => currentSeason.Id == id);

		if (season is null)
		{
			return Task.FromResult<Season?>(null);
		}

		foreach (var existingSeason in Seasons)
		{
			existingSeason.IsActive = false;
		}

		season.IsActive = true;
		season.UpdatedAt = DateTime.UtcNow;

		return Task.FromResult<Season?>(season);
	}
}

public sealed class TestStatsService : IStatsService
{
	public Task<List<PlayerSeasonStats>> GetSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(new List<PlayerSeasonStats>());
	}

	public Task<List<PlayerSeasonStats>> GetAllSeasonStatsAsync(
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(new List<PlayerSeasonStats>());
	}

	public Task<List<PlayerSeasonStats>> GetPlayerSeasonStatsAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(new List<PlayerSeasonStats>());
	}

	public Task<List<PlayerHistoricalStats>> GetHistoricalStatsAsync(
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(new List<PlayerHistoricalStats>());
	}

	public Task<PlayerHistoricalStats?> GetHistoricalStatsByPlayerIdAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult<PlayerHistoricalStats?>(null);
	}

	public Task<PlayerHistoricalStats> UpsertHistoricalStatsAsync(
		PlayerHistoricalStats stats,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(stats);
	}

	public Task RecalculateSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		return Task.CompletedTask;
	}
}


public sealed class TestClubPostService : IClubPostService
{
	public List<ClubPost> Posts { get; } = new();

	public Task<IReadOnlyList<ClubPost>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<ClubPost>>(
			Posts
				.OrderByDescending(post => post.IsPinned)
				.ThenByDescending(post => post.CreatedAt)
				.ToList()
		);
	}

	public Task<ClubPost?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(Posts.FirstOrDefault(post => post.Id == id));
	}

	public Task<ClubPost> CreateAsync(
		ClubPost post,
		CancellationToken cancellationToken = default
	)
	{
		if (post.Id == Guid.Empty)
		{
			post.Id = Guid.NewGuid();
		}

		post.Title = post.Title.Trim();
		post.Body = post.Body.Trim();
		post.CreatedByUserEmail = post.CreatedByUserEmail.Trim();
		post.CreatedAt = DateTime.UtcNow;
		post.UpdatedAt = DateTime.UtcNow;

		Posts.Add(post);

		return Task.FromResult(post);
	}

	public Task<ClubPost?> UpdateAsync(
		ClubPost post,
		CancellationToken cancellationToken = default
	)
	{
		var existingPost = Posts.FirstOrDefault(currentPost => currentPost.Id == post.Id);

		if (existingPost is null)
		{
			return Task.FromResult<ClubPost?>(null);
		}

		post.Title = post.Title.Trim();
		post.Body = post.Body.Trim();
		post.CreatedByUserEmail = post.CreatedByUserEmail.Trim();
		post.UpdatedAt = DateTime.UtcNow;

		var index = Posts.IndexOf(existingPost);
		Posts[index] = post;

		return Task.FromResult<ClubPost?>(post);
	}

	public Task<bool> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var existingPost = Posts.FirstOrDefault(currentPost => currentPost.Id == id);

		if (existingPost is null)
		{
			return Task.FromResult(false);
		}

		Posts.Remove(existingPost);

		return Task.FromResult(true);
	}
}


public sealed class TestClubNotificationService : IClubNotificationService
{
	public List<ClubNotification> Notifications { get; } = new();

	public Task<IReadOnlyList<ClubNotification>> GetForUserAsync(
		Guid userId,
		bool unreadOnly = false,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult<IReadOnlyList<ClubNotification>>(
			Notifications
				.Where(notification => notification.Recipients.Any(recipient =>
					recipient.UserId == userId &&
					(!unreadOnly || recipient.Status == NotificationStatus.Unread)))
				.OrderByDescending(notification => notification.CreatedAt)
				.ToList()
		);
	}

	public Task<int> GetUnreadCountAsync(
		Guid userId,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(
			Notifications.Count(notification => notification.Recipients.Any(recipient =>
				recipient.UserId == userId &&
				recipient.Status == NotificationStatus.Unread))
		);
	}

	public Task<ClubNotification> CreateAsync(
		ClubNotification notification,
		CancellationToken cancellationToken = default
	)
	{
		if (notification.Id == Guid.Empty)
		{
			notification.Id = Guid.NewGuid();
		}

		notification.Title = notification.Title.Trim();
		notification.Message = notification.Message.Trim();
		notification.ActionPath = notification.ActionPath.Trim();
		notification.CreatedByUserEmail = notification.CreatedByUserEmail.Trim();
		notification.Recipients ??= [];
		notification.CreatedAt = DateTime.UtcNow;

		Notifications.Add(notification);

		return Task.FromResult(notification);
	}

	public Task<ClubNotification?> MarkReadAsync(
		Guid notificationId,
		Guid userId,
		CancellationToken cancellationToken = default
	)
	{
		var notification = Notifications.FirstOrDefault(currentNotification => currentNotification.Id == notificationId);

		if (notification is null)
		{
			return Task.FromResult<ClubNotification?>(null);
		}

		var recipient = notification.Recipients.FirstOrDefault(currentRecipient => currentRecipient.UserId == userId);

		if (recipient is null)
		{
			return Task.FromResult<ClubNotification?>(null);
		}

		recipient.Status = NotificationStatus.Read;
		recipient.ReadAt ??= DateTime.UtcNow;

		return Task.FromResult<ClubNotification?>(notification);
	}

	public Task<int> MarkAllReadAsync(
		Guid userId,
		CancellationToken cancellationToken = default
	)
	{
		var updatedCount = 0;

		foreach (var notification in Notifications)
		{
			var recipient = notification.Recipients.FirstOrDefault(currentRecipient => currentRecipient.UserId == userId);

			if (recipient is null || recipient.Status == NotificationStatus.Read)
			{
				continue;
			}

			recipient.Status = NotificationStatus.Read;
			recipient.ReadAt = DateTime.UtcNow;
			updatedCount++;
		}

		return Task.FromResult(updatedCount);
	}
}
