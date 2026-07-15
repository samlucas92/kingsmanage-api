using KingsManage;

namespace KingsManage.Web.Models;

public class UpdateClubEventModel
{
	public ClubEventType Type { get; set; }
	public ClubEventTeamScope TeamScope { get; set; } = ClubEventTeamScope.Both;
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public DateTime StartDateTime { get; set; }
	public DateTime? EndDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public List<ClubEventMatchLinkModel> MatchLinks { get; set; } = [];

	public ClubEvent ToClubEvent(ClubEvent existingEvent)
	{
		return new ClubEvent
		{
			Id = existingEvent.Id,
			Type = Type,
			TeamScope = TeamScope,
			Title = Title,
			Description = Description,
			StartDateTime = StartDateTime,
			EndDateTime = EndDateTime,
			Location = Location,
			RecurrenceSeriesId = existingEvent.RecurrenceSeriesId,
			Recurrence = existingEvent.Recurrence,
			MatchLinks = MatchLinks.Select(matchLink => matchLink.ToMatchLink()).ToList(),
			AvailabilityResponses = existingEvent.AvailabilityResponses,
			SeenBy = existingEvent.SeenBy,
			CreatedAt = existingEvent.CreatedAt
		};
	}
}
