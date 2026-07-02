namespace KingsManage;

public sealed class FileUploadCapacityResult
{
	public bool IsAllowed { get; set; }
	public bool ReusesExistingObject { get; set; }
	public FileStorageUsage Usage { get; set; } = new();
}
