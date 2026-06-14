using KingsManage;

namespace KingsManage.Web.Models;

public sealed class LoginResponse
{
	public string Token { get; set; } = string.Empty;

	public DateTime ExpiresAt { get; set; }

	public UserViewModel User { get; set; } = new();
}
