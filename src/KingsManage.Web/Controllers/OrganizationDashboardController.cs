using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "OrganizationAdmin")]
[Route("api/organization/dashboard")]
public sealed class OrganizationDashboardController : ControllerBase
{
	private readonly IOrganizationDashboardService _dashboard;

	public OrganizationDashboardController(IOrganizationDashboardService dashboard)
	{
		_dashboard = dashboard;
	}

	[HttpGet]
	public async Task<ActionResult<OrganizationDashboard>> Get(
		[FromQuery] Guid? clubId,
		CancellationToken cancellationToken) =>
		await _dashboard.GetAsync(clubId, cancellationToken) is { } dashboard
			? Ok(dashboard)
			: NotFound("Club was not found in this organization.");
}
