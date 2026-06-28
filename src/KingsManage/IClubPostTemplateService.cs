namespace KingsManage;

public interface IClubPostTemplateService
{
	Task<IReadOnlyList<ClubPostTemplate>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<ClubPostTemplate> CreateAsync(ClubPostTemplate template, CancellationToken cancellationToken = default);
	Task<ClubPostTemplate?> UpdateAsync(ClubPostTemplate template, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
