namespace KingsManage.Web.Models;

public sealed class R2StorageSettings
{
	public string AccountId { get; set; } = string.Empty;
	public string AccessKeyId { get; set; } = string.Empty;
	public string SecretAccessKey { get; set; } = string.Empty;
	public string BucketName { get; set; } = string.Empty;
	public string PublicBaseUrl { get; set; } = string.Empty;
}
