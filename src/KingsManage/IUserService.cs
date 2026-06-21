namespace KingsManage;

public interface IUserService
{
	Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

	Task<AppUser> CreateAsync(
		AppUser user,
		string password,
		CancellationToken cancellationToken = default
	);

	Task<AppUser?> UpdateAsync(AppUser user, CancellationToken cancellationToken = default);

	Task<AppUser?> SetActiveAsync(
		Guid id,
		bool isActive,
		CancellationToken cancellationToken = default
	);

	Task<AppUser?> ValidateCredentialsAsync(
		string email,
		string password,
		CancellationToken cancellationToken = default
	);

	Task<AppUser?> SetDefaultClubAsync(
		Guid id,
		Guid clubId,
		CancellationToken cancellationToken = default
	) => Task.FromResult<AppUser?>(null);

	Task<bool> ChangePasswordAsync(
		Guid id,
		string currentPassword,
		string newPassword,
		CancellationToken cancellationToken = default
	);

	Task<bool> ResetPasswordAsync(
		Guid id,
		string newPassword,
		CancellationToken cancellationToken = default
	);

	Task<AppUser> EnsureDefaultAdminUserAsync(
		string email,
		string password,
		CancellationToken cancellationToken = default
	);
}
