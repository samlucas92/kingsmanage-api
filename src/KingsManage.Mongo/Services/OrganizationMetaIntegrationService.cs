using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class OrganizationMetaIntegrationService : IOrganizationMetaIntegrationService
{
	private readonly IMongoCollection<OrganizationMetaIntegration> integrations;
	private readonly IMongoCollection<MetaOAuthState> oauthStates;
	private readonly ITenantContext tenant;

	public OrganizationMetaIntegrationService(MongoContext context, ITenantContext tenant)
	{
		integrations = context.Database.GetCollection<OrganizationMetaIntegration>("organizationMetaIntegrations");
		oauthStates = context.Database.GetCollection<MetaOAuthState>("metaOAuthStates");
		this.tenant = tenant;
	}

	public Task<OrganizationMetaIntegration?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
		GetByOrganizationAsync(tenant.OrganizationId, cancellationToken);

	public async Task<OrganizationMetaIntegration?> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
		await integrations.Find(item => item.OrganizationId == organizationId).FirstOrDefaultAsync(cancellationToken);

	public async Task<OrganizationMetaIntegration> SaveConnectionAsync(OrganizationMetaIntegration integration, CancellationToken cancellationToken = default)
	{
		var now = DateTime.UtcNow;
		var existing = await GetByOrganizationAsync(integration.OrganizationId, cancellationToken);
		integration.Id = existing?.Id ?? (integration.Id == Guid.Empty ? Guid.NewGuid() : integration.Id);
		integration.CreatedAt = existing?.CreatedAt ?? now;
		integration.CreatedByUserId = existing?.CreatedByUserId ?? integration.UpdatedByUserId;
		integration.UpdatedAt = now;
		await integrations.ReplaceOneAsync(
			item => item.OrganizationId == integration.OrganizationId,
			integration,
			new ReplaceOptions { IsUpsert = true },
			cancellationToken);
		return integration;
	}

	public async Task<OrganizationMetaIntegration?> UpdateConfigurationAsync(bool isEnabled, string timeZoneId, IReadOnlyList<SocialChannelMapping> mappings, Guid userId, CancellationToken cancellationToken = default)
	{
		var integration = await GetCurrentAsync(cancellationToken);
		if (integration is null) return null;
		integration.IsEnabled = isEnabled;
		integration.TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "Europe/London" : timeZoneId.Trim();
		integration.ClubMappings = mappings.ToList();
		integration.UpdatedByUserId = userId;
		integration.UpdatedAt = DateTime.UtcNow;
		await integrations.ReplaceOneAsync(item => item.Id == integration.Id, integration, cancellationToken: cancellationToken);
		return integration;
	}

	public async Task<OrganizationMetaIntegration?> SetEnabledAsync(bool isEnabled, Guid userId, CancellationToken cancellationToken = default)
	{
		var integration = await GetCurrentAsync(cancellationToken);
		if (integration is null) return null;
		integration.IsEnabled = isEnabled;
		integration.UpdatedByUserId = userId;
		integration.UpdatedAt = DateTime.UtcNow;
		await integrations.ReplaceOneAsync(item => item.Id == integration.Id, integration, cancellationToken: cancellationToken);
		return integration;
	}

	public async Task<bool> DisconnectAsync(CancellationToken cancellationToken = default) =>
		(await integrations.DeleteOneAsync(item => item.OrganizationId == tenant.OrganizationId, cancellationToken)).DeletedCount > 0;

	public async Task StoreOAuthStateAsync(MetaOAuthState state, CancellationToken cancellationToken = default) =>
		await oauthStates.InsertOneAsync(state, cancellationToken: cancellationToken);

	public async Task<bool> ConsumeOAuthStateAsync(Guid userId, string stateHash, CancellationToken cancellationToken = default)
	{
		var now = DateTime.UtcNow;
		var result = await oauthStates.UpdateOneAsync(
			item => item.OrganizationId == tenant.OrganizationId && item.UserId == userId && item.StateHash == stateHash && item.UsedAt == null && item.ExpiresAt > now,
			Builders<MetaOAuthState>.Update.Set(item => item.UsedAt, now),
			cancellationToken: cancellationToken);
		return result.ModifiedCount == 1;
	}
}
