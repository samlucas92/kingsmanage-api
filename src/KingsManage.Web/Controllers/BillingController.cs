using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "OrganizationAdmin")]
[Route("api/billing")]
public sealed class BillingController : ControllerBase
{
	private readonly IBillingService billing;
	private readonly ITenantContext tenant;

	public BillingController(IBillingService billing, ITenantContext tenant)
	{
		this.billing = billing;
		this.tenant = tenant;
	}

	[HttpGet("subscription")]
	public async Task<ActionResult<OrganizationSubscription>> GetSubscription(
		CancellationToken cancellationToken) =>
		Ok(await billing.GetCurrentAsync(cancellationToken));

	[HttpPut("subscription")]
	public async Task<ActionResult<OrganizationSubscription>> UpdateSubscription(
		SubscriptionUpdate update,
		CancellationToken cancellationToken)
	{
		try
		{
			return Ok(await billing.UpdateCurrentAsync(update, cancellationToken));
		}
		catch (ArgumentException exception)
		{
			return BadRequest(exception.Message);
		}
	}

	[HttpGet("invoices")]
	public async Task<ActionResult<IReadOnlyList<BillingInvoice>>> GetInvoices(
		CancellationToken cancellationToken) =>
		Ok(await billing.GetInvoicesAsync(tenant.OrganizationId, cancellationToken));
}
