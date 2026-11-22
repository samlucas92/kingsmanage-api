using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "OrganizationAdmin")]
[Route("api/organization/integrations/google-sheets")]
public sealed class OrganizationGoogleSheetsIntegrationsController : ControllerBase
{
	private readonly IOrganizationGoogleSheetsIntegrationService integrations;
	private readonly IGoogleSheetsIntegrationClient google;
	private readonly IIntegrationSecretProtector protector;
	private readonly GoogleSheetsIntegrationSettings settings;
	private readonly ITenantContext tenant;

	public OrganizationGoogleSheetsIntegrationsController(IOrganizationGoogleSheetsIntegrationService integrations, IGoogleSheetsIntegrationClient google, IIntegrationSecretProtector protector, GoogleSheetsIntegrationSettings settings, ITenantContext tenant)
	{
		this.integrations = integrations;
		this.google = google;
		this.protector = protector;
		this.settings = settings;
		this.tenant = tenant;
	}

	[HttpGet]
	public async Task<ActionResult<GoogleSheetsIntegrationViewModel>> Get(CancellationToken cancellationToken) =>
		Ok(ToViewModel(await integrations.GetCurrentAsync(cancellationToken)));

	[HttpPost("connect/start")]
	public async Task<ActionResult> StartConnection(CancellationToken cancellationToken)
	{
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		await integrations.StoreOAuthStateAsync(new GoogleSheetsOAuthState
		{
			Id = Guid.NewGuid(), OrganizationId = tenant.OrganizationId, UserId = userId.Value,
			StateHash = Hash(state), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(10)
		}, cancellationToken);
		return Ok(new { authorizationUrl = google.BuildAuthorizationUrl(state) });
	}

	[HttpPost("connect/complete")]
	public async Task<ActionResult<GoogleSheetsIntegrationViewModel>> CompleteConnection(CompleteGoogleSheetsConnectionRequest request, CancellationToken cancellationToken)
	{
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.State)) return BadRequest("Google did not return a valid authorization response.");
		if (!await integrations.ConsumeOAuthStateAsync(userId.Value, Hash(request.State), cancellationToken)) return BadRequest("The Google connection request has expired or has already been used.");
		var authorization = await google.CompleteAuthorizationAsync(request.Code.Trim(), cancellationToken);
		var existing = await integrations.GetCurrentAsync(cancellationToken);
		var saved = await integrations.SaveAsync(new OrganizationGoogleSheetsIntegration
		{
			Id = existing?.Id ?? Guid.NewGuid(), OrganizationId = tenant.OrganizationId,
			Status = OrganizationIntegrationStatus.Connected,
			ConnectedGoogleUserEmail = authorization.Email,
			EncryptedRefreshToken = protector.Protect(authorization.RefreshToken),
			SpreadsheetId = existing?.SpreadsheetId, SpreadsheetName = existing?.SpreadsheetName,
			SpreadsheetUrl = existing?.SpreadsheetUrl, Sheets = existing?.Sheets ?? [],
			LastValidatedAt = DateTime.UtcNow, CreatedByUserId = existing?.CreatedByUserId ?? userId.Value,
			UpdatedByUserId = userId.Value
		}, cancellationToken);
		return Ok(ToViewModel(saved));
	}

	[HttpGet("picker-token")]
	public async Task<ActionResult<GooglePickerTokenViewModel>> GetPickerToken(CancellationToken cancellationToken)
	{
		var integration = await integrations.GetCurrentAsync(cancellationToken);
		if (integration is null) return NotFound("Connect Google before choosing a spreadsheet.");
		if (string.IsNullOrWhiteSpace(settings.PickerApiKey) || string.IsNullOrWhiteSpace(settings.PickerAppId)) return BadRequest("Google Picker is not configured on the API.");
		var token = await google.RefreshAccessTokenAsync(protector.Unprotect(integration.EncryptedRefreshToken), cancellationToken);
		return Ok(new GooglePickerTokenViewModel { AccessToken = token.Value, ExpiresAt = token.ExpiresAt, ApiKey = settings.PickerApiKey, AppId = settings.PickerAppId });
	}

	[HttpPut("spreadsheet")]
	public async Task<ActionResult<GoogleSheetsIntegrationViewModel>> SelectSpreadsheet(SelectGoogleSpreadsheetRequest request, CancellationToken cancellationToken)
	{
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		if (string.IsNullOrWhiteSpace(request.SpreadsheetId)) return BadRequest("Choose a Google Sheet.");
		var integration = await integrations.GetCurrentAsync(cancellationToken);
		if (integration is null) return NotFound("Connect Google before choosing a spreadsheet.");
		var token = await google.RefreshAccessTokenAsync(protector.Unprotect(integration.EncryptedRefreshToken), cancellationToken);
		var spreadsheet = await google.GetSpreadsheetAsync(token.Value, request.SpreadsheetId.Trim(), cancellationToken);
		integration.SpreadsheetId = spreadsheet.Id;
		integration.SpreadsheetName = spreadsheet.Name;
		integration.SpreadsheetUrl = spreadsheet.Url;
		integration.Sheets = spreadsheet.Sheets.ToList();
		integration.Status = OrganizationIntegrationStatus.Connected;
		integration.LastValidatedAt = DateTime.UtcNow;
		integration.LastError = null;
		integration.UpdatedByUserId = userId.Value;
		return Ok(ToViewModel(await integrations.SaveAsync(integration, cancellationToken)));
	}

	[HttpPost("validate")]
	public async Task<ActionResult<GoogleSheetsIntegrationViewModel>> Validate(CancellationToken cancellationToken)
	{
		var integration = await integrations.GetCurrentAsync(cancellationToken);
		if (integration is null) return NotFound();
		try
		{
			var token = await google.RefreshAccessTokenAsync(protector.Unprotect(integration.EncryptedRefreshToken), cancellationToken);
			if (!string.IsNullOrWhiteSpace(integration.SpreadsheetId))
			{
				var sheet = await google.GetSpreadsheetAsync(token.Value, integration.SpreadsheetId, cancellationToken);
				integration.SpreadsheetName = sheet.Name; integration.SpreadsheetUrl = sheet.Url; integration.Sheets = sheet.Sheets.ToList();
			}
			integration.Status = OrganizationIntegrationStatus.Connected; integration.LastError = null; integration.LastValidatedAt = DateTime.UtcNow;
		}
		catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or CryptographicException)
		{
			integration.Status = OrganizationIntegrationStatus.NeedsAttention; integration.LastError = exception.Message;
		}
		integration.UpdatedByUserId = GetCurrentUserId() ?? integration.UpdatedByUserId;
		return Ok(ToViewModel(await integrations.SaveAsync(integration, cancellationToken)));
	}

	[HttpDelete]
	public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
	{
		await integrations.DisconnectAsync(cancellationToken);
		return NoContent();
	}

	private static GoogleSheetsIntegrationViewModel ToViewModel(OrganizationGoogleSheetsIntegration? value) => new()
	{
		IsConfigured = value is not null, Status = value?.Status ?? OrganizationIntegrationStatus.NotConfigured,
		ConnectedGoogleUserEmail = value?.ConnectedGoogleUserEmail, SpreadsheetId = value?.SpreadsheetId,
		SpreadsheetName = value?.SpreadsheetName, SpreadsheetUrl = value?.SpreadsheetUrl,
		Sheets = value?.Sheets ?? [], LastValidatedAt = value?.LastValidatedAt, LastError = value?.LastError
	};

	private Guid? GetCurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;
	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
