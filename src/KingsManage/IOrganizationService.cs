namespace KingsManage;

public interface IOrganizationService
{
	Task<Organization?> GetCurrentAsync(CancellationToken cancellationToken = default);

	Task<Organization?> UpdateCurrentAsync(Organization organization, CancellationToken cancellationToken = default);
}
