using KingsManage;
using KingsManage.Mongo;
using KingsManage.Web.Models;
using MongoDB.Driver;

namespace KingsManage.Web.Services;

public sealed class SocialPublishingBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory scopeFactory;
	private readonly MetaIntegrationSettings settings;
	private readonly ILogger<SocialPublishingBackgroundService> logger;

	public SocialPublishingBackgroundService(IServiceScopeFactory scopeFactory, MetaIntegrationSettings settings, ILogger<SocialPublishingBackgroundService> logger)
	{
		this.scopeFactory = scopeFactory;
		this.settings = settings;
		this.logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(settings.PollIntervalSeconds, 5, 300)));
		do { await PublishOneAsync(stoppingToken); }
		while (await timer.WaitForNextTickAsync(stoppingToken));
	}

	private async Task PublishOneAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			var publications = scope.ServiceProvider.GetRequiredService<ISocialPublicationService>();
			var publication = await publications.LeaseDueAsync(cancellationToken);
			if (publication?.FileId is not Guid fileId || publication.LeaseId is not Guid leaseId) return;
			var context = scope.ServiceProvider.GetRequiredService<MongoContext>();
			var file = await context.Database.GetCollection<ClubFile>("files").Find(item => item.Id == fileId && item.OrganizationId == publication.OrganizationId && item.ClubId == publication.ClubId && item.Status == ClubFileStatus.Uploaded).FirstOrDefaultAsync(cancellationToken);
			if (file is null)
			{
				foreach (var delivery in publication.Deliveries.Where(item => item.Status == SocialDeliveryStatus.Pending))
					await publications.FailDeliveryAsync(publication.Id, leaseId, delivery.Platform, "The publication image is unavailable.", null, cancellationToken);
				return;
			}
			var integrationService = scope.ServiceProvider.GetRequiredService<IOrganizationMetaIntegrationService>();
			var integration = await integrationService.GetByOrganizationAsync(publication.OrganizationId, cancellationToken);
			if (integration is null || !integration.IsEnabled || integration.Status != OrganizationIntegrationStatus.Connected)
			{
				foreach (var delivery in publication.Deliveries.Where(item => item.Status == SocialDeliveryStatus.Pending))
					await publications.FailDeliveryAsync(publication.Id, leaseId, delivery.Platform, "Meta publishing is disabled or needs attention.", null, cancellationToken);
				return;
			}
			var page = integration.Pages.FirstOrDefault(item => publication.Deliveries.Any(delivery => delivery.Platform == SocialPlatform.Facebook && delivery.DestinationId == item.Id) || publication.Deliveries.Any(delivery => delivery.Platform == SocialPlatform.Instagram && delivery.DestinationId == item.InstagramAccount?.Id));
			if (page is null)
			{
				foreach (var delivery in publication.Deliveries.Where(item => item.Status == SocialDeliveryStatus.Pending))
					await publications.FailDeliveryAsync(publication.Id, leaseId, delivery.Platform, "The configured Meta destination is no longer available.", null, cancellationToken);
				return;
			}
			var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
			var media = await storage.CreateDownloadUrlAsync(file.StorageKey, TimeSpan.FromHours(2), cancellationToken);
			var protector = scope.ServiceProvider.GetRequiredService<IIntegrationSecretProtector>();
			var meta = scope.ServiceProvider.GetRequiredService<IMetaGraphClient>();
			var pageToken = protector.Unprotect(page.EncryptedAccessToken);
			foreach (var delivery in publication.Deliveries.Where(item => item.Status == SocialDeliveryStatus.Pending).ToList())
			{
				try
				{
					var externalId = delivery.Platform == SocialPlatform.Facebook
						? publication.Mode == SocialPublicationMode.FacebookDraft
							? await meta.CreateFacebookDraftPhotoAsync(page.Id, pageToken, media.Url, publication.FacebookCaption, cancellationToken)
							: await meta.PublishFacebookPhotoAsync(page.Id, pageToken, media.Url, publication.FacebookCaption, cancellationToken)
						: await meta.PublishInstagramImageAsync(delivery.DestinationId, pageToken, media.Url, publication.InstagramCaption, cancellationToken);
					await publications.CompleteDeliveryAsync(publication.Id, leaseId, delivery.Platform, externalId, cancellationToken);
				}
				catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException)
				{
					var retryAt = delivery.AttemptCount < 2 ? DateTime.UtcNow.AddMinutes(Math.Pow(5, delivery.AttemptCount + 1)) : (DateTime?)null;
					await publications.FailDeliveryAsync(publication.Id, leaseId, delivery.Platform, exception.Message, retryAt, cancellationToken);
					logger.LogWarning("Meta {Platform} delivery failed for publication {PublicationId}: {Message}", delivery.Platform, publication.Id, exception.Message);
					break;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
		catch (Exception exception)
		{
			logger.LogError(exception, "Social publishing worker failed.");
		}
	}
}
