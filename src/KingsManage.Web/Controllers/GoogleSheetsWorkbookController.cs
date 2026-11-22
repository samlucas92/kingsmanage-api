using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/google-sheets/workbook")]
public sealed class GoogleSheetsWorkbookController : ControllerBase
{
	private readonly IOrganizationGoogleSheetsIntegrationService integrations;
	private readonly IGoogleSheetsIntegrationClient google;
	private readonly IIntegrationSecretProtector protector;

	public GoogleSheetsWorkbookController(IOrganizationGoogleSheetsIntegrationService integrations, IGoogleSheetsIntegrationClient google, IIntegrationSecretProtector protector)
	{
		this.integrations = integrations; this.google = google; this.protector = protector;
	}

	[HttpGet]
	public async Task<ActionResult<GoogleSheetValuesViewModel>> Get([FromQuery] string? sheet, CancellationToken cancellationToken)
	{
		var integration = await integrations.GetCurrentAsync(cancellationToken);
		if (integration is null || string.IsNullOrWhiteSpace(integration.SpreadsheetId)) return NotFound("No Google Sheet has been connected for this organisation.");
		var selected = string.IsNullOrWhiteSpace(sheet) ? integration.Sheets.OrderBy(item => item.Index).FirstOrDefault()?.Title : sheet.Trim();
		if (string.IsNullOrWhiteSpace(selected) || !integration.Sheets.Any(item => item.Title == selected)) return BadRequest("Choose a valid sheet tab.");
		var token = await google.RefreshAccessTokenAsync(protector.Unprotect(integration.EncryptedRefreshToken), cancellationToken);
		var values = await google.GetValuesAsync(token.Value, integration.SpreadsheetId, selected, cancellationToken);
		return Ok(new GoogleSheetValuesViewModel
		{
			SpreadsheetName = integration.SpreadsheetName ?? "Google Sheet", SpreadsheetUrl = integration.SpreadsheetUrl ?? string.Empty,
			SheetName = selected, Sheets = integration.Sheets, Values = values, RefreshedAt = DateTime.UtcNow
		});
	}
}
