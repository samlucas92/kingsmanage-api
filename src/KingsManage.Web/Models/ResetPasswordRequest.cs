namespace KingsManage.Web.Models;

public sealed class ResetPasswordRequest
{
	public string NewPassword { get; set; } = string.Empty;
}
