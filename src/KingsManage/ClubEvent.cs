namespace KingsManage;

public class ClubEvent
{
	public Guid Id { get; set; }
	public Guid? SeasonId { get; set; }
	public ClubEventType Type { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public DateTime StartDateTime { get; set; }
	public DateTime? EndDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public Guid? MatchId { get; set; }
	public ClubTeam? Team { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
