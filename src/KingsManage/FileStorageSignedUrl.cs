namespace KingsManage;

public sealed class FileStorageSignedUrl
{
	public string Url { get; set; } = string.Empty;
	public DateTime ExpiresAtUtc { get; set; }
}
