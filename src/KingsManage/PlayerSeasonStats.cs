namespace KingsManage;

public class PlayerSeasonStats
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid PlayerId { get; set; }

	public Guid SeasonId { get; set; }

	public Guid? TeamId { get; set; }

	public ClubTeam Team { get; set; }

	public int Appearances { get; set; }

	public int Starts { get; set; }

	public int Bench { get; set; }

	public int UnusedSubstitutes { get; set; }

	public int Goals { get; set; }

	public int Assists { get; set; }

	public int Minutes { get; set; }

	public int Motm { get; set; }

	public int YellowCards { get; set; }

	public int RedCards { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
