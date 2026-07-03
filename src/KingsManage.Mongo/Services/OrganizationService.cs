using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class OrganizationService : IOrganizationService
{
	private readonly IMongoCollection<Organization> _organizations;
	private readonly ITenantContext _tenant;

	public OrganizationService(MongoContext context, ITenantContext tenant)
	{
		_organizations = context.Database.GetCollection<Organization>("organizations");
		_tenant = tenant;
	}

	public async Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default) =>
		await _organizations.Find(_ => true)
			.SortBy(organization => organization.Name)
			.ToListAsync(cancellationToken);

	public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		await _organizations.Find(organization => organization.Id == id)
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<Organization?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
		await GetByIdAsync(_tenant.OrganizationId, cancellationToken);

	public async Task<Organization?> CreateAsync(
		Organization organization,
		CancellationToken cancellationToken = default)
	{
		Normalise(organization);
		if (await SlugExistsAsync(organization.Slug, null, cancellationToken)) return null;

		var now = DateTime.UtcNow;
		organization.Id = Guid.NewGuid();
		organization.IsActive = true;
		organization.CreatedAt = now;
		organization.UpdatedAt = now;
		await _organizations.InsertOneAsync(organization, cancellationToken: cancellationToken);
		return organization;
	}

	public async Task<Organization?> UpdateAsync(
		Guid id,
		Organization organization,
		CancellationToken cancellationToken = default)
	{
		var existing = await GetByIdAsync(id, cancellationToken);
		if (existing is null) return null;

		Normalise(organization);
		if (await SlugExistsAsync(organization.Slug, id, cancellationToken)) return null;

		existing.Name = organization.Name;
		existing.Slug = organization.Slug;
		existing.UpdatedAt = DateTime.UtcNow;
		var result = await _organizations.ReplaceOneAsync(
			current => current.Id == id,
			existing,
			cancellationToken: cancellationToken);
		return result.MatchedCount == 0 ? null : existing;
	}

	public async Task<Organization?> UpdateCurrentAsync(Organization organization, CancellationToken cancellationToken = default)
		=> await UpdateAsync(_tenant.OrganizationId, organization, cancellationToken);

	public async Task<Organization?> SetActiveAsync(
		Guid id,
		bool isActive,
		CancellationToken cancellationToken = default)
	{
		return await _organizations.FindOneAndUpdateAsync(
			organization => organization.Id == id,
			Builders<Organization>.Update
				.Set(organization => organization.IsActive, isActive)
				.Set(organization => organization.UpdatedAt, DateTime.UtcNow),
			new FindOneAndUpdateOptions<Organization>
			{
				ReturnDocument = ReturnDocument.After
			},
			cancellationToken);
	}

	private async Task<bool> SlugExistsAsync(
		string slug,
		Guid? exceptId,
		CancellationToken cancellationToken)
	{
		var filter = Builders<Organization>.Filter.Eq(organization => organization.Slug, slug);
		if (exceptId.HasValue)
			filter &= Builders<Organization>.Filter.Ne(organization => organization.Id, exceptId.Value);
		return await _organizations.Find(filter).AnyAsync(cancellationToken);
	}

	private static void Normalise(Organization organization)
	{
		organization.Name = organization.Name.Trim();
		organization.Slug = organization.Slug.Trim().ToLowerInvariant();
	}
}
