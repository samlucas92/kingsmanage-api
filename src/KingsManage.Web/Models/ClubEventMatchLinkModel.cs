using KingsManage;

namespace KingsManage.Web.Models;

public class ClubEventMatchLinkModel
{
	public Guid? TeamId { get; set; }
	public ClubTeam Team { get; set; }
	public Guid? MatchId { get; set; }

	public ClubEventMatchLink ToMatchLink()
	{
		return new ClubEventMatchLink
		{
			TeamId = TeamId,
			Team = Team,
			MatchId = MatchId
		};
	}
}
