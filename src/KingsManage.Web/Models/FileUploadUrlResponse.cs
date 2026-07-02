using KingsManage;

namespace KingsManage.Web.Models;

public sealed class FileUploadUrlResponse
{
	public ClubFile File { get; set; } = new();
	public string UploadUrl { get; set; } = string.Empty;
	public DateTime ExpiresAtUtc { get; set; }
	public bool UploadRequired { get; set; } = true;
	public bool ReusedStoredObject { get; set; }
	public string StorageWarning { get; set; } = string.Empty;
}
