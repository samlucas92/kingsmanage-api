using KingsManage;

namespace KingsManage.Web.Models;

public class ClubEventMatchLinkModel
{
	public ClubTeam Team { get; set; }
	public Guid? MatchId { get; set; }

	public ClubEventMatchLink ToMatchLink()
	{
		return new ClubEventMatchLink
		{
			Team = Team,
			MatchId = MatchId
		};
	}
}
