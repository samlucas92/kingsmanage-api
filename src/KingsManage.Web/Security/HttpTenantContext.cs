using System.Security.Claims;
using KingsManage;

namespace KingsManage.Web.Security;

public sealed class HttpTenantContext : ITenantContext
{
	public const string OrganizationClaim = "organizationId";
	public const string ClubClaim = "clubId";

	private readonly IHttpContextAccessor _httpContextAccessor;

	public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	public Guid OrganizationId => ReadRequiredClaim(OrganizationClaim);

	public Guid ClubId => ReadRequiredClaim(ClubClaim);

	private Guid ReadRequiredClaim(string claimType)
	{
		var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
		if (Guid.TryParse(value, out var id) && id != Guid.Empty)
		{
			return id;
		}

		throw new InvalidOperationException($"The authenticated request does not contain a valid {claimType} claim.");
	}
}
