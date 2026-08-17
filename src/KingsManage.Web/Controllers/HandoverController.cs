using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Route("api/handover")]
[Authorize(Policy = "TeamManagement")]
public sealed class HandoverController : ControllerBase
{
	private readonly IHandoverVaultService vault;

	public HandoverController(IHandoverVaultService vault)
	{
		this.vault = vault;
	}

	[HttpGet]
	public async Task<ActionResult<HandoverVaultSnapshot>> Get(CancellationToken cancellationToken)
	{
		var userId = CurrentUserId();
		return Ok(await vault.GetSnapshotAsync(userId, IsAdmin(), cancellationToken));
	}

	[HttpGet("roles/{id:guid}")]
	public async Task<ActionResult<OperationalRole>> GetRole(Guid id, CancellationToken cancellationToken)
	{
		var role = await vault.GetRoleAsync(id, cancellationToken);
		if (role is null) return NotFound();
		if (!IsAdmin() && role.PrimaryOwnerUserId != CurrentUserId() && !role.SupportingOwnerUserIds.Contains(CurrentUserId())) return Forbid();
		return Ok(role);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost("roles")]
	public async Task<ActionResult<OperationalRole>> SaveRole(OperationalRole role, CancellationToken cancellationToken) =>
		Ok(await vault.SaveRoleAsync(role, CurrentUserId(), cancellationToken));

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost("responsibilities")]
	public async Task<ActionResult<RoleResponsibility>> SaveResponsibility(RoleResponsibility responsibility, CancellationToken cancellationToken) =>
		Ok(await vault.SaveResponsibilityAsync(responsibility, CurrentUserId(), cancellationToken));

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost("document-links")]
	public async Task<ActionResult<HandoverDocumentLink>> LinkDocument(HandoverDocumentLink link, CancellationToken cancellationToken) =>
		Ok(await vault.LinkDocumentAsync(link, CurrentUserId(), cancellationToken));

	[Authorize(Policy = "ClubAdmin")]
	[HttpDelete("document-links/{id:guid}")]
	public async Task<IActionResult> UnlinkDocument(Guid id, CancellationToken cancellationToken) =>
		await vault.UnlinkDocumentAsync(id, CurrentUserId(), cancellationToken) ? NoContent() : NotFound();

	[HttpPost("tasks")]
	public async Task<ActionResult<OperationalTask>> SaveTask(OperationalTask task, CancellationToken cancellationToken)
	{
		if (!IsAdmin())
		{
			if (task.Id == Guid.Empty) return Forbid();
			var snapshot = await vault.GetSnapshotAsync(CurrentUserId(), false, cancellationToken);
			var existing = snapshot.Tasks.FirstOrDefault(item => item.Id == task.Id && item.AssignedUserIds.Contains(CurrentUserId()));
			if (existing is null) return NotFound();
			existing.Status = task.Status;
			existing.CompletionNotes = task.CompletionNotes;
			task = existing;
		}
		return Ok(await vault.SaveTaskAsync(task, CurrentUserId(), cancellationToken));
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost("contacts")]
	public async Task<ActionResult<OperationalContact>> SaveContact(OperationalContact contact, CancellationToken cancellationToken) =>
		Ok(await vault.SaveContactAsync(contact, CurrentUserId(), cancellationToken));

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost("records")]
	public async Task<ActionResult<HandoverRecord>> CreateHandover(CreateHandoverModel model, CancellationToken cancellationToken)
	{
		var handover = await vault.CreateHandoverAsync(new HandoverRecord
		{
			OperationalRoleId = model.OperationalRoleId,
			OutgoingUserId = model.OutgoingUserId,
			IncomingUserId = model.IncomingUserId,
			DueAt = model.DueAt,
			Notes = model.Notes
		}, model.AccessTransfers, model.AdditionalItems, CurrentUserId(), cancellationToken);
		return CreatedAtAction(nameof(GetHandover), new { id = handover.Id }, handover);
	}

	[HttpGet("records/{id:guid}")]
	public async Task<ActionResult<HandoverRecord>> GetHandover(Guid id, CancellationToken cancellationToken)
	{
		var handover = await vault.GetHandoverAsync(id, CurrentUserId(), IsAdmin(), cancellationToken);
		return handover is null ? NotFound() : Ok(handover);
	}

	[HttpPost("records/{handoverId:guid}/items/{itemId:guid}/confirm")]
	public async Task<ActionResult<HandoverRecord>> ConfirmItem(Guid handoverId, Guid itemId, ConfirmHandoverItemModel model, CancellationToken cancellationToken)
	{
		var handover = await vault.ConfirmItemAsync(handoverId, itemId, CurrentUserId(), model.Confirmed, model.Notes, cancellationToken);
		return handover is null ? NotFound() : Ok(handover);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost("records/{handoverId:guid}/items/{itemId:guid}/status")]
	public async Task<ActionResult<HandoverRecord>> SetItemStatus(Guid handoverId, Guid itemId, SetHandoverItemStatusModel model, CancellationToken cancellationToken)
	{
		if (model.Status == HandoverItemStatus.Confirmed) return BadRequest("Participant confirmations must use the confirmation action.");
		var handover = await vault.SetItemStatusAsync(handoverId, itemId, model.Status, CurrentUserId(), model.Notes, cancellationToken);
		return handover is null ? NotFound() : Ok(handover);
	}

	[HttpPost("records/{id:guid}/status")]
	public async Task<ActionResult<HandoverRecord>> SetStatus(Guid id, SetHandoverStatusModel model, CancellationToken cancellationToken)
	{
		if (model.Status != HandoverStatus.ReadyForReview && !IsAdmin()) return Forbid();
		if (!IsAdmin() && await vault.GetHandoverAsync(id, CurrentUserId(), false, cancellationToken) is null) return NotFound();
		var handover = await vault.SetHandoverStatusAsync(id, model.Status, CurrentUserId(), cancellationToken);
		return handover is null ? NotFound() : Ok(handover);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost("records/{id:guid}/refresh")]
	public async Task<ActionResult<HandoverRecord>> Refresh(Guid id, CancellationToken cancellationToken)
	{
		var handover = await vault.RefreshHandoverAsync(id, CurrentUserId(), cancellationToken);
		return handover is null ? NotFound() : Ok(handover);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpGet("audit/{entityId:guid}")]
	public async Task<ActionResult<IReadOnlyList<HandoverAuditEntry>>> GetAudit(Guid entityId, CancellationToken cancellationToken) =>
		Ok(await vault.GetAuditAsync(entityId, cancellationToken));

	private Guid CurrentUserId()
	{
		var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
		if (Guid.TryParse(claim, out var id)) return id;
		throw new UnauthorizedAccessException("Current user id is unavailable.");
	}

	private bool IsAdmin() =>
		User.HasClaim(HttpTenantContext.PlatformAdminClaim, "true") ||
		User.HasClaim(HttpTenantContext.TenantRoleClaim, TenantRole.OrganizationAdmin.ToString()) ||
		User.HasClaim(HttpTenantContext.TenantRoleClaim, TenantRole.ClubAdmin.ToString());
}
