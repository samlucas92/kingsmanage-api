namespace KingsManage;

public class PostponementAudit
{
	public string Id { get; set; } = string.Empty;

	public DateTime OldDate { get; set; }

	public DateTime NewDate { get; set; }

	public string? Reason { get; set; }

	public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}