using KingsManage.Web.Realtime;
using NUnit.Framework;

namespace KingsManage.Tests.Unit.Realtime;

public class RealtimeGroupsTests
{
	private static readonly Guid OrganizationId =
		Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid ClubId =
		Guid.Parse("22222222-2222-2222-2222-222222222222");
	private static readonly Guid UserId =
		Guid.Parse("33333333-3333-3333-3333-333333333333");

	[Test]
	public void OrganizationGroup_IsTenantScoped()
	{
		Assert.That(
			RealtimeGroups.Organization(OrganizationId),
			Is.EqualTo(
				"organization:11111111111111111111111111111111"
			)
		);
	}

	[Test]
	public void ClubGroup_IncludesOrganizationAndClub()
	{
		Assert.That(
			RealtimeGroups.Club(OrganizationId, ClubId),
			Is.EqualTo(
				"club:11111111111111111111111111111111:22222222222222222222222222222222"
			)
		);
	}

	[Test]
	public void UserGroup_IncludesTheFullTenantBoundary()
	{
		Assert.That(
			RealtimeGroups.User(OrganizationId, ClubId, UserId),
			Is.EqualTo(
				"user:11111111111111111111111111111111:22222222222222222222222222222222:33333333333333333333333333333333"
			)
		);
	}
}
