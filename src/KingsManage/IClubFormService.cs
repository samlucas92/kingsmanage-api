namespace KingsManage;

public interface IClubFormService
{
	Task<IReadOnlyList<ClubForm>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<ClubForm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	Task<ClubForm?> GetByGoCodeAsync(string goCode, CancellationToken cancellationToken = default);

	Task<ClubForm> CreateAsync(ClubForm form, CancellationToken cancellationToken = default);

	Task<ClubForm?> UpdateAsync(ClubForm form, CancellationToken cancellationToken = default);

	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

	Task<bool> HasSubmittedAsync(Guid formId, Guid userId, CancellationToken cancellationToken = default);

	Task<bool> HasSubmittedAsync(Guid formId, string respondentKey, CancellationToken cancellationToken = default);

	Task<bool> HasSubmittedAsync(ClubForm form, string respondentKey, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<ClubFormSubmission>> GetSubmissionsAsync(Guid formId, CancellationToken cancellationToken = default);

	Task<ClubFormSubmission> SubmitAsync(ClubFormSubmission submission, CancellationToken cancellationToken = default);
}
