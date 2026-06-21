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
	private readonly ITenantContext _tenantContext;

	public UserService(MongoContext context, ITenantContext tenantContext)
	{
		_users = context.Database.GetCollection<AppUser>("users");
		_tenantContext = tenantContext;
	}

	public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return await _users
			.Find(GetTenantFilter())
			.SortBy(user => user.Email)
			.ToListAsync(cancellationToken);
	}

	public async Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return await _users
			.Find(GetTenantFilter() & Builders<AppUser>.Filter.Eq(user => user.Id, id))
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
		EnsureMembership(user);
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

		if (existingUser.IsActive && !user.IsActive)
		{
			await EnsureOrganizationAdminCanBeDeactivatedAsync(existingUser, cancellationToken);
		}

		user.Email = NormaliseEmail(user.Email);
		EnsureMembership(user);
		user.PasswordHash = existingUser.PasswordHash;
		user.IsPlatformAdmin = existingUser.IsPlatformAdmin;
		user.DefaultOrganizationId = existingUser.DefaultOrganizationId;
		user.DefaultClubId = existingUser.DefaultClubId;
		user.Memberships = existingUser.Memberships;
		user.CreatedAt = existingUser.CreatedAt;
		user.LastLoginAt = existingUser.LastLoginAt;
		user.UpdatedAt = DateTime.UtcNow;

		var result = await _users.ReplaceOneAsync(
			GetTenantFilter() & Builders<AppUser>.Filter.Eq(existing => existing.Id, user.Id),
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
		if (!isActive)
		{
			var user = await GetByIdAsync(id, cancellationToken);
			if (user is not null) await EnsureOrganizationAdminCanBeDeactivatedAsync(user, cancellationToken);
		}

		var update = Builders<AppUser>.Update
			.Set(user => user.IsActive, isActive)
			.Set(user => user.UpdatedAt, DateTime.UtcNow);

		return await _users.FindOneAndUpdateAsync(
			GetTenantFilter() & Builders<AppUser>.Filter.Eq(user => user.Id, id),
			update,
			new FindOneAndUpdateOptions<AppUser> { ReturnDocument = ReturnDocument.After },
			cancellationToken
		);
	}

	private async Task EnsureOrganizationAdminCanBeDeactivatedAsync(AppUser user, CancellationToken cancellationToken)
	{
		var isOrganizationAdmin = user.Memberships.Any(membership =>
			membership.OrganizationId == _tenantContext.OrganizationId &&
			membership.Role == TenantRole.OrganizationAdmin);
		if (!isOrganizationAdmin) return;

		var activeAdminCount = await _users.CountDocumentsAsync(existing =>
			existing.IsActive && existing.Memberships.Any(membership =>
				membership.OrganizationId == _tenantContext.OrganizationId &&
				membership.Role == TenantRole.OrganizationAdmin), cancellationToken: cancellationToken);
		if (activeAdminCount <= 1) throw new InvalidOperationException("The final Organization Admin cannot be deactivated.");
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

		EnsureMembership(user);
		update = update
			.Set(existingUser => existingUser.DefaultOrganizationId, user.DefaultOrganizationId)
			.Set(existingUser => existingUser.DefaultClubId, user.DefaultClubId)
			.Set(existingUser => existingUser.Memberships, user.Memberships);

		return await _users.FindOneAndUpdateAsync(
			existingUser => existingUser.Id == user.Id,
			update,
			new FindOneAndUpdateOptions<AppUser> { ReturnDocument = ReturnDocument.After },
			cancellationToken
		);
	}

	public async Task<AppUser?> SetDefaultClubAsync(
		Guid id,
		Guid clubId,
		CancellationToken cancellationToken = default
	)
	{
		var user = await GetByIdAsync(id, cancellationToken);

		if (user is null || !user.IsActive)
		{
			return null;
		}

		var canAccessClub = user.IsPlatformAdmin || user.Memberships.Any(membership =>
			membership.OrganizationId == _tenantContext.OrganizationId &&
			(membership.ClubId == clubId ||
				(membership.ClubId == null && membership.Role == TenantRole.OrganizationAdmin)));

		if (!canAccessClub)
		{
			return null;
		}

		var update = Builders<AppUser>.Update
			.Set(existingUser => existingUser.DefaultOrganizationId, _tenantContext.OrganizationId)
			.Set(existingUser => existingUser.DefaultClubId, clubId)
			.Set(existingUser => existingUser.UpdatedAt, DateTime.UtcNow);

		return await _users.FindOneAndUpdateAsync(
			existingUser => existingUser.Id == id,
			update,
			new FindOneAndUpdateOptions<AppUser> { ReturnDocument = ReturnDocument.After },
			cancellationToken);
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
			GetTenantFilter() & Builders<AppUser>.Filter.Eq(existingUser => existingUser.Id, id),
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
			GetTenantFilter() & Builders<AppUser>.Filter.Eq(existingUser => existingUser.Id, id),
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
			IsPlatformAdmin = true,
			IsActive = true
		};

		return await CreateAsync(adminUser, password, cancellationToken);
	}

	private static string NormaliseEmail(string email)
	{
		return email.Trim().ToLowerInvariant();
	}

	private FilterDefinition<AppUser> GetTenantFilter()
	{
		if (!_tenantContext.IsAvailable)
		{
			throw new InvalidOperationException("A tenant context is required for user management.");
		}

		return Builders<AppUser>.Filter.ElemMatch(
			user => user.Memberships,
			membership =>
				membership.OrganizationId == _tenantContext.OrganizationId &&
				(membership.ClubId == null || membership.ClubId == _tenantContext.ClubId));
	}

	private void EnsureMembership(AppUser user)
	{
		user.Memberships ??= [];

		if (user.Memberships.Count > 0)
		{
			user.DefaultOrganizationId ??= user.Memberships[0].OrganizationId;
			user.DefaultClubId ??= user.Memberships
				.Select(membership => membership.ClubId)
				.FirstOrDefault(clubId => clubId.HasValue) ?? DefaultTenant.ClubId;
			return;
		}

		var organizationId = _tenantContext.IsAvailable
			? _tenantContext.OrganizationId
			: DefaultTenant.OrganizationId;
		var clubId = _tenantContext.IsAvailable
			? _tenantContext.ClubId
			: DefaultTenant.ClubId;

		user.DefaultOrganizationId = organizationId;
		user.DefaultClubId = clubId;

		user.Memberships.Add(new UserMembership
		{
			OrganizationId = organizationId,
			ClubId = user.Role == UserRole.Admin ? null : clubId,
			Role = user.Role switch
			{
				UserRole.Admin => TenantRole.OrganizationAdmin,
				UserRole.Coach => TenantRole.Coach,
				_ => TenantRole.Player
			}
		});
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
