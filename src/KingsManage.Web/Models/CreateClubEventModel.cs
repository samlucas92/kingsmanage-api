using KingsManage;

namespace KingsManage.Web.Models;

public class CreateClubEventModel
{
	public Guid? SeasonId { get; set; }
	public ClubEventType Type { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public DateTime StartDateTime { get; set; }
	public DateTime? EndDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public Guid? MatchId { get; set; }
	public ClubTeam? Team { get; set; }

	public ClubEvent ToClubEvent()
	{
		return new ClubEvent
		{
			SeasonId = SeasonId,
			Type = Type,
			Title = Title,
			Description = Description,
			StartDateTime = StartDateTime,
			EndDateTime = EndDateTime,
			Location = Location,
			MatchId = MatchId,
			Team = Team
		};
	}
}
