namespace KingsManage;

public class AppUser
{
	public Guid Id { get; set; }

	public string Email { get; set; } = string.Empty;

	public string PasswordHash { get; set; } = string.Empty;

	public UserRole Role { get; set; }

	public bool IsPlatformAdmin { get; set; }

	public Guid? DefaultOrganizationId { get; set; }

	public Guid? DefaultClubId { get; set; }

	public List<UserMembership> Memberships { get; set; } = [];

	public Guid? PlayerId { get; set; }

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public DateTime? LastLoginAt { get; set; }
}
