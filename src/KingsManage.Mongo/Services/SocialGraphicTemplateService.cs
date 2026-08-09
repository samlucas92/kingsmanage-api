using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class SocialGraphicTemplateService : ISocialGraphicTemplateService
{
	private readonly IMongoCollection<SocialGraphicTemplateCustomization> customizations;
	private readonly IMongoCollection<SocialGraphicTemplateRevision> revisions;
	private readonly TenantMongoScope tenant;

	public SocialGraphicTemplateService(MongoContext context, TenantMongoScope tenant)
	{
		customizations = context.Database.GetCollection<SocialGraphicTemplateCustomization>(
			"socialGraphicTemplates"
		);
		revisions = context.Database.GetCollection<SocialGraphicTemplateRevision>(
			"socialGraphicTemplateRevisions"
		);
		this.tenant = tenant;
	}

	public async Task<SocialGraphicTemplateCustomization?> GetAsync(
		string templateId,
		CancellationToken cancellationToken = default
	) =>
		await customizations.Find(CustomizationFilter(templateId))
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<IReadOnlyList<SocialGraphicTemplateRevision>> GetRevisionsAsync(
		string templateId,
		int limit = 20,
		CancellationToken cancellationToken = default
	) =>
		await revisions.Find(RevisionFilter(templateId))
			.SortByDescending(revision => revision.Revision)
			.Limit(Math.Clamp(limit, 1, 50))
			.ToListAsync(cancellationToken);

	public async Task<SocialGraphicTemplateSaveResult> SaveAsync(
		string templateId,
		int schemaVersion,
		string definitionJson,
		int expectedRevision,
		Guid userId,
		CancellationToken cancellationToken = default
	)
	{
		var normalizedTemplateId = NormalizeTemplateId(templateId);
		var existing = await GetAsync(normalizedTemplateId, cancellationToken);
		if (existing is null)
		{
			if (expectedRevision != 0)
			{
				return new(SocialGraphicTemplateSaveStatus.Conflict);
			}

			var now = DateTime.UtcNow;
			var customization = tenant.Assign(new SocialGraphicTemplateCustomization
			{
				Id = Guid.NewGuid(),
				TemplateId = normalizedTemplateId,
				SchemaVersion = schemaVersion,
				DefinitionJson = definitionJson,
				Revision = await GetNextRevisionAsync(normalizedTemplateId, cancellationToken),
				UpdatedByUserId = userId,
				CreatedAt = now,
				UpdatedAt = now
			});

			try
			{
				await customizations.InsertOneAsync(
					customization,
					cancellationToken: cancellationToken
				);
			}
			catch (MongoWriteException exception) when (
				exception.WriteError.Category == ServerErrorCategory.DuplicateKey
			)
			{
				return new(SocialGraphicTemplateSaveStatus.Conflict);
			}

			await InsertRevisionAsync(customization, userId, cancellationToken);
			return new(SocialGraphicTemplateSaveStatus.Saved, customization);
		}

		if (existing.Revision != expectedRevision)
		{
			return new(SocialGraphicTemplateSaveStatus.Conflict);
		}

		var updated = new SocialGraphicTemplateCustomization
		{
			OrganizationId = existing.OrganizationId,
			ClubId = existing.ClubId,
			Id = existing.Id,
			TemplateId = existing.TemplateId,
			SchemaVersion = schemaVersion,
			DefinitionJson = definitionJson,
			Revision = existing.Revision + 1,
			UpdatedByUserId = userId,
			CreatedAt = existing.CreatedAt,
			UpdatedAt = DateTime.UtcNow
		};
		var updateFilter = CustomizationFilter(normalizedTemplateId) &
			Builders<SocialGraphicTemplateCustomization>.Filter.Eq(
				item => item.Revision,
				expectedRevision
			);
		var updateResult = await customizations.ReplaceOneAsync(
			updateFilter,
			updated,
			cancellationToken: cancellationToken
		);
		if (updateResult.MatchedCount == 0)
		{
			return new(SocialGraphicTemplateSaveStatus.Conflict);
		}

		await InsertRevisionAsync(updated, userId, cancellationToken);
		return new(SocialGraphicTemplateSaveStatus.Saved, updated);
	}

	public async Task<SocialGraphicTemplateSaveResult> RestoreRevisionAsync(
		string templateId,
		int revision,
		int expectedRevision,
		Guid userId,
		CancellationToken cancellationToken = default
	)
	{
		var target = await revisions.Find(
			RevisionFilter(templateId) &
				Builders<SocialGraphicTemplateRevision>.Filter.Eq(item => item.Revision, revision)
		).FirstOrDefaultAsync(cancellationToken);
		if (target is null)
		{
			return new(SocialGraphicTemplateSaveStatus.RevisionNotFound);
		}

		return await SaveAsync(
			templateId,
			target.SchemaVersion,
			target.DefinitionJson,
			expectedRevision,
			userId,
			cancellationToken
		);
	}

	public async Task<SocialGraphicTemplateResetResult> ResetAsync(
		string templateId,
		int expectedRevision,
		CancellationToken cancellationToken = default
	)
	{
		var existing = await GetAsync(templateId, cancellationToken);
		if (existing is null)
		{
			return SocialGraphicTemplateResetResult.NotFound;
		}
		if (existing.Revision != expectedRevision)
		{
			return SocialGraphicTemplateResetResult.Conflict;
		}

		var result = await customizations.DeleteOneAsync(
			CustomizationFilter(templateId) &
				Builders<SocialGraphicTemplateCustomization>.Filter.Eq(
					item => item.Revision,
					expectedRevision
				),
			cancellationToken
		);
		return result.DeletedCount > 0
			? SocialGraphicTemplateResetResult.Reset
			: SocialGraphicTemplateResetResult.Conflict;
	}

	private async Task<int> GetNextRevisionAsync(
		string templateId,
		CancellationToken cancellationToken
	)
	{
		var latest = await revisions.Find(RevisionFilter(templateId))
			.SortByDescending(item => item.Revision)
			.FirstOrDefaultAsync(cancellationToken);
		return (latest?.Revision ?? 0) + 1;
	}

	private async Task InsertRevisionAsync(
		SocialGraphicTemplateCustomization customization,
		Guid userId,
		CancellationToken cancellationToken
	)
	{
		await revisions.InsertOneAsync(
			tenant.Assign(new SocialGraphicTemplateRevision
			{
				Id = Guid.NewGuid(),
				CustomizationId = customization.Id,
				TemplateId = customization.TemplateId,
				SchemaVersion = customization.SchemaVersion,
				DefinitionJson = customization.DefinitionJson,
				Revision = customization.Revision,
				CreatedByUserId = userId,
				CreatedAt = customization.UpdatedAt
			}),
			cancellationToken: cancellationToken
		);
	}

	private FilterDefinition<SocialGraphicTemplateCustomization> CustomizationFilter(
		string templateId
	) =>
		tenant.Filter<SocialGraphicTemplateCustomization>() &
		Builders<SocialGraphicTemplateCustomization>.Filter.Eq(
			item => item.TemplateId,
			NormalizeTemplateId(templateId)
		);

	private FilterDefinition<SocialGraphicTemplateRevision> RevisionFilter(string templateId) =>
		tenant.Filter<SocialGraphicTemplateRevision>() &
		Builders<SocialGraphicTemplateRevision>.Filter.Eq(
			item => item.TemplateId,
			NormalizeTemplateId(templateId)
		);

	private static string NormalizeTemplateId(string templateId) =>
		templateId.Trim().ToLowerInvariant();
}
