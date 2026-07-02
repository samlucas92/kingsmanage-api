namespace KingsManage;

public sealed class FileLifecycleSettings
{
	public bool Enabled { get; set; } = true;
	public int CleanupIntervalMinutes { get; set; } = 60;
	public int OrphanRetentionHours { get; set; } = 168;
	public int PendingUploadRetentionHours { get; set; } = 24;
	public int QuarantineRetentionHours { get; set; } = 72;
	public long DefaultOrganizationQuotaBytes { get; set; } = 1024L * 1024L * 1024L;
	public int QuotaWarningPercent { get; set; } = 80;
}
