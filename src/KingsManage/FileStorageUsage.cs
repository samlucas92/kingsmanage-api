namespace KingsManage;

public sealed class FileStorageUsage
{
	public Guid OrganizationId { get; set; }
	public long UsedBytes { get; set; }
	public long QuotaBytes { get; set; }
	public long RemainingBytes { get; set; }
	public double UsedPercent { get; set; }
	public bool IsNearLimit { get; set; }
	public bool IsAtLimit { get; set; }
	public int StoredObjectCount { get; set; }
	public int PendingObjectCount { get; set; }
	public int OrphanedObjectCount { get; set; }
}
