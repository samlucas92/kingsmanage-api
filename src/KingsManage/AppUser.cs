namespace KingsManage;

public class AppUser
{
	public Guid Id { get; set; }

	public string Email { get; set; } = string.Empty;

	public string PasswordHash { get; set; } = string.Empty;

	public UserRole Role { get; set; }

	public Guid? PlayerId { get; set; }

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public DateTime? LastLoginAt { get; set; }
}
