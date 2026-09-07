namespace KingsManage;

public interface IOppositionTeamService
{
	Task<IReadOnlyList<OppositionTeam>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<OppositionTeam?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<OppositionTeam> CreateAsync(OppositionTeam team, CancellationToken cancellationToken = default);
	Task<OppositionTeam?> UpdateAsync(OppositionTeam team, CancellationToken cancellationToken = default);
	Task<OppositionTeam?> SetBadgeFileAsync(Guid id, Guid? badgeFileId, CancellationToken cancellationToken = default);
}
