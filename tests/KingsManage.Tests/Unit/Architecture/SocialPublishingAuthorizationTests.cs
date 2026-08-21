using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace KingsManage.Tests.Unit.Architecture;

[TestFixture]
public sealed class SocialPublishingAuthorizationTests
{
	[Test]
	public void IntegrationConfiguration_IsOrganizationAdminOnly() =>
		AssertPolicy(typeof(OrganizationIntegrationsController), "OrganizationAdmin");

	[Test]
	public void SocialPublishing_IsClubAdminOnly() =>
		AssertPolicy(typeof(SocialPublicationsController), "ClubAdmin");

	private static void AssertPolicy(Type controllerType, string expectedPolicy)
	{
		var policies = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true)
			.Cast<AuthorizeAttribute>()
			.Select(attribute => attribute.Policy);
		Assert.That(policies, Does.Contain(expectedPolicy));
	}
}
