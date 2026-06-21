namespace KingsManage;

public interface ITenantContext
{
	bool IsAvailable { get; }

	Guid OrganizationId { get; }

	Guid ClubId { get; }
}
