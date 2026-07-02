namespace KingsManage;

public interface ISportsClubService
{
	Task<IReadOnlyList<SportsClub>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<SportsClub?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<SportsClub> CreateAsync(SportsClub club, CancellationToken cancellationToken = default);
	Task<SportsClub?> UpdateAsync(Guid id, SportsClub club, CancellationToken cancellationToken = default);
	Task<SportsClub?> SetLogoFileAsync(Guid id, Guid? logoFileId, CancellationToken cancellationToken = default);
	Task<SportsClub?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
