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
[Route("api/organization/integrations")]
public sealed class OrganizationIntegrationsController : ControllerBase
{
	private readonly IOrganizationMetaIntegrationService integrations;
	private readonly IMetaGraphClient meta;
	private readonly IIntegrationSecretProtector protector;
	private readonly ITenantContext tenant;
	private readonly ISportsClubService clubs;

	public OrganizationIntegrationsController(
		IOrganizationMetaIntegrationService integrations,
		IMetaGraphClient meta,
		IIntegrationSecretProtector protector,
		ITenantContext tenant,
		ISportsClubService clubs)
	{
		this.integrations = integrations;
		this.meta = meta;
		this.protector = protector;
		this.tenant = tenant;
		this.clubs = clubs;
	}

	[HttpGet("meta")]
	public async Task<ActionResult<MetaIntegrationViewModel>> Get(CancellationToken cancellationToken) =>
		Ok(ToViewModel(await integrations.GetCurrentAsync(cancellationToken)));

	[HttpPost("meta/connect/start")]
	public async Task<ActionResult> StartConnection(CancellationToken cancellationToken)
	{
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		await integrations.StoreOAuthStateAsync(new MetaOAuthState
		{
			Id = Guid.NewGuid(),
			OrganizationId = tenant.OrganizationId,
			UserId = userId.Value,
			StateHash = Hash(state),
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddMinutes(10)
		}, cancellationToken);
		return Ok(new { authorizationUrl = meta.BuildAuthorizationUrl(state) });
	}

