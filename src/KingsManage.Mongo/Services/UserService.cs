using System.Security.Cryptography;
using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class UserService : IUserService
{
	private const int PasswordSaltBytes = 16;
	private const int PasswordHashBytes = 32;
	private const int PasswordIterations = 210_000;

	private readonly IMongoCollection<AppUser> _users;

	public UserService(MongoContext context)
	{
		_users = context.Database.GetCollection<AppUser>("users");
	}

	public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return await _users
			.Find(_ => true)
			.SortBy(user => user.Email)
			.ToListAsync(cancellationToken);
	}

	public async Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return await _users
			.Find(user => user.Id == id)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
	{
		var normalisedEmail = NormaliseEmail(email);

		return await _users
			.Find(user => user.Email == normalisedEmail)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<AppUser> CreateAsync(
		AppUser user,
		string password,
		CancellationToken cancellationToken = default
	)
	{
		if (string.IsNullOrWhiteSpace(password))
		{
			throw new ArgumentException("Password is required.", nameof(password));
		}

		user.Id = user.Id == Guid.Empty ? Guid.NewGuid() : user.Id;
		user.Email = NormaliseEmail(user.Email);
		user.PasswordHash = HashPassword(password);
		user.CreatedAt = DateTime.UtcNow;
		user.UpdatedAt = DateTime.UtcNow;

		await _users.InsertOneAsync(user, cancellationToken: cancellationToken);

		return user;
	}

	public async Task<AppUser?> UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
	{
		var existingUser = await GetByIdAsync(user.Id, cancellationToken);

		if (existingUser is null)
		{
			return null;
		}

		user.Email = NormaliseEmail(user.Email);
		user.PasswordHash = existingUser.PasswordHash;
		user.CreatedAt = existingUser.CreatedAt;
		user.LastLoginAt = existingUser.LastLoginAt;
		user.UpdatedAt = DateTime.UtcNow;

		var result = await _users.ReplaceOneAsync(
			existing => existing.Id == user.Id,
			user,
			cancellationToken: cancellationToken
		);

		if (result.MatchedCount == 0)
		{
			return null;
		}

		return user;
	}

	public async Task<AppUser?> SetActiveAsync(
		Guid id,
		bool isActive,
		CancellationToken cancellationToken = default
	)
	{
		var update = Builders<AppUser>.Update
			.Set(user => user.IsActive, isActive)
			.Set(user => user.UpdatedAt, DateTime.UtcNow);

		return await _users.FindOneAndUpdateAsync(
			user => user.Id == id,
			update,
			new FindOneAndUpdateOptions<AppUser> { ReturnDocument = ReturnDocument.After },
			cancellationToken
		);
	}

	public async Task<AppUser?> ValidateCredentialsAsync(
		string email,
		string password,
		CancellationToken cancellationToken = default
	)
	{
		var user = await GetByEmailAsync(email, cancellationToken);

		if (user is null || !user.IsActive || !VerifyPassword(password, user.PasswordHash))
		{
			return null;
		}

		var update = Builders<AppUser>.Update
			.Set(existingUser => existingUser.LastLoginAt, DateTime.UtcNow)
			.Set(existingUser => existingUser.UpdatedAt, DateTime.UtcNow);

		return await _users.FindOneAndUpdateAsync(
			existingUser => existingUser.Id == user.Id,
			update,
			new FindOneAndUpdateOptions<AppUser> { ReturnDocument = ReturnDocument.After },
			cancellationToken
		);
	}

	public async Task<bool> ChangePasswordAsync(
		Guid id,
		string currentPassword,
		string newPassword,
		CancellationToken cancellationToken = default
	)
	{
		if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
		{
			return false;
		}

		var user = await GetByIdAsync(id, cancellationToken);

		if (user is null || !user.IsActive || !VerifyPassword(currentPassword, user.PasswordHash))
		{
			return false;
		}

		var update = Builders<AppUser>.Update
			.Set(existingUser => existingUser.PasswordHash, HashPassword(newPassword))
			.Set(existingUser => existingUser.UpdatedAt, DateTime.UtcNow);

		var result = await _users.UpdateOneAsync(
			existingUser => existingUser.Id == id,
			update,
			cancellationToken: cancellationToken
		);

		return result.ModifiedCount == 1;
	}

	public async Task<bool> ResetPasswordAsync(
		Guid id,
		string newPassword,
		CancellationToken cancellationToken = default
	)
	{
		if (string.IsNullOrWhiteSpace(newPassword))
		{
			return false;
		}

		var update = Builders<AppUser>.Update
			.Set(existingUser => existingUser.PasswordHash, HashPassword(newPassword))
			.Set(existingUser => existingUser.UpdatedAt, DateTime.UtcNow);

		var result = await _users.UpdateOneAsync(
			existingUser => existingUser.Id == id,
			update,
			cancellationToken: cancellationToken
		);

		return result.ModifiedCount == 1;
	}

	public async Task<AppUser> EnsureDefaultAdminUserAsync(
		string email,
		string password,
		CancellationToken cancellationToken = default
	)
	{
		var existingUsers = await _users
			.Find(_ => true)
			.Limit(1)
			.ToListAsync(cancellationToken);

		if (existingUsers.Count > 0)
		{
			return existingUsers[0];
		}

		var adminUser = new AppUser
		{
			Email = email,
			Role = UserRole.Admin,
			IsActive = true
		};

		return await CreateAsync(adminUser, password, cancellationToken);
	}

	private static string NormaliseEmail(string email)
	{
		return email.Trim().ToLowerInvariant();
	}

	private static string HashPassword(string password)
	{
		var salt = RandomNumberGenerator.GetBytes(PasswordSaltBytes);

		var hash = Rfc2898DeriveBytes.Pbkdf2(
			password,
			salt,
			PasswordIterations,
			HashAlgorithmName.SHA256,
			PasswordHashBytes
		);

		return string.Join(
			':',
			"pbkdf2-sha256",
			PasswordIterations,
			Convert.ToBase64String(salt),
			Convert.ToBase64String(hash)
		);
	}

	private static bool VerifyPassword(string password, string storedPasswordHash)
	{
		var parts = storedPasswordHash.Split(':');

		if (parts.Length != 4 || parts[0] != "pbkdf2-sha256")
		{
			return false;
		}

		if (!int.TryParse(parts[1], out var iterations))
		{
			return false;
		}

		var salt = Convert.FromBase64String(parts[2]);
		var expectedHash = Convert.FromBase64String(parts[3]);

		var actualHash = Rfc2898DeriveBytes.Pbkdf2(
			password,
			salt,
			iterations,
			HashAlgorithmName.SHA256,
			expectedHash.Length
		);

		return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
	}
}
