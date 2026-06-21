namespace KingsManage;

public interface ITenantContext
{
	Guid OrganizationId { get; }

	Guid ClubId { get; }
}
