namespace KingsManage;

public interface IOrganizationService
{
	Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	Task<Organization?> GetCurrentAsync(CancellationToken cancellationToken = default);

	Task<Organization?> CreateAsync(Organization organization, CancellationToken cancellationToken = default);

	Task<Organization?> UpdateAsync(Guid id, Organization organization, CancellationToken cancellationToken = default);

	Task<Organization?> UpdateCurrentAsync(Organization organization, CancellationToken cancellationToken = default);

	Task<Organization?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
	Task<OrganizationDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public enum OrganizationDeleteResult
{
	Deleted,
	NotFound,
	HasClubs
}
