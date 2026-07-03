namespace KingsManage;

public interface ITeamAccessContext
{
	bool HasClubWideAccess { get; }
	IReadOnlySet<Guid> TeamIds { get; }
	bool CanAccessTeam(Guid? teamId);
	bool CanAccessAnyTeam(IEnumerable<Guid> teamIds);
}
