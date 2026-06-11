namespace KingsManage;

public interface IPlayerService
{
	Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Player> CreateAsync(Player player, CancellationToken cancellationToken = default);
	Task<Player?> UpdateAsync(Player player, CancellationToken cancellationToken = default);
	Task<Player?> SetActiveAsync(
		Guid id,
		bool isActive,
		CancellationToken cancellationToken = default
	);
}
