using KingsManage;

namespace KingsManage.Web.Models;

public sealed class FileDownloadUrlResponse
{
	public ClubFile File { get; set; } = new();
	public string DownloadUrl { get; set; } = string.Empty;
	public DateTime ExpiresAtUtc { get; set; }
}
