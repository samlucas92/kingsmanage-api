namespace KingsManage;

public interface IPlayerService
{
	Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<Player?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

	Task<Player> CreateAsync(Player player, CancellationToken cancellationToken = default);

	Task<Player?> UpdateAsync(Player player, CancellationToken cancellationToken = default);

	Task<Player?> SetActiveAsync(
		string id,
		bool isActive,
		CancellationToken cancellationToken = default
	);
}