namespace KingsManage;

public class PlayerHistoricalStats
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid PlayerId { get; set; }

	public int Appearances { get; set; }

	public int Goals { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
