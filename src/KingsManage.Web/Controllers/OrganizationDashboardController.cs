using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "OrganizationAdmin")]
[Route("api/organization/dashboard")]
public sealed class OrganizationDashboardController : ControllerBase
{
	private readonly IOrganizationDashboardService dashboard;

	public OrganizationDashboardController(IOrganizationDashboardService dashboard)
	{
		this.dashboard = dashboard;
	}

	[HttpGet]
	public async Task<ActionResult<OrganizationDashboard>> Get(
		[FromQuery] Guid? clubId,
		CancellationToken cancellationToken) =>
		await dashboard.GetAsync(clubId, cancellationToken) is { } dashboardResult
			? Ok(dashboardResult)
			: NotFound("Club was not found in this organization.");
}
