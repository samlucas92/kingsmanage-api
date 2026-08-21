namespace KingsManage.Web.Models;

public sealed class MetaIntegrationSettings
{
	public string AppId { get; set; } = string.Empty;
	public string AppSecret { get; set; } = string.Empty;
	public string RedirectUri { get; set; } = string.Empty;
	public string GraphApiVersion { get; set; } = "v24.0";
	public string TokenEncryptionKey { get; set; } = string.Empty;
	public bool PublishingEnabled { get; set; } = true;
	public int PollIntervalSeconds { get; set; } = 20;

	public bool OAuthIsConfigured =>
		!string.IsNullOrWhiteSpace(AppId) &&
		!string.IsNullOrWhiteSpace(AppSecret) &&
		Uri.TryCreate(RedirectUri, UriKind.Absolute, out _);
}
