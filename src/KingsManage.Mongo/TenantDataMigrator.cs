using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo;

public sealed class TenantDataMigrator
{
	private readonly IMongoDatabase database;

	public TenantDataMigrator(MongoContext context)
	{
		database = context.Database;
	}

	public async Task RunAsync(CancellationToken cancellationToken = default)
	{
		await EnsureDefaultOrganizationAndClubAsync(cancellationToken);

		await BackfillAsync<Player>("players", cancellationToken);
		await BackfillAsync<Season>("seasons", cancellationToken);
		await BackfillAsync<Match>("matches", cancellationToken);
		await BackfillAsync<ClubEvent>("events", cancellationToken);
		await BackfillAsync<ClubPost>("posts", cancellationToken);
		await BackfillAsync<ClubForm>("forms", cancellationToken);
		await BackfillAsync<ClubFormSubmission>("formSubmissions", cancellationToken);
		await BackfillAsync<ClubTeamProfile>("clubTeamProfiles", cancellationToken);
		await BackfillAsync<FinanceTransaction>("financeTransactions", cancellationToken);
		await BackfillAsync<ClubFile>("files", cancellationToken);
		await BackfillAsync<ClubNotification>("notifications", cancellationToken);
		await BackfillAsync<MessageThread>("messageThreads", cancellationToken);
		await BackfillAsync<Message>("messages", cancellationToken);
		await BackfillAsync<PlayerSeasonStats>("playerSeasonStats", cancellationToken);
		await BackfillAsync<PlayerHistoricalStats>("playerHistoricalStats", cancellationToken);
		await BackfillAsync<TrainingAssessment>("trainingAssessments", cancellationToken);

		await BackfillUsersAsync(cancellationToken);
		await EnsureTenantIndexesAsync(cancellationToken);
		await EnsureReadModelIndexesAsync(cancellationToken);
		await EnsureFormIndexesAsync(cancellationToken);
		await EnsureFormAnalyticsIndexesAsync(cancellationToken);
		await EnsureStoredFileObjectIndexesAsync(cancellationToken);
		await EnsureFileLifecycleIndexesAsync(cancellationToken);
		await EnsureBillingIndexesAsync(cancellationToken);
		await EnsureSocialGraphicTemplateIndexesAsync(cancellationToken);
		await EnsureHandoverVaultIndexesAsync(cancellationToken);
	}

