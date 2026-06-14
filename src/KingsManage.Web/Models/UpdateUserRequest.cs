using KingsManage;

namespace KingsManage.Web.Models;

public sealed class UpdateUserRequest
{
	public string Email { get; set; } = string.Empty;

	public UserRole Role { get; set; }

	public Guid? PlayerId { get; set; }

	public bool IsActive { get; set; } = true;
}
