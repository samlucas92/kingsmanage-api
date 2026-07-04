using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "SiteAdmin")]
[Route("api/platform/billing")]
public sealed class PlatformBillingController : ControllerBase
{
	private readonly IBillingService _billing;

	public PlatformBillingController(IBillingService billing)
	{
		_billing = billing;
	}

	[HttpGet("{organizationId:guid}")]
	public async Task<ActionResult<OrganizationSubscription>> Get(
		Guid organizationId,
		CancellationToken cancellationToken) =>
		Ok(await _billing.GetByOrganizationAsync(organizationId, cancellationToken));

	[HttpPatch("{organizationId:guid}/status")]
	public async Task<ActionResult<OrganizationSubscription>> SetStatus(
		Guid organizationId,
		SubscriptionStatusUpdate update,
		CancellationToken cancellationToken) =>
		Ok(await _billing.SetStatusAsync(organizationId, update, cancellationToken));

	[HttpGet("{organizationId:guid}/invoices")]
	public async Task<ActionResult<IReadOnlyList<BillingInvoice>>> GetInvoices(
		Guid organizationId,
		CancellationToken cancellationToken) =>
		Ok(await _billing.GetInvoicesAsync(organizationId, cancellationToken));

	[HttpPost("{organizationId:guid}/invoices")]
	public async Task<ActionResult<BillingInvoice>> AddInvoice(
		Guid organizationId,
		BillingInvoice invoice,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(invoice.Number))
			return BadRequest("Invoice number is required.");
		if (invoice.Amount < 0)
			return BadRequest("Invoice amount cannot be negative.");
		invoice.OrganizationId = organizationId;
		var created = await _billing.AddInvoiceAsync(invoice, cancellationToken);
		return Created($"/api/platform/billing/{organizationId}/invoices", created);
	}
}