	[HttpPost("meta/connect/complete")]
	public async Task<ActionResult<MetaIntegrationViewModel>> CompleteConnection(CompleteMetaConnectionRequest request, CancellationToken cancellationToken)
	{
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.State)) return BadRequest("Meta did not return a valid authorization response.");
		if (!await integrations.ConsumeOAuthStateAsync(userId.Value, Hash(request.State), cancellationToken)) return BadRequest("The Meta connection request has expired or has already been used.");
		var authorization = await meta.CompleteAuthorizationAsync(request.Code.Trim(), cancellationToken);
		var existing = await integrations.GetCurrentAsync(cancellationToken);
		var pageIds = authorization.Pages.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
		var saved = await integrations.SaveConnectionAsync(new OrganizationMetaIntegration
		{
			Id = existing?.Id ?? Guid.NewGuid(),
			OrganizationId = tenant.OrganizationId,
			IsEnabled = existing?.IsEnabled ?? false,
			Status = OrganizationIntegrationStatus.Connected,
			ConnectedMetaUserId = authorization.MetaUserId,
			ConnectedMetaUserName = authorization.MetaUserName,
			EncryptedUserAccessToken = protector.Protect(authorization.UserAccessToken),
			TokenExpiresAt = authorization.TokenExpiresAt,
			LastValidatedAt = DateTime.UtcNow,
			TimeZoneId = existing?.TimeZoneId ?? "Europe/London",
			Pages = authorization.Pages.Select(page => new MetaPageConnection
			{
				Id = page.Id,
				Name = page.Name,
				EncryptedAccessToken = protector.Protect(page.AccessToken),
				Tasks = page.Tasks.ToList(),
				InstagramAccount = page.InstagramAccount
			}).ToList(),
			ClubMappings = (existing?.ClubMappings ?? []).Where(mapping => mapping.FacebookPageId is null || pageIds.Contains(mapping.FacebookPageId)).ToList(),
			CreatedByUserId = existing?.CreatedByUserId ?? userId.Value,
			UpdatedByUserId = userId.Value
		}, cancellationToken);
		return Ok(ToViewModel(saved));
	}

	[HttpPut("meta/configuration")]
	public async Task<ActionResult<MetaIntegrationViewModel>> UpdateConfiguration(UpdateMetaConfigurationRequest request, CancellationToken cancellationToken)
	{
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		var integration = await integrations.GetCurrentAsync(cancellationToken);
		if (integration is null) return NotFound();
		var error = await ValidateMappingsAsync(request.ClubMappings, integration, cancellationToken);
		if (error is not null) return BadRequest(error);
		var updated = await integrations.UpdateConfigurationAsync(request.IsEnabled, request.TimeZoneId, request.ClubMappings, userId.Value, cancellationToken);
		return Ok(ToViewModel(updated));
	}

	[HttpPatch("meta/enabled")]
	public async Task<ActionResult<MetaIntegrationViewModel>> SetEnabled(SetIntegrationEnabledRequest request, CancellationToken cancellationToken)
	{
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		var current = await integrations.GetCurrentAsync(cancellationToken);
		if (current is null) return NotFound();
		if (request.IsEnabled && !current.ClubMappings.Any(item => item.FacebookEnabled || item.InstagramEnabled)) return BadRequest("Configure at least one club destination before enabling Meta publishing.");
		return Ok(ToViewModel(await integrations.SetEnabledAsync(request.IsEnabled, userId.Value, cancellationToken)));
	}

	[HttpPost("meta/validate")]
	public async Task<ActionResult<MetaIntegrationViewModel>> Validate(CancellationToken cancellationToken)
	{
		var current = await integrations.GetCurrentAsync(cancellationToken);
		if (current is null) return NotFound();
		try
		{
			await meta.ValidateAsync(protector.Unprotect(current.EncryptedUserAccessToken), cancellationToken);
			current.Status = OrganizationIntegrationStatus.Connected;
			current.LastValidatedAt = DateTime.UtcNow;
			current.LastError = null;
		}
		catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or CryptographicException)
		{
			current.Status = OrganizationIntegrationStatus.NeedsAttention;
			current.LastError = exception.Message;
		}
		current.UpdatedByUserId = GetCurrentUserId() ?? current.UpdatedByUserId;
		await integrations.SaveConnectionAsync(current, cancellationToken);
		return Ok(ToViewModel(current));
	}

	[HttpDelete("meta")]
	public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
	{
		await integrations.DisconnectAsync(cancellationToken);
		return NoContent();
	}

	private async Task<string?> ValidateMappingsAsync(IReadOnlyList<SocialChannelMapping> mappings, OrganizationMetaIntegration integration, CancellationToken cancellationToken)
	{
		if (mappings.GroupBy(item => item.ClubId).Any(group => group.Count() > 1)) return "Each club can only be mapped once.";
		var validClubs = (await clubs.GetAllAsync(cancellationToken)).Select(item => item.Id).ToHashSet();
		foreach (var mapping in mappings)
		{
			if (!validClubs.Contains(mapping.ClubId)) return "A selected club does not belong to this organisation.";
			var page = integration.Pages.FirstOrDefault(item => item.Id == mapping.FacebookPageId);
			if (mapping.FacebookEnabled && page is null) return "Select a valid Facebook Page for every enabled Facebook destination.";
			if (mapping.InstagramEnabled && (page?.InstagramAccount?.Id != mapping.InstagramAccountId)) return "Select the Instagram account connected to the chosen Facebook Page.";
		}
		return null;
	}

	private static MetaIntegrationViewModel ToViewModel(OrganizationMetaIntegration? integration) => new()
	{
		IsConfigured = integration is not null,
		IsEnabled = integration?.IsEnabled ?? false,
		Status = integration?.Status ?? OrganizationIntegrationStatus.NotConfigured,
		ConnectedMetaUserName = integration?.ConnectedMetaUserName,
		TokenExpiresAt = integration?.TokenExpiresAt,
		LastValidatedAt = integration?.LastValidatedAt,
		LastError = integration?.LastError,
		TimeZoneId = integration?.TimeZoneId ?? "Europe/London",
		Pages = integration?.Pages.Select(item => new MetaPageViewModel { Id = item.Id, Name = item.Name, Tasks = item.Tasks, InstagramAccount = item.InstagramAccount }).ToList() ?? [],
		ClubMappings = integration?.ClubMappings ?? []
	};

	private Guid? GetCurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;
	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
