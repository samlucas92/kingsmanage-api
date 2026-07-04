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
		await BackfillAsync<ClubTeamProfile>("clubTeamProfiles", cancellationToken);
		await BackfillAsync<FinanceTransaction>("financeTransactions", cancellationToken);
		await BackfillAsync<ClubFile>("files", cancellationToken);
		await BackfillAsync<ClubNotification>("notifications", cancellationToken);
		await BackfillAsync<MessageThread>("messageThreads", cancellationToken);
		await BackfillAsync<Message>("messages", cancellationToken);
		await BackfillAsync<PlayerSeasonStats>("playerSeasonStats", cancellationToken);
		await BackfillAsync<PlayerHistoricalStats>("playerHistoricalStats", cancellationToken);

		await BackfillUsersAsync(cancellationToken);
		await EnsureTenantIndexesAsync(cancellationToken);
		await EnsureStoredFileObjectIndexesAsync(cancellationToken);
		await EnsureFileLifecycleIndexesAsync(cancellationToken);
		await EnsureBillingIndexesAsync(cancellationToken);
	}

	private async Task EnsureBillingIndexesAsync(CancellationToken cancellationToken)
	{
		var subscriptions = _database.GetCollection<OrganizationSubscription>("organizationSubscriptions");
		await subscriptions.Indexes.CreateOneAsync(
			new CreateIndexModel<OrganizationSubscription>(
				Builders<OrganizationSubscription>.IndexKeys.Ascending(item => item.OrganizationId),
				new CreateIndexOptions { Name = "OrganizationId_1", Unique = true }),
			cancellationToken: cancellationToken);
		var invoices = _database.GetCollection<BillingInvoice>("billingInvoices");
		await invoices.Indexes.CreateOneAsync(
			new CreateIndexModel<BillingInvoice>(
				Builders<BillingInvoice>.IndexKeys
					.Ascending(item => item.OrganizationId)
					.Descending(item => item.IssuedAt),
				new CreateIndexOptions { Name = "OrganizationIssuedAt_1" }),
			cancellationToken: cancellationToken);
	}

	private async Task EnsureFileLifecycleIndexesAsync(CancellationToken cancellationToken)
	{
		var objects = _database.GetCollection<StoredFileObject>("storedFileObjects");
		var files = _database.GetCollection<ClubFile>("files");
		var audit = _database.GetCollection<FileLifecycleAudit>("fileLifecycleAudit");

		await objects.Indexes.CreateOneAsync(
			new CreateIndexModel<StoredFileObject>(
				Builders<StoredFileObject>.IndexKeys
					.Ascending(item => item.Status)
					.Ascending(item => item.ReferenceCount)
					.Ascending(item => item.OrphanedAt),
				new CreateIndexOptions { Name = "LifecycleCleanup_1" }
			),
			cancellationToken: cancellationToken
		);
		await files.Indexes.CreateOneAsync(
			new CreateIndexModel<ClubFile>(
				Builders<ClubFile>.IndexKeys
					.Ascending(item => item.Status)
					.Ascending(item => item.CreatedAt)
					.Ascending(item => item.QuarantinedAt),
				new CreateIndexOptions { Name = "UploadExpiry_1" }
			),
			cancellationToken: cancellationToken
		);
		await audit.Indexes.CreateOneAsync(
			new CreateIndexModel<FileLifecycleAudit>(
				Builders<FileLifecycleAudit>.IndexKeys
					.Ascending(item => item.OrganizationId)
					.Descending(item => item.CreatedAt),
				new CreateIndexOptions { Name = "OrganizationCreatedAt_1" }
			),
			cancellationToken: cancellationToken
		);
	}

	private async Task EnsureStoredFileObjectIndexesAsync(CancellationToken cancellationToken)
	{
		var objects = _database.GetCollection<StoredFileObject>("storedFileObjects");
		var keys = Builders<StoredFileObject>.IndexKeys
			.Ascending(item => item.OrganizationId)
			.Ascending(item => item.ContentHash);

		await objects.Indexes.CreateOneAsync(
			new CreateIndexModel<StoredFileObject>(
				keys,
				new CreateIndexOptions
				{
					Name = "OrganizationContentHash_1",
					Unique = true
				}
			),
			cancellationToken: cancellationToken
		);
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
				ClubId = user.Role == UserRole.Admin ? null : DefaultTenant.ClubId,
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

	private async Task EnsureTenantIndexesAsync(CancellationToken cancellationToken)
	{
		var organizations = _database.GetCollection<Organization>("organizations");
		await organizations.Indexes.CreateOneAsync(
			new CreateIndexModel<Organization>(
				Builders<Organization>.IndexKeys.Ascending(organization => organization.Slug),
				new CreateIndexOptions { Name = "Slug_1", Unique = true }),
			cancellationToken: cancellationToken);

		var clubs = _database.GetCollection<SportsClub>("clubs");
		await clubs.Indexes.CreateOneAsync(
			new CreateIndexModel<SportsClub>(
				Builders<SportsClub>.IndexKeys
					.Ascending(club => club.OrganizationId)
					.Ascending(club => club.Slug),
				new CreateIndexOptions { Name = "OrganizationSlug_1", Unique = true }),
			cancellationToken: cancellationToken);

		await EnsureTenantIndexAsync<Player>("players", cancellationToken);
		await EnsureTenantIndexAsync<Season>("seasons", cancellationToken);
		await EnsureTenantIndexAsync<Match>("matches", cancellationToken);
		await EnsureTenantIndexAsync<ClubEvent>("events", cancellationToken);
		await EnsureTenantIndexAsync<ClubPost>("posts", cancellationToken);
		await EnsureTenantIndexAsync<ClubTeamProfile>("clubTeamProfiles", cancellationToken);
		await EnsureTenantIndexAsync<FinanceTransaction>("financeTransactions", cancellationToken);
		await EnsureTenantIndexAsync<ClubFile>("files", cancellationToken);
		await EnsureTenantIndexAsync<ClubNotification>("notifications", cancellationToken);
		await EnsureTenantIndexAsync<MessageThread>("messageThreads", cancellationToken);
		await EnsureTenantIndexAsync<Message>("messages", cancellationToken);
		await EnsureTenantIndexAsync<PlayerSeasonStats>("playerSeasonStats", cancellationToken);
		await EnsureTenantIndexAsync<PlayerHistoricalStats>("playerHistoricalStats", cancellationToken);
	}

	private async Task EnsureTenantIndexAsync<T>(string collectionName, CancellationToken cancellationToken)
		where T : ITenantOwned
	{
		var collection = _database.GetCollection<T>(collectionName);
		var keys = Builders<T>.IndexKeys
			.Ascending(item => item.OrganizationId)
			.Ascending(item => item.ClubId);

		await collection.Indexes.CreateOneAsync(
			new CreateIndexModel<T>(keys, new CreateIndexOptions { Name = "TenantScope_1" }),
			cancellationToken: cancellationToken);
	}
}
