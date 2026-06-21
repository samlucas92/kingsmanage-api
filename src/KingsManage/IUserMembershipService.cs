namespace KingsManage;

public sealed class MembershipClubOption
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public List<MembershipTeamOption> Teams { get; set; } = [];
}

public sealed class MembershipTeamOption
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}

public interface IUserMembershipService
{
	Task<IReadOnlyList<MembershipClubOption>> GetOptionsAsync(CancellationToken cancellationToken = default);
	Task<AppUser?> UpdateAsync(Guid userId, IReadOnlyList<UserMembership> memberships, Guid? defaultClubId, CancellationToken cancellationToken = default);
}
