using KingsManage;

namespace KingsManage.Web.Models;

public sealed class UserViewModel
{
	public Guid Id { get; set; }

	public string Email { get; set; } = string.Empty;

	public UserRole Role { get; set; }

	public Guid? PlayerId { get; set; }
	public Guid? DefaultClubId { get; set; }
	public TenantRole? TenantRole { get; set; }
	public bool IsPlatformAdmin { get; set; }
	public List<UserMembership> Memberships { get; set; } = [];

	public bool IsActive { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public DateTime? LastLoginAt { get; set; }

	public static UserViewModel FromUser(AppUser user, TenantRole? tenantRole = null)
	{
		return new UserViewModel
		{
			Id = user.Id,
			Email = user.Email,
			Role = tenantRole.HasValue ? MapRole(tenantRole.Value) : user.Role,
			PlayerId = user.PlayerId,
			DefaultClubId = user.DefaultClubId,
			TenantRole = tenantRole,
			IsPlatformAdmin = user.IsPlatformAdmin,
			Memberships = user.Memberships,
			IsActive = user.IsActive,
			CreatedAt = user.CreatedAt,
			UpdatedAt = user.UpdatedAt,
			LastLoginAt = user.LastLoginAt
		};
	}

	private static UserRole MapRole(KingsManage.TenantRole role) => role switch
	{
		KingsManage.TenantRole.OrganizationAdmin or KingsManage.TenantRole.ClubAdmin => UserRole.Admin,
		KingsManage.TenantRole.TeamManager or KingsManage.TenantRole.Coach => UserRole.Coach,
		_ => UserRole.Player
	};
}
