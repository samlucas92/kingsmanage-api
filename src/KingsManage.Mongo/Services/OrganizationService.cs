using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class OrganizationService : IOrganizationService
{
	private readonly IMongoCollection<Organization> organizations;
	private readonly IMongoCollection<SportsClub> clubs;
	private readonly IMongoCollection<OrganizationSubscription> subscriptions;
	private readonly IMongoCollection<BillingInvoice> invoices;
	private readonly ITenantContext tenant;

	public OrganizationService(MongoContext context, ITenantContext tenant)
	{
		organizations = context.Database.GetCollection<Organization>("organizations");
		clubs = context.Database.GetCollection<SportsClub>("clubs");
		subscriptions = context.Database.GetCollection<OrganizationSubscription>("organizationSubscriptions");
		invoices = context.Database.GetCollection<BillingInvoice>("billingInvoices");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default) =>
		await organizations.Find(_ => true)
			.SortBy(organization => organization.Name)
			.ToListAsync(cancellationToken);

	public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		await organizations.Find(organization => organization.Id == id)
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<Organization?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
		await GetByIdAsync(tenant.OrganizationId, cancellationToken);

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
		await organizations.InsertOneAsync(organization, cancellationToken: cancellationToken);
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
		var result = await organizations.ReplaceOneAsync(
			current => current.Id == id,
			existing,
			cancellationToken: cancellationToken);
		return result.MatchedCount == 0 ? null : existing;
	}

	public async Task<Organization?> UpdateCurrentAsync(Organization organization, CancellationToken cancellationToken = default)
		=> await UpdateAsync(tenant.OrganizationId, organization, cancellationToken);

	public async Task<Organization?> SetActiveAsync(
		Guid id,
		bool isActive,
		CancellationToken cancellationToken = default)
	{
		return await organizations.FindOneAndUpdateAsync(
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

	public async Task<OrganizationDeleteResult> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default)
	{
		var organization = await GetByIdAsync(id, cancellationToken);
		if (organization is null) return OrganizationDeleteResult.NotFound;
		if (await clubs.Find(club => club.OrganizationId == id).AnyAsync(cancellationToken))
			return OrganizationDeleteResult.HasClubs;

		await subscriptions.DeleteManyAsync(
			subscription => subscription.OrganizationId == id,
			cancellationToken);
		await invoices.DeleteManyAsync(
			invoice => invoice.OrganizationId == id,
			cancellationToken);
		var result = await organizations.DeleteOneAsync(
			item => item.Id == id,
			cancellationToken);
		return result.DeletedCount > 0
			? OrganizationDeleteResult.Deleted
			: OrganizationDeleteResult.NotFound;
	}

	private async Task<bool> SlugExistsAsync(
		string slug,
		Guid? exceptId,
		CancellationToken cancellationToken)
	{
		var filter = Builders<Organization>.Filter.Eq(organization => organization.Slug, slug);
		if (exceptId.HasValue)
			filter &= Builders<Organization>.Filter.Ne(organization => organization.Id, exceptId.Value);
		return await organizations.Find(filter).AnyAsync(cancellationToken);
	}

	private static void Normalise(Organization organization)
	{
		organization.Name = organization.Name.Trim();
		organization.Slug = organization.Slug.Trim().ToLowerInvariant();
	}
}
