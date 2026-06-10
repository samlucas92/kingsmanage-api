namespace KingsManage;

public class Season
{
	public string Id { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public DateTime StartDate { get; set; }

	public DateTime EndDate { get; set; }

	public bool IsActive { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}