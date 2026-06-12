using KingsManage;

namespace KingsManage.Web.Models;

public class PlayerMatchViewModel
{
	public Guid Id { get; set; }

	public Guid? SeasonId { get; set; }

	public ClubTeam Team { get; set; }

	public string Opponent { get; set; } = string.Empty;

	public DateTime Date { get; set; }

	public MatchVenue Venue { get; set; }

	public MatchState State { get; set; }

	public MatchResult? Result { get; set; }

	public bool IsCompleted { get; set; }

	public MatchPlayerStat? PlayerStat { get; set; }

	public static PlayerMatchViewModel FromMatch(Match match, Guid playerId)
	{
		return new PlayerMatchViewModel
		{
			Id = match.Id,
			SeasonId = match.SeasonId,
			Team = match.Team,
			Opponent = match.Opponent,
			Date = match.Date,
			Venue = match.Venue,
			State = match.State,
			Result = match.Result,
			IsCompleted = match.IsCompleted,
			PlayerStat = match.PlayerStats.FirstOrDefault(stat => stat.PlayerId == playerId)
		};
	}
}
