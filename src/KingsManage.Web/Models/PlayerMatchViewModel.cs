using KingsManage;

namespace KingsManage.Web.Models;

public class PlayerMatchViewModel
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

	public MatchPlayerStats? PlayerStat { get; set; }

	public static PlayerMatchViewModel FromMatch(Match match, Guid playerId)
	{
		return new PlayerMatchViewModel
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
			PlayerStat = match.PlayerStats.FirstOrDefault(stat => stat.PlayerId == playerId)
		};
	}
}
