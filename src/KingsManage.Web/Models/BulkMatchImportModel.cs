using KingsManage;

namespace KingsManage.Web.Models;

public class BulkMatchImportModel
{
	public Guid SeasonId { get; set; }
	public bool CreateEvents { get; set; }
	public List<BulkMatchImportItemModel> Matches { get; set; } = [];
}

public class BulkMatchImportItemModel
{
	public Guid TeamId { get; set; }
	public ClubTeam Team { get; set; }
	public string TeamName { get; set; } = string.Empty;
	public string Opponent { get; set; } = string.Empty;
	public string Competition { get; set; } = string.Empty;
	public DateTime Date { get; set; }
	public MatchVenue Venue { get; set; }
	public string Location { get; set; } = string.Empty;
	public string FormationKey { get; set; } = string.Empty;

	public Match ToMatch(Guid seasonId, Guid? clubEventId = null)
	{
		return new Match
		{
			SeasonId = seasonId,
			ClubEventId = clubEventId,
			TeamId = TeamId,
			Team = Team,
			Opponent = Opponent,
			Competition = Competition,
			Date = Date,
			Venue = Venue,
			Location = Location,
			FormationKey = FormationKey,
			State = MatchState.Upcoming
		};
	}
}

public class BulkMatchImportResultModel
{
	public int MatchCount { get; set; }
	public int EventCount { get; set; }
}
