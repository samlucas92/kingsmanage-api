using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Route("api/forms")]
public sealed class FormAnalyticsController : ControllerBase
{
	private readonly IClubFormService formService;
	private readonly IFormAnalyticsService analyticsService;

	public FormAnalyticsController(IClubFormService formService, IFormAnalyticsService analyticsService)
	{
		this.formService = formService;
		this.analyticsService = analyticsService;
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpGet("analytics")]
	public async Task<ActionResult<FormAnalyticsOverviewViewModel>> GetOverview(
		[FromQuery] DateTime? from,
		[FromQuery] DateTime? to,
		CancellationToken cancellationToken)
	{
		if (!IsValidRange(from, to)) return BadRequest("The analytics date range is invalid.");
		return Ok(FormAnalyticsOverviewViewModel.FromReport(
			await analyticsService.GetOverviewAsync(from, to, cancellationToken)));
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpGet("{id:guid}/analytics")]
	public async Task<ActionResult<FormAnalyticsDetailViewModel>> GetFormAnalytics(
		Guid id,
		[FromQuery] DateTime? from,
		[FromQuery] DateTime? to,
		CancellationToken cancellationToken)
	{
		if (!IsValidRange(from, to)) return BadRequest("The analytics date range is invalid.");
		var report = await analyticsService.GetFormAnalyticsAsync(id, from, to, cancellationToken);
		return report is null ? NotFound() : Ok(FormAnalyticsDetailViewModel.FromReport(report));
	}

	[Authorize]
	[HttpPost("{id:guid}/analytics/{eventName}")]
	public async Task<IActionResult> TrackAuthenticated(
		Guid id,
		string eventName,
		FormAnalyticsTrackModel model,
		CancellationToken cancellationToken)
	{
		var form = await formService.GetByIdAsync(id, cancellationToken);
		if (form is null || (form.Status != ClubFormStatus.Open && !CanManageForms())) return NotFound();
		return await TrackAsync(form, eventName, model, TryGetCurrentUserId(), cancellationToken);
	}

	[AllowAnonymous]
	[HttpPost("go/{goCode}/analytics/{eventName}")]
	public async Task<IActionResult> TrackPublic(
		string goCode,
		string eventName,
		FormAnalyticsTrackModel model,
		CancellationToken cancellationToken)
	{
		var form = await formService.GetByGoCodeAsync(goCode, cancellationToken);
		if (form is null || form.Status != ClubFormStatus.Open) return NotFound();
		var userId = TryGetCurrentUserId();
		if (!form.AllowAnonymousResponses && !userId.HasValue) return Unauthorized();
		return await TrackAsync(form, eventName, model, userId, cancellationToken);
	}

	private async Task<IActionResult> TrackAsync(
		ClubForm form,
		string eventName,
		FormAnalyticsTrackModel model,
		Guid? userId,
		CancellationToken cancellationToken)
	{
		if (model.SessionId == Guid.Empty) return BadRequest("Analytics session id is required.");
		switch (eventName.Trim().ToLowerInvariant())
		{
			case "view":
				await analyticsService.RecordViewAsync(form, model.SessionId, userId, cancellationToken);
				break;
			case "interaction":
				await analyticsService.RecordInteractionAsync(form, model.SessionId, userId, cancellationToken);
				break;
			case "field-interaction":
				if (!model.FieldId.HasValue || model.FieldId == Guid.Empty) return BadRequest("Field id is required.");
				await analyticsService.RecordFieldInteractionAsync(form, model.SessionId, model.FieldId.Value, userId, cancellationToken);
				break;
			case "validation-error":
				await analyticsService.RecordValidationErrorAsync(form, model.SessionId, model.FieldId, model.ErrorType, userId, cancellationToken);
				break;
			case "duration":
				await analyticsService.UpdateDurationAsync(form, model.SessionId, model.EngagedDurationMs, userId, cancellationToken);
				break;
			default:
				return NotFound();
		}
		return NoContent();
	}

	private Guid? TryGetCurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty
		? id
		: null;
	private bool CanManageForms() => User.IsInRole(UserRole.Admin.ToString()) || User.IsInRole(UserRole.Coach.ToString());
	private static bool IsValidRange(DateTime? from, DateTime? to) => !from.HasValue || !to.HasValue || from.Value <= to.Value;
}