	private async Task EnsureHandoverVaultIndexesAsync(CancellationToken cancellationToken)
	{
		var roles = database.GetCollection<OperationalRole>("operationalRoles");
		await roles.Indexes.CreateManyAsync([
			new CreateIndexModel<OperationalRole>(
				Builders<OperationalRole>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.IsActive).Ascending(item => item.DisplayOrder),
				new CreateIndexOptions { Name = "OrganizationActiveOrder_1" }),
			new CreateIndexModel<OperationalRole>(
				Builders<OperationalRole>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.PrimaryOwnerUserId),
				new CreateIndexOptions { Name = "OrganizationPrimaryOwner_1" })
		], cancellationToken);

		var responsibilities = database.GetCollection<RoleResponsibility>("roleResponsibilities");
		await responsibilities.Indexes.CreateOneAsync(new CreateIndexModel<RoleResponsibility>(
			Builders<RoleResponsibility>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.OperationalRoleId).Ascending(item => item.IsActive),
			new CreateIndexOptions { Name = "OrganizationRoleActive_1" }), cancellationToken: cancellationToken);

		var links = database.GetCollection<HandoverDocumentLink>("handoverDocumentLinks");
		await links.Indexes.CreateManyAsync([
			new CreateIndexModel<HandoverDocumentLink>(Builders<HandoverDocumentLink>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.OperationalRoleId).Ascending(item => item.DisplayOrder), new CreateIndexOptions { Name = "OrganizationRoleOrder_1" }),
			new CreateIndexModel<HandoverDocumentLink>(Builders<HandoverDocumentLink>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.ResponsibilityId).Ascending(item => item.DisplayOrder), new CreateIndexOptions { Name = "OrganizationResponsibilityOrder_1" }),
			new CreateIndexModel<HandoverDocumentLink>(Builders<HandoverDocumentLink>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.OrganizationDocumentId), new CreateIndexOptions { Name = "OrganizationDocument_1" })
		], cancellationToken);

		var tasks = database.GetCollection<OperationalTask>("operationalTasks");
		await tasks.Indexes.CreateManyAsync([
			new CreateIndexModel<OperationalTask>(Builders<OperationalTask>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.Status).Ascending(item => item.DueAt), new CreateIndexOptions { Name = "OrganizationStatusDueAt_1" }),
			new CreateIndexModel<OperationalTask>(Builders<OperationalTask>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.AssignedUserIds).Ascending(item => item.Status), new CreateIndexOptions { Name = "OrganizationAssigneesStatus_1" }),
			new CreateIndexModel<OperationalTask>(Builders<OperationalTask>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.RecurrenceSourceTaskId), new CreateIndexOptions<OperationalTask> { Name = "OrganizationRecurrenceSource_1", Unique = true, Sparse = true })
		], cancellationToken);

		var contacts = database.GetCollection<OperationalContact>("operationalContacts");
		await contacts.Indexes.CreateOneAsync(new CreateIndexModel<OperationalContact>(Builders<OperationalContact>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.OperationalRoleId).Ascending(item => item.IsActive), new CreateIndexOptions { Name = "OrganizationRoleActive_1" }), cancellationToken: cancellationToken);

		var handovers = database.GetCollection<HandoverRecord>("handoverRecords");
		await handovers.Indexes.CreateManyAsync([
			new CreateIndexModel<HandoverRecord>(Builders<HandoverRecord>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.Status).Descending(item => item.StartedAt), new CreateIndexOptions { Name = "OrganizationStatusStartedAt_1" }),
			new CreateIndexModel<HandoverRecord>(Builders<HandoverRecord>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.OutgoingUserId).Ascending(item => item.IncomingUserId), new CreateIndexOptions { Name = "OrganizationParticipants_1" })
		], cancellationToken);

		var audit = database.GetCollection<HandoverAuditEntry>("handoverAudit");
		await audit.Indexes.CreateOneAsync(new CreateIndexModel<HandoverAuditEntry>(Builders<HandoverAuditEntry>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.EntityId).Descending(item => item.OccurredAt), new CreateIndexOptions { Name = "OrganizationEntityOccurredAt_1" }), cancellationToken: cancellationToken);

		var posts = database.GetCollection<ClubPost>("posts");
		await posts.Indexes.CreateOneAsync(new CreateIndexModel<ClubPost>(Builders<ClubPost>.IndexKeys.Ascending(item => item.OrganizationId).Ascending(item => item.Type).Ascending(item => item.IsArchived).Descending(item => item.UpdatedAt), new CreateIndexOptions { Name = "OrganizationTypeArchivedUpdatedAt_1" }), cancellationToken: cancellationToken);
	}

	private async Task EnsureSocialGraphicTemplateIndexesAsync(
		CancellationToken cancellationToken
	)
	{
		var customizations = database.GetCollection<SocialGraphicTemplateCustomization>(
			"socialGraphicTemplates"
		);
		await customizations.Indexes.CreateOneAsync(
			new CreateIndexModel<SocialGraphicTemplateCustomization>(
				Builders<SocialGraphicTemplateCustomization>.IndexKeys
					.Ascending(item => item.OrganizationId)
					.Ascending(item => item.ClubId)
					.Ascending(item => item.TemplateId),
				new CreateIndexOptions
				{
					Name = "TenantTemplate_1",
					Unique = true
				}
			),
			cancellationToken: cancellationToken
		);

		var revisions = database.GetCollection<SocialGraphicTemplateRevision>(
			"socialGraphicTemplateRevisions"
		);
		await revisions.Indexes.CreateOneAsync(
			new CreateIndexModel<SocialGraphicTemplateRevision>(
				Builders<SocialGraphicTemplateRevision>.IndexKeys
					.Ascending(item => item.OrganizationId)
					.Ascending(item => item.ClubId)
					.Ascending(item => item.TemplateId)
					.Ascending(item => item.Revision),
				new CreateIndexOptions
				{
					Name = "TenantTemplateRevision_1",
					Unique = true
				}
			),
			cancellationToken: cancellationToken
		);
	}

	private async Task EnsureFormIndexesAsync(CancellationToken cancellationToken)
	{
		var forms = database.GetCollection<ClubForm>("forms");
		await forms.Indexes.CreateOneAsync(
			new CreateIndexModel<ClubForm>(
				Builders<ClubForm>.IndexKeys
					.Ascending(form => form.OrganizationId)
					.Ascending(form => form.ClubId)
					.Ascending(form => form.Status)
					.Descending(form => form.UpdatedAt),
				new CreateIndexOptions { Name = "TenantStatusUpdatedAt_1" }),
			cancellationToken: cancellationToken);

		await forms.Indexes.CreateOneAsync(
			new CreateIndexModel<ClubForm>(
				Builders<ClubForm>.IndexKeys.Ascending(form => form.GoCode),
				new CreateIndexOptions { Name = "GoCode_1" }),
			cancellationToken: cancellationToken);

		await forms.Indexes.CreateOneAsync(
			new CreateIndexModel<ClubForm>(
				Builders<ClubForm>.IndexKeys
					.Ascending(form => form.OrganizationId)
					.Ascending(form => form.ClubId)
					.Ascending(form => form.SourceType)
					.Ascending(form => form.SourceMatchId),
				new CreateIndexOptions { Name = "TenantSourceMatch_1" }),
			cancellationToken: cancellationToken);

		var submissions = database.GetCollection<ClubFormSubmission>("formSubmissions");
		try
		{
			await submissions.Indexes.DropOneAsync("TenantFormUser_1", cancellationToken);
		}
		catch (MongoCommandException exception) when (exception.CodeName == "IndexNotFound")
		{
		}

		await submissions.Indexes.CreateOneAsync(
			new CreateIndexModel<ClubFormSubmission>(
				Builders<ClubFormSubmission>.IndexKeys
					.Ascending(submission => submission.OrganizationId)
					.Ascending(submission => submission.ClubId)
					.Ascending(submission => submission.FormId)
					.Ascending(submission => submission.RespondentKey),
				new CreateIndexOptions { Name = "TenantFormRespondent_1" }),
			cancellationToken: cancellationToken);

		await submissions.Indexes.CreateOneAsync(
			new CreateIndexModel<ClubFormSubmission>(
				Builders<ClubFormSubmission>.IndexKeys
					.Ascending(submission => submission.OrganizationId)
					.Ascending(submission => submission.ClubId)
					.Ascending(submission => submission.FormId)
					.Ascending(submission => submission.SubmissionLimitKey),
				new CreateIndexOptions { Name = "TenantFormSubmissionLimit_1", Unique = true }),
			cancellationToken: cancellationToken);

		await submissions.Indexes.CreateOneAsync(
			new CreateIndexModel<ClubFormSubmission>(
				Builders<ClubFormSubmission>.IndexKeys
					.Ascending(submission => submission.OrganizationId)
					.Ascending(submission => submission.ClubId)
					.Ascending(submission => submission.FormId)
					.Descending(submission => submission.SubmittedAt),
				new CreateIndexOptions { Name = "TenantFormSubmittedAt_1" }),
			cancellationToken: cancellationToken);
	}

	private async Task EnsureFormAnalyticsIndexesAsync(CancellationToken cancellationToken)
	{
		var sessions = database.GetCollection<FormAnalyticsSession>("formAnalyticsSessions");
		await sessions.Indexes.CreateManyAsync(
			[
				new CreateIndexModel<FormAnalyticsSession>(
					Builders<FormAnalyticsSession>.IndexKeys
						.Ascending(item => item.OrganizationId)
						.Ascending(item => item.ClubId)
						.Ascending(item => item.FormId)
						.Ascending(item => item.Id),
					new CreateIndexOptions { Name = "TenantFormSession_1", Unique = true }),
				new CreateIndexModel<FormAnalyticsSession>(
					Builders<FormAnalyticsSession>.IndexKeys
						.Ascending(item => item.OrganizationId)
						.Ascending(item => item.ClubId)
						.Ascending(item => item.FormId)
						.Descending(item => item.StartedAt),
					new CreateIndexOptions { Name = "TenantFormStartedAt_1" })
			],
			cancellationToken);

		var events = database.GetCollection<FormAnalyticsEvent>("formAnalyticsEvents");
		await events.Indexes.CreateManyAsync(
			[
				new CreateIndexModel<FormAnalyticsEvent>(
					Builders<FormAnalyticsEvent>.IndexKeys
						.Ascending(item => item.OrganizationId)
						.Ascending(item => item.ClubId)
						.Ascending(item => item.FormId)
						.Descending(item => item.OccurredAt),
					new CreateIndexOptions { Name = "TenantFormOccurredAt_1" }),
				new CreateIndexModel<FormAnalyticsEvent>(
					Builders<FormAnalyticsEvent>.IndexKeys
						.Ascending(item => item.OrganizationId)
						.Ascending(item => item.ClubId)
						.Ascending(item => item.FormId)
						.Ascending(item => item.SessionId)
						.Ascending(item => item.EventType)
						.Ascending(item => item.FieldId),
					new CreateIndexOptions { Name = "TenantFormSessionEvent_1" }),
				CreateUniqueAnalyticsEventIndex("TenantFormViewedSession_1", FormAnalyticsEventType.Viewed, includeField: false),
				CreateUniqueAnalyticsEventIndex("TenantFormStartedSession_1", FormAnalyticsEventType.InteractionStarted, includeField: false),
				CreateUniqueAnalyticsEventIndex("TenantFormSubmittedSession_1", FormAnalyticsEventType.Submitted, includeField: false),
				CreateUniqueAnalyticsEventIndex("TenantFormFieldSession_1", FormAnalyticsEventType.FieldInteracted, includeField: true)
			],
			cancellationToken);
	}

	private static CreateIndexModel<FormAnalyticsEvent> CreateUniqueAnalyticsEventIndex(
		string name,
		FormAnalyticsEventType eventType,
		bool includeField)
	{
		var keys = Builders<FormAnalyticsEvent>.IndexKeys
			.Ascending(item => item.OrganizationId)
			.Ascending(item => item.ClubId)
			.Ascending(item => item.FormId)
			.Ascending(item => item.SessionId)
			.Ascending(item => item.EventType);
		if (includeField) keys = keys.Ascending(item => item.FieldId);
		return new CreateIndexModel<FormAnalyticsEvent>(keys, new CreateIndexOptions<FormAnalyticsEvent>
		{
			Name = name,
			Unique = true,
			PartialFilterExpression = Builders<FormAnalyticsEvent>.Filter.Eq(item => item.EventType, eventType)
		});
	}

	private async Task EnsureReadModelIndexesAsync(CancellationToken cancellationToken)
	{
		var matches = database.GetCollection<Match>("matches");
		await matches.Indexes.CreateOneAsync(
			new CreateIndexModel<Match>(
				Builders<Match>.IndexKeys
					.Ascending(match => match.OrganizationId)
					.Ascending(match => match.ClubId)
					.Ascending(match => match.SeasonId)
					.Descending(match => match.Date),
				new CreateIndexOptions { Name = "TenantSeasonDate_1" }),
			cancellationToken: cancellationToken);

		await matches.Indexes.CreateOneAsync(
			new CreateIndexModel<Match>(
				Builders<Match>.IndexKeys
					.Ascending(match => match.OrganizationId)
					.Ascending(match => match.ClubId)
					.Ascending(match => match.IsCompleted)
					.Ascending(match => match.State)
					.Ascending(match => match.Date),
				new CreateIndexOptions { Name = "TenantCompletedStateDate_1" }),
			cancellationToken: cancellationToken);

		var events = database.GetCollection<ClubEvent>("events");
		await events.Indexes.CreateOneAsync(
			new CreateIndexModel<ClubEvent>(
				Builders<ClubEvent>.IndexKeys
					.Ascending(clubEvent => clubEvent.OrganizationId)
					.Ascending(clubEvent => clubEvent.ClubId)
					.Ascending(clubEvent => clubEvent.Type)
					.Ascending(clubEvent => clubEvent.StartDateTime),
				new CreateIndexOptions { Name = "TenantTypeStartDate_1" }),
			cancellationToken: cancellationToken);

		var financeTransactions = database.GetCollection<FinanceTransaction>("financeTransactions");
		await financeTransactions.Indexes.CreateOneAsync(
			new CreateIndexModel<FinanceTransaction>(
				Builders<FinanceTransaction>.IndexKeys
					.Ascending(transaction => transaction.OrganizationId)
					.Ascending(transaction => transaction.ClubId)
					.Ascending(transaction => transaction.SeasonId)
					.Descending(transaction => transaction.TransactionDate),
				new CreateIndexOptions { Name = "TenantSeasonTransactionDate_1" }),
			cancellationToken: cancellationToken);

		var playerSeasonStats = database.GetCollection<PlayerSeasonStats>("playerSeasonStats");
		await playerSeasonStats.Indexes.CreateOneAsync(
			new CreateIndexModel<PlayerSeasonStats>(
				Builders<PlayerSeasonStats>.IndexKeys
					.Ascending(stats => stats.OrganizationId)
					.Ascending(stats => stats.ClubId)
					.Ascending(stats => stats.SeasonId)
					.Ascending(stats => stats.PlayerId),
				new CreateIndexOptions { Name = "TenantSeasonPlayer_1" }),
			cancellationToken: cancellationToken);

		var trainingAssessments = database.GetCollection<TrainingAssessment>("trainingAssessments");
		await trainingAssessments.Indexes.CreateOneAsync(
			new CreateIndexModel<TrainingAssessment>(
				Builders<TrainingAssessment>.IndexKeys
					.Ascending(assessment => assessment.OrganizationId)
					.Ascending(assessment => assessment.ClubId)
					.Ascending(assessment => assessment.EventId)
					.Ascending(assessment => assessment.PlayerId),
				new CreateIndexOptions { Name = "TenantEventPlayer_1", Unique = true }),
			cancellationToken: cancellationToken);

		await trainingAssessments.Indexes.CreateOneAsync(
			new CreateIndexModel<TrainingAssessment>(
				Builders<TrainingAssessment>.IndexKeys
					.Ascending(assessment => assessment.OrganizationId)
					.Ascending(assessment => assessment.ClubId)
					.Ascending(assessment => assessment.PlayerId)
					.Descending(assessment => assessment.AssessedAt),
				new CreateIndexOptions { Name = "TenantPlayerAssessedAt_1" }),
			cancellationToken: cancellationToken);
	}

	private async Task EnsureBillingIndexesAsync(CancellationToken cancellationToken)
	{
		var subscriptions = database.GetCollection<OrganizationSubscription>("organizationSubscriptions");
		await subscriptions.Indexes.CreateOneAsync(
			new CreateIndexModel<OrganizationSubscription>(
				Builders<OrganizationSubscription>.IndexKeys.Ascending(item => item.OrganizationId),
				new CreateIndexOptions { Name = "OrganizationId_1", Unique = true }),
			cancellationToken: cancellationToken);
		var invoices = database.GetCollection<BillingInvoice>("billingInvoices");
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
		var objects = database.GetCollection<StoredFileObject>("storedFileObjects");
		var files = database.GetCollection<ClubFile>("files");
		var audit = database.GetCollection<FileLifecycleAudit>("fileLifecycleAudit");

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
		var objects = database.GetCollection<StoredFileObject>("storedFileObjects");
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
		var organizations = database.GetCollection<Organization>("organizations");
		var clubs = database.GetCollection<SportsClub>("clubs");
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
		var collection = database.GetCollection<T>(collectionName);
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
		var users = database.GetCollection<AppUser>("users");
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
		var organizations = database.GetCollection<Organization>("organizations");
		await organizations.Indexes.CreateOneAsync(
			new CreateIndexModel<Organization>(
				Builders<Organization>.IndexKeys.Ascending(organization => organization.Slug),
				new CreateIndexOptions { Name = "Slug_1", Unique = true }),
			cancellationToken: cancellationToken);

		var clubs = database.GetCollection<SportsClub>("clubs");
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
		await EnsureTenantIndexAsync<ClubForm>("forms", cancellationToken);
		await EnsureTenantIndexAsync<ClubFormSubmission>("formSubmissions", cancellationToken);
		await EnsureTenantIndexAsync<ClubTeamProfile>("clubTeamProfiles", cancellationToken);
		await EnsureTenantIndexAsync<FinanceTransaction>("financeTransactions", cancellationToken);
		await EnsureTenantIndexAsync<ClubFile>("files", cancellationToken);
		await EnsureTenantIndexAsync<ClubNotification>("notifications", cancellationToken);
		await EnsureTenantIndexAsync<MessageThread>("messageThreads", cancellationToken);
		await EnsureTenantIndexAsync<Message>("messages", cancellationToken);
		await EnsureTenantIndexAsync<PlayerSeasonStats>("playerSeasonStats", cancellationToken);
		await EnsureTenantIndexAsync<PlayerHistoricalStats>("playerHistoricalStats", cancellationToken);
		await EnsureTenantIndexAsync<TrainingAssessment>("trainingAssessments", cancellationToken);
	}

	private async Task EnsureTenantIndexAsync<T>(string collectionName, CancellationToken cancellationToken)
		where T : ITenantOwned
	{
		var collection = database.GetCollection<T>(collectionName);
		var keys = Builders<T>.IndexKeys
			.Ascending(item => item.OrganizationId)
			.Ascending(item => item.ClubId);

		await collection.Indexes.CreateOneAsync(
			new CreateIndexModel<T>(keys, new CreateIndexOptions { Name = "TenantScope_1" }),
			cancellationToken: cancellationToken);
	}
}
