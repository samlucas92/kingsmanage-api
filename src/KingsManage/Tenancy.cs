namespace KingsManage;

public interface ITenantOwned
{
	Guid OrganizationId { get; set; }

	Guid ClubId { get; set; }
}

public static class DefaultTenant
{
	public static readonly Guid OrganizationId = Guid.Parse("8d7c96be-2e8d-4d73-9f2a-f95ce42cf001");
	public static readonly Guid ClubId = Guid.Parse("8d7c96be-2e8d-4d73-9f2a-f95ce42cf002");

	public const string OrganizationName = "Kingsbridge Colts";
	public const string ClubName = "Kingsbridge Colts Football Club";
}

public enum TenantRole
{
	OrganizationAdmin,
	ClubAdmin,
	TeamManager,
	Coach,
	Player
}

public sealed class UserMembership
{
	public Guid OrganizationId { get; set; }

	public Guid? ClubId { get; set; }

	public Guid? TeamId { get; set; }

	public TenantRole Role { get; set; }
}
