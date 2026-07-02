namespace KingsManage;

public sealed class FileStorageValidationResult
{
	public bool IsValid { get; set; }
	public string ErrorMessage { get; set; } = string.Empty;
	public string ContentHash { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long SizeBytes { get; set; }
	public bool IsSafe { get; set; } = true;
	public string ThreatName { get; set; } = string.Empty;
}
