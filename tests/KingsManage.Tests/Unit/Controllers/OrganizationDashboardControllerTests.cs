using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public sealed class OrganizationDashboardControllerTests
{
	[Test]
	public async Task Get_ReturnsOrganizationTotals()
	{
		var controller = new OrganizationDashboardController(
			new StubDashboardService());

		var result = await controller.Get(null, CancellationToken.None);
		var dashboard = (result.Result as OkObjectResult)?.Value as OrganizationDashboard;

		Assert.That(dashboard?.ClubCount, Is.EqualTo(2));
		Assert.That(dashboard?.TeamCount, Is.EqualTo(5));
	}

	private sealed class StubDashboardService : IOrganizationDashboardService
	{
		public Task<OrganizationDashboard?> GetAsync(
			Guid? clubId = null,
			CancellationToken cancellationToken = default) =>
			Task.FromResult<OrganizationDashboard?>(new OrganizationDashboard
			{
				ClubCount = 2,
				TeamCount = 5
			});
	}
}
