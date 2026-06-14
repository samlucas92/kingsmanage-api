using KingsManage;

namespace KingsManage.Web.Models;

public class CreateMatchForEventModel
{
	public Guid? SeasonId { get; set; }
	public ClubTeam Team { get; set; }
	public string Opponent { get; set; } = string.Empty;
	public DateTime? Date { get; set; }
	public MatchVenue Venue { get; set; }
	public LineupFormation SelectedFormation { get; set; } = LineupFormation.FourThreeThree;

	public Match ToMatch(DateTime eventStartDateTime)
	{
		return new Match
		{
			SeasonId = SeasonId,
			Team = Team,
			Opponent = Opponent,
			Date = Date ?? eventStartDateTime,
			Venue = Venue,
			State = MatchState.Upcoming,
			SelectedFormation = SelectedFormation
		};
	}
}
