using KingsManage;

namespace KingsManage.Web.Models;

public class MatchViewModel
{
	public Guid Id { get; set; }

	public Guid? SeasonId { get; set; }

	public Guid? ClubEventId { get; set; }

	public ClubTeam Team { get; set; }

	public string Opponent { get; set; } = string.Empty;

	public string Competition { get; set; } = string.Empty;

	public DateTime Date { get; set; }

	public MatchVenue Venue { get; set; }

	public string Location { get; set; } = string.Empty;

	public MatchState State { get; set; }

	public MatchResult? Result { get; set; }

	public bool IsCompleted { get; set; }

	public bool IsLineupLocked { get; set; }

	public LineupFormation SelectedFormation { get; set; }

	public static MatchViewModel FromMatch(Match match)
	{
		return new MatchViewModel
		{
			Id = match.Id,
			SeasonId = match.SeasonId,
			ClubEventId = match.ClubEventId,
			Team = match.Team,
			Opponent = match.Opponent,
			Competition = match.Competition,
			Date = match.Date,
			Venue = match.Venue,
			Location = match.Location,
			State = match.State,
			Result = match.Result,
			IsCompleted = match.IsCompleted,
			IsLineupLocked = match.IsLineupLocked,
			SelectedFormation = match.SelectedFormation
		};
	}
}
