namespace KingsManage;

public sealed class SportsClub
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid OrganizationId { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Slug { get; set; } = string.Empty;

	public string SportKey { get; set; } = string.Empty;

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
