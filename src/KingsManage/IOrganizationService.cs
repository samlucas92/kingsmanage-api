namespace KingsManage;

public interface IOrganizationService
{
	Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<OrganizationAdministratorAccount>> GetAdministratorAccountsAsync(
		CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<OrganizationAdministratorAccount>>([]);

	Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	Task<Organization?> GetCurrentAsync(CancellationToken cancellationToken = default);

	Task<Organization?> CreateAsync(Organization organization, CancellationToken cancellationToken = default);

	Task<Organization?> UpdateAsync(Guid id, Organization organization, CancellationToken cancellationToken = default);

	Task<Organization?> UpdateCurrentAsync(Organization organization, CancellationToken cancellationToken = default);

	Task<Organization?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
	Task<OrganizationDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class OrganizationAdministratorAccount
{
	public Guid OrganizationId { get; set; }
	public Guid UserId { get; set; }
	public string Email { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public DateTime? LastLoginAt { get; set; }
}

public enum OrganizationDeleteResult
{
	Deleted,
	NotFound,
	HasClubs
}
