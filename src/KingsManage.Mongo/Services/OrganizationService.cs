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

	public async Task<Organization?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
		await _organizations.Find(organization => organization.Id == _tenant.OrganizationId)
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<Organization?> UpdateCurrentAsync(Organization organization, CancellationToken cancellationToken = default)
	{
		var existing = await GetCurrentAsync(cancellationToken);
		if (existing is null) return null;

		existing.Name = organization.Name.Trim();
		existing.Slug = organization.Slug.Trim().ToLowerInvariant();
		existing.UpdatedAt = DateTime.UtcNow;

		var result = await _organizations.ReplaceOneAsync(
			current => current.Id == _tenant.OrganizationId,
			existing,
			cancellationToken: cancellationToken);

		return result.MatchedCount == 0 ? null : existing;
	}
}
