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
	public TestStatsService StatsService { get; } = new();

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
				["DefaultAdmin:Password"] = "DefaultAdmin123!"
			};

			configurationBuilder.AddInMemoryCollection(testConfiguration);
		});

		builder.ConfigureTestServices(services =>
		{
			services.RemoveAll<IUserService>();
			services.RemoveAll<IPlayerService>();
			services.RemoveAll<IStatsService>();

			services.AddSingleton<IUserService>(UserService);
			services.AddSingleton<IPlayerService>(PlayerService);
			services.AddSingleton<IStatsService>(StatsService);
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
