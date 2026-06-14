namespace KingsManage.Web.Models;

public sealed class JwtSettings
{
	public string Issuer { get; set; } = "KingsManage";

	public string Audience { get; set; } = "KingsManage.Frontend";

	public string Secret { get; set; } = string.Empty;

	public int ExpiryMinutes { get; set; } = 480;
}
