namespace KingsManage;

public interface IPlayerSponsorshipService
{
	Task<IReadOnlyList<PlayerSponsorship>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<PlayerSponsorship?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<PlayerSponsorship?> GetByGoCodeAsync(string goCode, CancellationToken cancellationToken = default);
	Task<PlayerSponsorship> CreateAsync(PlayerSponsorship sponsorship, CancellationToken cancellationToken = default);
	Task<PlayerSponsorship?> UpdateAsync(PlayerSponsorship sponsorship, CancellationToken cancellationToken = default);
	Task<PlayerSponsorship?> SubmitAsync(PlayerSponsorship sponsorship, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
