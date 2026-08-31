namespace KingsManage;

public interface IOrganizationLocationService
{
	Task<IReadOnlyList<OrganizationLocation>> GetAllAsync(
		CancellationToken cancellationToken = default
	);

	Task<OrganizationLocation?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task<OrganizationLocation> CreateAsync(
		OrganizationLocation location,
		CancellationToken cancellationToken = default
	);

	Task<OrganizationLocation?> UpdateAsync(
		OrganizationLocation location,
		CancellationToken cancellationToken = default
	);

	Task<bool> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);
}
