using KingsManage;

namespace KingsManage.Web.Models;

public class CreateMatchForEventModel
{
	public Guid? SeasonId { get; set; }
	public ClubTeam Team { get; set; }
	public string Opponent { get; set; } = string.Empty;
	public string Competition { get; set; } = string.Empty;
	public DateTime? Date { get; set; }
	public MatchVenue Venue { get; set; }
	public string Location { get; set; } = string.Empty;
	public LineupFormation SelectedFormation { get; set; } = LineupFormation.FourThreeThree;

	public Match ToMatch(
		Guid activeSeasonId,
		DateTime eventStartDateTime,
		string eventLocation,
		Guid? clubEventId = null
	)
	{
		return new Match
		{
			SeasonId = activeSeasonId,
			ClubEventId = clubEventId,
			Team = Team,
			Opponent = Opponent,
			Competition = Competition,
			Date = Date ?? eventStartDateTime,
			Venue = Venue,
			Location = string.IsNullOrWhiteSpace(Location)
				? eventLocation
				: Location,
			State = MatchState.Upcoming,
			SelectedFormation = SelectedFormation
		};
	}
}
