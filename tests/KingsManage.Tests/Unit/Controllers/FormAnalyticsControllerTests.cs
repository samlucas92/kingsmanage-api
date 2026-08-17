using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace KingsManage.Tests.Unit.Controllers;

public sealed class FormAnalyticsControllerTests
{
	[TestCase(nameof(FormAnalyticsController.GetOverview))]
	[TestCase(nameof(FormAnalyticsController.GetFormAnalytics))]
	public void ReportsRequireTeamManagement(string methodName)
	{
		var method = typeof(FormAnalyticsController).GetMethod(methodName)!;
		var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
			.Cast<AuthorizeAttribute>()
			.Single();

		Assert.That(authorize.Policy, Is.EqualTo("TeamManagement"));
	}

	[Test]
	public void PublicTrackingAllowsAnonymousFormVisitors()
	{
		var method = typeof(FormAnalyticsController).GetMethod(nameof(FormAnalyticsController.TrackPublic))!;

		Assert.That(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true), Is.Not.Empty);
	}
}
