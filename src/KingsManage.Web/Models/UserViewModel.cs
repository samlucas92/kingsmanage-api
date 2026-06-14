using KingsManage;

namespace KingsManage.Web.Models;

public sealed class UserViewModel
{
	public Guid Id { get; set; }

	public string Email { get; set; } = string.Empty;

	public UserRole Role { get; set; }

	public Guid? PlayerId { get; set; }

	public bool IsActive { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public DateTime? LastLoginAt { get; set; }

	public static UserViewModel FromUser(AppUser user)
	{
		return new UserViewModel
		{
			Id = user.Id,
			Email = user.Email,
			Role = user.Role,
			PlayerId = user.PlayerId,
			IsActive = user.IsActive,
			CreatedAt = user.CreatedAt,
			UpdatedAt = user.UpdatedAt,
			LastLoginAt = user.LastLoginAt
		};
	}
}
