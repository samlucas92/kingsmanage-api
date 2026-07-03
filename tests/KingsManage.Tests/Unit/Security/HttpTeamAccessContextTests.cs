using System.Security.Claims;
using KingsManage.Web.Security;
using Microsoft.AspNetCore.Http;

namespace KingsManage.Tests.Unit.Security;

[TestFixture]
public sealed class HttpTeamAccessContextTests
{
	[Test]
	public void TeamScopedUser_CanOnlyAccessClaimedTeams()
	{
		var allowedTeamId = Guid.NewGuid();
		var context = CreateContext(
			new Claim(HttpTeamAccessContext.TeamAccessClaim, allowedTeamId.ToString()));

		Assert.That(context.HasClubWideAccess, Is.False);
		Assert.That(context.CanAccessTeam(allowedTeamId), Is.True);
		Assert.That(context.CanAccessTeam(Guid.NewGuid()), Is.False);
	}

	[Test]
	public void ClubWideUser_CanAccessEveryTeam()
	{
		var context = CreateContext(
			new Claim(
				HttpTeamAccessContext.TeamAccessClaim,
				HttpTeamAccessContext.ClubWideAccessValue));

		Assert.That(context.HasClubWideAccess, Is.True);
		Assert.That(context.CanAccessTeam(Guid.NewGuid()), Is.True);
		Assert.That(context.CanAccessAnyTeam([Guid.NewGuid(), Guid.NewGuid()]), Is.True);
	}

	[Test]
	public void GeneralClubRecord_RemainsVisibleToTeamScopedUsers()
	{
		var context = CreateContext(
			new Claim(HttpTeamAccessContext.TeamAccessClaim, Guid.NewGuid().ToString()));

		Assert.That(context.CanAccessTeam(null), Is.True);
		Assert.That(context.CanAccessAnyTeam([]), Is.True);
	}

	private static HttpTeamAccessContext CreateContext(params Claim[] claims)
	{
		var httpContext = new DefaultHttpContext
		{
			User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
		};
		return new HttpTeamAccessContext(
			new HttpContextAccessor { HttpContext = httpContext });
	}
}
