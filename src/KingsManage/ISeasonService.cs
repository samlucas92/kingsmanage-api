namespace KingsManage;

public interface ISeasonService
{
	Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<Season?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

	Task<Season?> GetActiveAsync(CancellationToken cancellationToken = default);

	Task<Season> CreateAsync(Season season, CancellationToken cancellationToken = default);

	Task<Season?> UpdateAsync(Season season, CancellationToken cancellationToken = default);

	Task<Season?> SetActiveAsync(string id, CancellationToken cancellationToken = default);
}