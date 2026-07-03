using KingsManage;

namespace KingsManage.Web.Security;

public sealed class HttpTeamAccessContext : ITeamAccessContext
{
	public const string TeamAccessClaim = "teamAccess";
	public const string ClubWideAccessValue = "*";

	private readonly IHttpContextAccessor _httpContextAccessor;

	public HttpTeamAccessContext(IHttpContextAccessor httpContextAccessor)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	public bool HasClubWideAccess
	{
		get
		{
			var user = _httpContextAccessor.HttpContext?.User;
			return user is not null &&
				(user.HasClaim(HttpTenantContext.PlatformAdminClaim, "true") ||
				 user.HasClaim(TeamAccessClaim, ClubWideAccessValue));
		}
	}

	public IReadOnlySet<Guid> TeamIds =>
		(_httpContextAccessor.HttpContext?.User.FindAll(TeamAccessClaim) ?? [])
			.Select(claim => Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty)
			.Where(id => id != Guid.Empty)
			.ToHashSet();

	public bool CanAccessTeam(Guid? teamId) =>
		HasClubWideAccess || !teamId.HasValue || TeamIds.Contains(teamId.Value);

	public bool CanAccessAnyTeam(IEnumerable<Guid> teamIds)
	{
		var ids = teamIds.Distinct().ToList();
		return HasClubWideAccess || ids.Count == 0 || ids.Any(TeamIds.Contains);
	}
}
