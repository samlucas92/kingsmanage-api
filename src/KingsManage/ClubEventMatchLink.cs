namespace KingsManage;

public class ClubEventMatchLink
{
	public Guid? TeamId { get; set; }
	public ClubTeam Team { get; set; }
	public Guid? MatchId { get; set; }
}
