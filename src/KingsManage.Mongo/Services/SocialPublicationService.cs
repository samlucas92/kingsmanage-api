using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class SocialPublicationService : ISocialPublicationService
{
	private readonly IMongoCollection<SocialPublication> publications;
	private readonly TenantMongoScope tenant;

	public SocialPublicationService(MongoContext context, TenantMongoScope tenant)
	{
		publications = context.Database.GetCollection<SocialPublication>("socialPublications");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<SocialPublication>> GetCurrentClubAsync(int limit = 50, CancellationToken cancellationToken = default) =>
		await publications.Find(tenant.Filter<SocialPublication>()).SortByDescending(item => item.CreatedAt).Limit(Math.Clamp(limit, 1, 100)).ToListAsync(cancellationToken);

	public async Task<SocialPublication?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
		await publications.Find(tenant.Filter<SocialPublication>(item => item.Id == id)).FirstOrDefaultAsync(cancellationToken);

	public async Task<SocialPublication> CreateAsync(SocialPublication publication, CancellationToken cancellationToken = default)
	{
		publication.Id = publication.Id == Guid.Empty ? Guid.NewGuid() : publication.Id;
		publication.CreatedAt = publication.UpdatedAt = DateTime.UtcNow;
		tenant.Assign(publication);
		await publications.InsertOneAsync(publication, cancellationToken: cancellationToken);
		return publication;
	}

	public async Task<SocialPublication?> AttachFileAsync(Guid id, Guid fileId, CancellationToken cancellationToken = default)
	{
		var publication = await GetAsync(id, cancellationToken);
		if (publication is null || publication.Status != SocialPublicationStatus.Draft) return null;
		publication.FileId = fileId;
		publication.UpdatedAt = DateTime.UtcNow;
		await publications.ReplaceOneAsync(tenant.Filter<SocialPublication>(item => item.Id == id), publication, cancellationToken: cancellationToken);
		return publication;
	}

	public async Task<SocialPublication?> QueueAsync(Guid id, SocialPublicationMode mode, CancellationToken cancellationToken = default)
	{
		var publication = await GetAsync(id, cancellationToken);
		if (publication is null || publication.Status != SocialPublicationStatus.Draft || publication.FileId is null || mode == SocialPublicationMode.YepsetDraft || publication.Deliveries.Count == 0) return null;
		if (mode == SocialPublicationMode.FacebookDraft)
		{
			if (!publication.Deliveries.Any(item => item.Platform == SocialPlatform.Facebook)) return null;
			foreach (var delivery in publication.Deliveries.Where(item => item.Platform == SocialPlatform.Instagram)) delivery.Status = SocialDeliveryStatus.Saved;
		}
		publication.Mode = mode;
		publication.Status = SocialPublicationStatus.Scheduled;
		publication.ScheduledForUtc = DateTime.UtcNow;
		publication.UpdatedAt = DateTime.UtcNow;
		await publications.ReplaceOneAsync(tenant.Filter<SocialPublication>(item => item.Id == id), publication, cancellationToken: cancellationToken);
		return publication;
	}

	public async Task<SocialPublication?> CancelAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var publication = await GetAsync(id, cancellationToken);
		if (publication is null || publication.Status is SocialPublicationStatus.Published or SocialPublicationStatus.MetaDraft or SocialPublicationStatus.Cancelled) return null;
		publication.Status = SocialPublicationStatus.Cancelled;
		foreach (var delivery in publication.Deliveries.Where(item => item.Status is not (SocialDeliveryStatus.Published or SocialDeliveryStatus.Drafted))) delivery.Status = SocialDeliveryStatus.Cancelled;
		publication.UpdatedAt = DateTime.UtcNow;
		await publications.ReplaceOneAsync(tenant.Filter<SocialPublication>(item => item.Id == id), publication, cancellationToken: cancellationToken);
		return publication;
	}

	public async Task<SocialPublication?> RetryAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var publication = await GetAsync(id, cancellationToken);
		if (publication is null || publication.Status is not (SocialPublicationStatus.Failed or SocialPublicationStatus.PartiallyPublished)) return null;
		foreach (var delivery in publication.Deliveries.Where(item => item.Status == SocialDeliveryStatus.Failed))
		{
			delivery.Status = SocialDeliveryStatus.Pending;
			delivery.NextAttemptAt = null;
			delivery.LastError = null;
		}
		publication.Status = SocialPublicationStatus.Scheduled;
		publication.ScheduledForUtc = DateTime.UtcNow;
		publication.UpdatedAt = DateTime.UtcNow;
		await publications.ReplaceOneAsync(tenant.Filter<SocialPublication>(item => item.Id == id), publication, cancellationToken: cancellationToken);
		return publication;
	}

	public async Task<SocialPublication?> LeaseDueAsync(CancellationToken cancellationToken = default)
	{
		var now = DateTime.UtcNow;
		var leaseId = Guid.NewGuid();
		var filter = Builders<SocialPublication>.Filter.In(item => item.Status, [SocialPublicationStatus.Scheduled, SocialPublicationStatus.Processing]) &
			Builders<SocialPublication>.Filter.Lte(item => item.ScheduledForUtc, now) &
			(Builders<SocialPublication>.Filter.Eq(item => item.LeaseExpiresAt, null) | Builders<SocialPublication>.Filter.Lt(item => item.LeaseExpiresAt, now));
		var update = Builders<SocialPublication>.Update
			.Set(item => item.LeaseId, leaseId)
			.Set(item => item.LeaseExpiresAt, now.AddMinutes(5))
			.Set(item => item.Status, SocialPublicationStatus.Processing)
			.Set(item => item.UpdatedAt, now);
		return await publications.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<SocialPublication> { ReturnDocument = ReturnDocument.After, Sort = Builders<SocialPublication>.Sort.Ascending(item => item.ScheduledForUtc) }, cancellationToken);
	}

	public Task CompleteDeliveryAsync(Guid publicationId, Guid leaseId, SocialPlatform platform, string providerPostId, CancellationToken cancellationToken = default) =>
		UpdateDeliveryAsync(publicationId, leaseId, platform, true, providerPostId, null, null, cancellationToken);

	public Task FailDeliveryAsync(Guid publicationId, Guid leaseId, SocialPlatform platform, string error, DateTime? retryAt, CancellationToken cancellationToken = default) =>
		UpdateDeliveryAsync(publicationId, leaseId, platform, false, null, error, retryAt, cancellationToken);

	private async Task UpdateDeliveryAsync(Guid publicationId, Guid leaseId, SocialPlatform platform, bool succeeded, string? providerPostId, string? error, DateTime? retryAt, CancellationToken cancellationToken)
	{
		var publication = await publications.Find(item => item.Id == publicationId && item.LeaseId == leaseId).FirstOrDefaultAsync(cancellationToken);
		if (publication is null) return;
		var delivery = publication.Deliveries.First(item => item.Platform == platform);
		delivery.AttemptCount++;
		delivery.LastAttemptAt = DateTime.UtcNow;
		delivery.Status = succeeded
			? publication.Mode == SocialPublicationMode.FacebookDraft ? SocialDeliveryStatus.Drafted : SocialDeliveryStatus.Published
			: retryAt is not null ? SocialDeliveryStatus.Pending : SocialDeliveryStatus.Failed;
		delivery.ProviderPostId = providerPostId;
		delivery.LastError = error;
		delivery.NextAttemptAt = retryAt;
		var published = publication.Deliveries.Count(item => item.Status == SocialDeliveryStatus.Published);
		var drafted = publication.Deliveries.Count(item => item.Status is SocialDeliveryStatus.Drafted or SocialDeliveryStatus.Saved);
		var pending = publication.Deliveries.Count(item => item.Status == SocialDeliveryStatus.Pending);
		publication.Status = publication.Mode == SocialPublicationMode.FacebookDraft && drafted == publication.Deliveries.Count
			? SocialPublicationStatus.MetaDraft
			: published == publication.Deliveries.Count
			? SocialPublicationStatus.Published
			: pending > 0 ? retryAt is not null ? SocialPublicationStatus.Scheduled : SocialPublicationStatus.Processing
			: published > 0 ? SocialPublicationStatus.PartiallyPublished : SocialPublicationStatus.Failed;
		if (retryAt is not null) publication.ScheduledForUtc = retryAt;
		publication.PublishedAt = publication.Status == SocialPublicationStatus.Published ? DateTime.UtcNow : null;
		if (pending == 0 || retryAt is not null)
		{
			publication.LeaseId = null;
			publication.LeaseExpiresAt = null;
		}
		publication.UpdatedAt = DateTime.UtcNow;
		await publications.ReplaceOneAsync(item => item.Id == publication.Id && item.LeaseId == leaseId, publication, cancellationToken: cancellationToken);
	}
}
