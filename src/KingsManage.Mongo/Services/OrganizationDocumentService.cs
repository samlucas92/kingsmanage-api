using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class OrganizationDocumentService : IOrganizationDocumentService
{
	private readonly IMongoCollection<ClubPost> documents;
	private readonly ITenantContext tenant;

	static OrganizationDocumentService()
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(ClubPost)))
		{
			BsonClassMap.RegisterClassMap<ClubPost>(classMap =>
			{
				classMap.AutoMap();
				classMap.SetIgnoreExtraElements(true);
			});
		}
	}

	public OrganizationDocumentService(MongoContext context, ITenantContext tenant)
	{
		documents = context.Database.GetCollection<ClubPost>("posts");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<ClubPost>> GetAllAsync(CancellationToken cancellationToken = default) =>
		await documents
			.Find(DocumentFilter())
			.SortBy(document => document.IsArchived)
			.ThenByDescending(document => document.UpdatedAt)
			.ToListAsync(cancellationToken);

	public async Task<ClubPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		await documents.Find(DocumentFilter() & Builders<ClubPost>.Filter.Eq(document => document.Id, id))
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<ClubPost> CreateAsync(ClubPost document, CancellationToken cancellationToken = default)
	{
		document.Id = document.Id == Guid.Empty ? Guid.NewGuid() : document.Id;
		document.OrganizationId = tenant.OrganizationId;
		document.ClubId = Guid.Empty;
		document.Type = ClubPostType.OrganizationDocument;
		document.Title = document.Title.Trim();
		document.Body = document.Body.Trim();
		document.CreatedAt = DateTime.UtcNow;
		document.UpdatedAt = document.CreatedAt;
		await documents.InsertOneAsync(document, cancellationToken: cancellationToken);
		return document;
	}

	public async Task<ClubPost?> UpdateAsync(ClubPost document, CancellationToken cancellationToken = default)
	{
		var existing = await GetByIdAsync(document.Id, cancellationToken);
		if (existing is null)
		{
			return null;
		}

		existing.Title = document.Title.Trim();
		existing.Body = document.Body.Trim();
		existing.UpdatedAt = DateTime.UtcNow;
		var result = await documents.ReplaceOneAsync(
			DocumentFilter() & Builders<ClubPost>.Filter.Eq(item => item.Id, document.Id),
			existing,
			cancellationToken: cancellationToken);
		return result.MatchedCount == 0 ? null : existing;
	}

	public async Task<ClubPost?> SetArchivedAsync(Guid id, bool archived, CancellationToken cancellationToken = default) =>
		await documents.FindOneAndUpdateAsync(
			DocumentFilter() & Builders<ClubPost>.Filter.Eq(document => document.Id, id),
			Builders<ClubPost>.Update
				.Set(document => document.IsArchived, archived)
				.Set(document => document.UpdatedAt, DateTime.UtcNow),
			new FindOneAndUpdateOptions<ClubPost> { ReturnDocument = ReturnDocument.After },
			cancellationToken);

	private FilterDefinition<ClubPost> DocumentFilter() =>
		Builders<ClubPost>.Filter.Eq(document => document.OrganizationId, tenant.OrganizationId) &
		Builders<ClubPost>.Filter.Eq(document => document.Type, ClubPostType.OrganizationDocument);
}
