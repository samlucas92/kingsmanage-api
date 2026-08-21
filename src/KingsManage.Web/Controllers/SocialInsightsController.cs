using System.Security.Cryptography;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/social-insights")]
public sealed class SocialInsightsController : ControllerBase
{
	private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
	private readonly IOrganizationMetaIntegrationService integrations;
	private readonly IMetaGraphClient meta;
	private readonly IIntegrationSecretProtector protector;
	private readonly ITenantContext tenant;
	private readonly IMemoryCache cache;

	public SocialInsightsController(
		IOrganizationMetaIntegrationService integrations,
		IMetaGraphClient meta,
		IIntegrationSecretProtector protector,
		ITenantContext tenant,
		IMemoryCache cache)
	{
		this.integrations = integrations;
		this.meta = meta;
		this.protector = protector;
		this.tenant = tenant;
		this.cache = cache;
	}

	[HttpGet]
	public async Task<ActionResult<SocialInsightsOverview>> GetOverview([FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
	{
		var destination = await GetDestinationAsync(cancellationToken);
		if (destination.Error is not null) return Conflict(new { message = destination.Error });
		var cacheKey = $"social-insights:{tenant.OrganizationId}:{tenant.ClubId}";
		if (!refresh && cache.TryGetValue(cacheKey, out SocialInsightsOverview? cached) && cached is not null) return Ok(Filter(cached, destination.Mapping!));

		try
		{
			var overview = await meta.GetInsightsOverviewAsync(destination.Page!, destination.AccessToken!, cancellationToken);
			cache.Set(cacheKey, overview, CacheDuration);
			return Ok(Filter(overview, destination.Mapping!));
		}
		catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or CryptographicException)
		{
			return Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway, title: "Meta insights are unavailable");
		}
	}

	[HttpGet("posts/{platform}/{postId}")]
	public async Task<ActionResult<SocialPostInsightsDetail>> GetPost(SocialPlatform platform, string postId, CancellationToken cancellationToken)
	{
		var destination = await GetDestinationAsync(cancellationToken);
		if (destination.Error is not null) return Conflict(new { message = destination.Error });
		if (!IsEnabled(platform, destination.Mapping!)) return NotFound(new { message = "That social channel is not enabled for this club." });

		try
		{
			var overview = await GetOrLoadOverviewAsync(destination, cancellationToken);
			if (!overview.Posts.Any(item => item.Platform == platform && item.Id == postId)) return NotFound(new { message = "That post is not available for this club." });
			var cacheKey = $"social-insights-post:{tenant.OrganizationId}:{tenant.ClubId}:{platform}:{postId}";
			if (cache.TryGetValue(cacheKey, out SocialPostInsightsDetail? cached) && cached is not null) return Ok(cached);
			var detail = await meta.GetPostInsightsAsync(platform, postId, destination.AccessToken!, cancellationToken);
			cache.Set(cacheKey, detail, CacheDuration);
			return Ok(detail);
		}
		catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or CryptographicException)
		{
			return Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway, title: "Meta insights are unavailable");
		}
	}

	private async Task<SocialInsightsOverview> GetOrLoadOverviewAsync(MetaDestination destination, CancellationToken cancellationToken)
	{
		var cacheKey = $"social-insights:{tenant.OrganizationId}:{tenant.ClubId}";
		if (cache.TryGetValue(cacheKey, out SocialInsightsOverview? cached) && cached is not null) return Filter(cached, destination.Mapping!);
		var overview = await meta.GetInsightsOverviewAsync(destination.Page!, destination.AccessToken!, cancellationToken);
		cache.Set(cacheKey, overview, CacheDuration);
		return Filter(overview, destination.Mapping!);
	}

	private async Task<MetaDestination> GetDestinationAsync(CancellationToken cancellationToken)
	{
		var integration = await integrations.GetCurrentAsync(cancellationToken);
		if (integration is null || !integration.IsEnabled || integration.Status != OrganizationIntegrationStatus.Connected)
			return new("Connect and enable Meta in Organisation integrations before viewing insights.");
		var mapping = integration.ClubMappings.FirstOrDefault(item => item.ClubId == tenant.ClubId);
		if (mapping is null || (!mapping.FacebookEnabled && !mapping.InstagramEnabled))
			return new("Configure a Facebook or Instagram destination for this club before viewing insights.");
		var page = integration.Pages.FirstOrDefault(item => item.Id == mapping.FacebookPageId);
		if (page is null) return new("The configured Facebook Page is no longer available. Reconfigure the Meta integration.");
		if (mapping.InstagramEnabled && page.InstagramAccount?.Id != mapping.InstagramAccountId)
			return new("The configured Instagram account is no longer available. Reconfigure the Meta integration.");
		try
		{
			return new(mapping, page, protector.Unprotect(page.EncryptedAccessToken));
		}
		catch (Exception exception) when (exception is CryptographicException or InvalidOperationException)
		{
			return new("The Meta connection credential cannot be read. Reconnect Meta in Organisation integrations.");
		}
	}

	private static SocialInsightsOverview Filter(SocialInsightsOverview overview, SocialChannelMapping mapping) => new()
	{
		GeneratedAt = overview.GeneratedAt,
		Accounts = overview.Accounts.Where(item => IsEnabled(item.Platform, mapping)).ToList(),
		Posts = overview.Posts.Where(item => IsEnabled(item.Platform, mapping)).ToList()
	};

	private static bool IsEnabled(SocialPlatform platform, SocialChannelMapping mapping) => platform switch
	{
		SocialPlatform.Facebook => mapping.FacebookEnabled,
		SocialPlatform.Instagram => mapping.InstagramEnabled,
		_ => false
	};

	private sealed record MetaDestination(SocialChannelMapping? Mapping, MetaPageConnection? Page, string? AccessToken, string? Error)
	{
		public MetaDestination(string error) : this(null, null, null, error) { }
		public MetaDestination(SocialChannelMapping mapping, MetaPageConnection page, string accessToken) : this(mapping, page, accessToken, null) { }
	}
}
