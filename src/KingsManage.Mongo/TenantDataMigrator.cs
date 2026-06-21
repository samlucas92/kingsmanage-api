using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo;

public sealed class TenantDataMigrator
{
	private readonly IMongoDatabase _database;

	public TenantDataMigrator(MongoContext context)
	{
		_database = context.Database;
	}

	public async Task RunAsync(CancellationToken cancellationToken = default)
	{
		await EnsureDefaultOrganizationAndClubAsync(cancellationToken);

		await BackfillAsync<Player>("players", cancellationToken);
		await BackfillAsync<Season>("seasons", cancellationToken);
		await BackfillAsync<Match>("matches", cancellationToken);
		await BackfillAsync<ClubEvent>("events", cancellationToken);
		await BackfillAsync<ClubPost>("posts", cancellationToken);
		await BackfillAsync<ClubTeamProfile>("clubTeams", cancellationToken);
		await BackfillAsync<FinanceTransaction>("financeTransactions", cancellationToken);
		await BackfillAsync<ClubFile>("files", cancellationToken);
		await BackfillAsync<ClubNotification>("notifications", cancellationToken);
		await BackfillAsync<MessageThread>("messageThreads", cancellationToken);
		await BackfillAsync<Message>("messages", cancellationToken);
		await BackfillAsync<PlayerSeasonStats>("playerSeasonStats", cancellationToken);
		await BackfillAsync<PlayerHistoricalStats>("playerHistoricalStats", cancellationToken);

		await BackfillUsersAsync(cancellationToken);
	}

	private async Task EnsureDefaultOrganizationAndClubAsync(CancellationToken cancellationToken)
	{
		var organizations = _database.GetCollection<Organization>("organizations");
		var clubs = _database.GetCollection<SportsClub>("clubs");
		var now = DateTime.UtcNow;

		await organizations.UpdateOneAsync(
			organization => organization.Id == DefaultTenant.OrganizationId,
			Builders<Organization>.Update
				.SetOnInsert(organization => organization.Id, DefaultTenant.OrganizationId)
				.SetOnInsert(organization => organization.Name, DefaultTenant.OrganizationName)
				.SetOnInsert(organization => organization.Slug, "kingsbridge-colts")
				.SetOnInsert(organization => organization.IsActive, true)
				.SetOnInsert(organization => organization.CreatedAt, now)
				.SetOnInsert(organization => organization.UpdatedAt, now),
			new UpdateOptions { IsUpsert = true },
			cancellationToken);

		await clubs.UpdateOneAsync(
			club => club.Id == DefaultTenant.ClubId,
			Builders<SportsClub>.Update
				.SetOnInsert(club => club.Id, DefaultTenant.ClubId)
				.SetOnInsert(club => club.OrganizationId, DefaultTenant.OrganizationId)
				.SetOnInsert(club => club.Name, DefaultTenant.ClubName)
				.SetOnInsert(club => club.Slug, "kingsbridge-colts-football")
				.SetOnInsert(club => club.SportKey, "football")
				.SetOnInsert(club => club.IsActive, true)
				.SetOnInsert(club => club.CreatedAt, now)
				.SetOnInsert(club => club.UpdatedAt, now),
			new UpdateOptions { IsUpsert = true },
			cancellationToken);
	}

	private async Task BackfillAsync<T>(string collectionName, CancellationToken cancellationToken)
		where T : ITenantOwned
	{
		var collection = _database.GetCollection<T>(collectionName);
		var filter = Builders<T>.Filter.Or(
			Builders<T>.Filter.Exists(nameof(ITenantOwned.OrganizationId), false),
			Builders<T>.Filter.Eq(item => item.OrganizationId, Guid.Empty),
			Builders<T>.Filter.Exists(nameof(ITenantOwned.ClubId), false),
			Builders<T>.Filter.Eq(item => item.ClubId, Guid.Empty));
		var update = Builders<T>.Update
			.Set(item => item.OrganizationId, DefaultTenant.OrganizationId)
			.Set(item => item.ClubId, DefaultTenant.ClubId);

		await collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
	}

	private async Task BackfillUsersAsync(CancellationToken cancellationToken)
	{
		var users = _database.GetCollection<AppUser>("users");
		var legacyUsers = await users.Find(user =>
			user.DefaultOrganizationId == null ||
			user.DefaultClubId == null ||
			user.Memberships.Count == 0).ToListAsync(cancellationToken);

		foreach (var user in legacyUsers)
		{
			user.DefaultOrganizationId = DefaultTenant.OrganizationId;
			user.DefaultClubId = DefaultTenant.ClubId;
			user.Memberships ??= [];
			user.Memberships.Add(new UserMembership
			{
				OrganizationId = DefaultTenant.OrganizationId,
				ClubId = DefaultTenant.ClubId,
				Role = user.Role switch
				{
					UserRole.Admin => TenantRole.OrganizationAdmin,
					UserRole.Coach => TenantRole.Coach,
					_ => TenantRole.Player
				}
			});

			await users.ReplaceOneAsync(existing => existing.Id == user.Id, user,
				cancellationToken: cancellationToken);
		}
	}
}
