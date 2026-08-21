namespace KingsManage;

public enum OrganizationIntegrationStatus
{
	NotConfigured,
	Connected,
	NeedsAttention
}

public enum SocialPlatform
{
	Facebook,
	Instagram
}

public enum SocialPublicationStatus
{
	Draft,
	Scheduled,
	Processing,
	MetaDraft,
	Published,
	PartiallyPublished,
	Failed,
	Cancelled
}

public enum SocialDeliveryStatus
{
	Pending,
	Processing,
	Saved,
	Drafted,
	Published,
	Failed,
	Cancelled
}

public enum SocialPublicationMode
{
	YepsetDraft,
	PublishNow,
	FacebookDraft
}

public sealed class MetaInstagramAccount
{
	public string Id { get; set; } = string.Empty;
	public string Username { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? ProfilePictureUrl { get; set; }
}

public sealed class MetaPageConnection
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string EncryptedAccessToken { get; set; } = string.Empty;
	public List<string> Tasks { get; set; } = [];
	public MetaInstagramAccount? InstagramAccount { get; set; }
}

public sealed class SocialChannelMapping
{
	public Guid ClubId { get; set; }
	public bool FacebookEnabled { get; set; }
	public string? FacebookPageId { get; set; }
	public bool InstagramEnabled { get; set; }
	public string? InstagramAccountId { get; set; }
}

public sealed class OrganizationMetaIntegration
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public bool IsEnabled { get; set; }
	public OrganizationIntegrationStatus Status { get; set; }
	public string? ConnectedMetaUserId { get; set; }
	public string? ConnectedMetaUserName { get; set; }
	public string EncryptedUserAccessToken { get; set; } = string.Empty;
	public List<string> GrantedScopes { get; set; } = [];
	public DateTime? TokenExpiresAt { get; set; }
	public DateTime? LastValidatedAt { get; set; }
	public string? LastError { get; set; }
	public string TimeZoneId { get; set; } = "Europe/London";
	public List<MetaPageConnection> Pages { get; set; } = [];
	public List<SocialChannelMapping> ClubMappings { get; set; } = [];
	public Guid CreatedByUserId { get; set; }
	public Guid UpdatedByUserId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}

public sealed class MetaOAuthState
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public Guid UserId { get; set; }
	public string StateHash { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public DateTime? UsedAt { get; set; }
}

public sealed class SocialPublicationDelivery
{
	public SocialPlatform Platform { get; set; }
	public string DestinationId { get; set; } = string.Empty;
	public string DestinationName { get; set; } = string.Empty;
	public SocialDeliveryStatus Status { get; set; } = SocialDeliveryStatus.Pending;
	public string? ProviderPostId { get; set; }
	public int AttemptCount { get; set; }
	public DateTime? LastAttemptAt { get; set; }
	public DateTime? NextAttemptAt { get; set; }
	public string? LastError { get; set; }
}

public sealed class SocialPublication : ITenantOwned
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid CreatedByUserId { get; set; }
	public Guid? FileId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? GraphicKind { get; set; }
	public string? TemplateId { get; set; }
	public string? EditorStateJson { get; set; }
	public string FacebookCaption { get; set; } = string.Empty;
	public string InstagramCaption { get; set; } = string.Empty;
	public DateTime? ScheduledForUtc { get; set; }
	public SocialPublicationMode Mode { get; set; } = SocialPublicationMode.YepsetDraft;
	public SocialPublicationStatus Status { get; set; } = SocialPublicationStatus.Draft;
	public List<SocialPublicationDelivery> Deliveries { get; set; } = [];
	public Guid? LeaseId { get; set; }
	public DateTime? LeaseExpiresAt { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
	public DateTime? PublishedAt { get; set; }
}

public sealed record SocialDestination(
	SocialPlatform Platform,
	string Id,
	string Name,
	string? Username = null);

public interface IOrganizationMetaIntegrationService
{
	Task<OrganizationMetaIntegration?> GetCurrentAsync(CancellationToken cancellationToken = default);
	Task<OrganizationMetaIntegration?> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
	Task<OrganizationMetaIntegration> SaveConnectionAsync(OrganizationMetaIntegration integration, CancellationToken cancellationToken = default);
	Task<OrganizationMetaIntegration?> UpdateConfigurationAsync(bool isEnabled, string timeZoneId, IReadOnlyList<SocialChannelMapping> mappings, Guid userId, CancellationToken cancellationToken = default);
	Task<OrganizationMetaIntegration?> SetEnabledAsync(bool isEnabled, Guid userId, CancellationToken cancellationToken = default);
	Task<bool> DisconnectAsync(CancellationToken cancellationToken = default);
	Task StoreOAuthStateAsync(MetaOAuthState state, CancellationToken cancellationToken = default);
	Task<bool> ConsumeOAuthStateAsync(Guid userId, string stateHash, CancellationToken cancellationToken = default);
}

public interface ISocialPublicationService
{
	Task<IReadOnlyList<SocialPublication>> GetCurrentClubAsync(int limit = 50, CancellationToken cancellationToken = default);
	Task<SocialPublication?> GetAsync(Guid id, CancellationToken cancellationToken = default);
	Task<SocialPublication> CreateAsync(SocialPublication publication, CancellationToken cancellationToken = default);
	Task<SocialPublication?> AttachFileAsync(Guid id, Guid fileId, CancellationToken cancellationToken = default);
	Task<SocialPublication?> QueueAsync(Guid id, SocialPublicationMode mode, CancellationToken cancellationToken = default);
	Task<SocialPublication?> CancelAsync(Guid id, CancellationToken cancellationToken = default);
	Task<SocialPublication?> RetryAsync(Guid id, CancellationToken cancellationToken = default);
	Task<SocialPublication?> LeaseDueAsync(CancellationToken cancellationToken = default);
	Task CompleteDeliveryAsync(Guid publicationId, Guid leaseId, SocialPlatform platform, string providerPostId, CancellationToken cancellationToken = default);
	Task FailDeliveryAsync(Guid publicationId, Guid leaseId, SocialPlatform platform, string error, DateTime? retryAt, CancellationToken cancellationToken = default);
}
