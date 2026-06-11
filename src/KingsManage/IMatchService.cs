namespace KingsManage;

public interface IMatchService
{
	Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<IReadOnlyList<Match>> GetBySeasonAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	);
	Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Match> CreateAsync(Match match, CancellationToken cancellationToken = default);
	Task<Match?> UpdateAsync(Match match, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Match?> SetResultAsync(
		Guid id,
		MatchResult result,
		CancellationToken cancellationToken = default
	);
	Task<Match?> ClearResultAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Match?> SetSelectedPlayersAsync(
		Guid id,
		List<SelectedPlayer> selectedPlayers,
		CancellationToken cancellationToken = default
	);
	Task<Match?> SetLineupFormationAsync(
		Guid id,
		LineupFormation formation,
		CancellationToken cancellationToken = default
	);
	Task<Match?> ToggleLineupLockedAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Match?> UpdateNotesAsync(
		Guid id,
		MatchNotes notes,
		CancellationToken cancellationToken = default
	);
	Task<Match?> UpdatePlayerStatsAsync(
		Guid id,
		List<MatchPlayerStats> playerStats,
		CancellationToken cancellationToken = default
	);
	Task<Match?> PostponeAsync(
		Guid id,
		DateTime newDate,
		string? reason,
		CancellationToken cancellationToken = default
	);
	Task<Match?> RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
