using KingsManage;

namespace KingsManage.Web.Models;

public class CreateClubEventModel
{
	public ClubEventType Type { get; set; }
	public ClubEventTeamScope TeamScope { get; set; } = ClubEventTeamScope.Both;
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public DateTime StartDateTime { get; set; }
	public DateTime? EndDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public List<ClubEventMatchLinkModel> MatchLinks { get; set; } = [];
	public bool CreateLinkedMatches { get; set; }
	public List<CreateMatchForEventModel> CreateMatches { get; set; } = [];
	public CreateEventRecurrenceModel? Recurrence { get; set; }

	public ClubEvent ToClubEvent()
	{
		return new ClubEvent
		{
			Type = Type,
			TeamScope = TeamScope,
			Title = Title,
			Description = Description,
			StartDateTime = StartDateTime,
			EndDateTime = EndDateTime,
			Location = Location,
			MatchLinks = MatchLinks.Select(matchLink => matchLink.ToMatchLink()).ToList()
		};
	}
}
