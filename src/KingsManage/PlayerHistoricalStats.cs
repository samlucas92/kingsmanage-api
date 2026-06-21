namespace KingsManage;

public class PlayerHistoricalStats : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid PlayerId { get; set; }

	public int Appearances { get; set; }

	public int Goals { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
