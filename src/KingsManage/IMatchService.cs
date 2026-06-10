namespace KingsManage;

public interface IMatchService
{
	Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<Match>> GetBySeasonAsync(
		string seasonId,
		CancellationToken cancellationToken = default
	);

	Task<Match?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

	Task<Match> CreateAsync(Match match, CancellationToken cancellationToken = default);

	Task<Match?> UpdateAsync(Match match, CancellationToken cancellationToken = default);

	Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

	Task<Match?> SetResultAsync(
		string id,
		MatchResult result,
		CancellationToken cancellationToken = default
	);

	Task<Match?> ClearResultAsync(string id, CancellationToken cancellationToken = default);

	Task<Match?> SetSelectedPlayersAsync(
		string id,
		List<SelectedPlayer> selectedPlayers,
		CancellationToken cancellationToken = default
	);

	Task<Match?> SetLineupFormationAsync(
		string id,
		LineupFormation formation,
		CancellationToken cancellationToken = default
	);

	Task<Match?> ToggleLineupLockedAsync(string id, CancellationToken cancellationToken = default);

	Task<Match?> UpdateNotesAsync(
		string id,
		MatchNotes notes,
		CancellationToken cancellationToken = default
	);

	Task<Match?> UpdatePlayerStatsAsync(
		string id,
		List<MatchPlayerStat> playerStats,
		CancellationToken cancellationToken = default
	);

	Task<Match?> PostponeAsync(
		string id,
		DateTime newDate,
		string? reason,
		CancellationToken cancellationToken = default
	);

	Task<Match?> RestoreAsync(string id, CancellationToken cancellationToken = default);
}