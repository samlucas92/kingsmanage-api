using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class OrganizationLocationService : IOrganizationLocationService
{
	private readonly IMongoCollection<OrganizationLocation> locations;
	private readonly TenantMongoScope tenant;

	public OrganizationLocationService(MongoContext context, TenantMongoScope tenant)
	{
		locations = context.Database.GetCollection<OrganizationLocation>("organizationLocations");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<OrganizationLocation>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await locations
			.Find(tenant.Filter<OrganizationLocation>())
			.SortBy(location => location.Name)
			.ThenBy(location => location.Address)
			.ToListAsync(cancellationToken);
	}

	public async Task<OrganizationLocation?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return await locations
			.Find(tenant.Filter<OrganizationLocation>(location => location.Id == id))
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<OrganizationLocation> CreateAsync(
		OrganizationLocation location,
		CancellationToken cancellationToken = default
	)
	{
		var now = DateTime.UtcNow;
		location.Id = Guid.NewGuid();
		Normalise(location);
		location.CreatedAt = now;
		location.UpdatedAt = now;
		tenant.Assign(location);
		await locations.InsertOneAsync(location, cancellationToken: cancellationToken);
		return location;
	}

	public async Task<OrganizationLocation?> UpdateAsync(
		OrganizationLocation location,
		CancellationToken cancellationToken = default
	)
	{
		var existing = await GetByIdAsync(location.Id, cancellationToken);
		if (existing is null)
		{
			return null;
		}

		Normalise(location);
		location.CreatedAt = existing.CreatedAt;
		location.UpdatedAt = DateTime.UtcNow;
		tenant.Assign(location);

		var result = await locations.ReplaceOneAsync(
			tenant.Filter<OrganizationLocation>(item => item.Id == location.Id),
			location,
			cancellationToken: cancellationToken
		);
		return result.MatchedCount == 0 ? null : location;
	}

	public async Task<bool> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var result = await locations.DeleteOneAsync(
			tenant.Filter<OrganizationLocation>(location => location.Id == id),
			cancellationToken
		);
		return result.DeletedCount > 0;
	}

	private static void Normalise(OrganizationLocation location)
	{
		location.Name = location.Name.Trim();
		location.Address = location.Address.Trim();
		location.Notes = (location.Notes ?? string.Empty).Trim();
	}
}
