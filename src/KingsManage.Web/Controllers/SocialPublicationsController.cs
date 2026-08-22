using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "ClubAdmin")]
[Route("api/social-publications")]
public sealed class SocialPublicationsController : ControllerBase
{
	private readonly ISocialPublicationService publications;
	private readonly IOrganizationMetaIntegrationService integrations;
	private readonly IClubFileService files;
	private readonly IStoredFileObjectService storedObjects;
	private readonly IFileLifecycleService fileLifecycle;
	private readonly ITenantContext tenant;

	public SocialPublicationsController(ISocialPublicationService publications, IOrganizationMetaIntegrationService integrations, IClubFileService files, IStoredFileObjectService storedObjects, IFileLifecycleService fileLifecycle, ITenantContext tenant)
	{
		this.publications = publications;
		this.integrations = integrations;
		this.files = files;
		this.storedObjects = storedObjects;
		this.fileLifecycle = fileLifecycle;
		this.tenant = tenant;
	}

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
	{
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		var deleted = await publications.DeleteUnsentAsync(id, cancellationToken);
		if (deleted is null) return Conflict("Only content that has never been sent to Meta can be deleted.");
		if (deleted.FileId is Guid fileId && await files.GetByIdAsync(fileId, cancellationToken) is { } file)
		{
			if (await files.SoftDeleteAsync(fileId, userId.Value, cancellationToken))
			{
				if (file.StoredObjectId is Guid storedObjectId) await storedObjects.DecrementReferenceCountAsync(storedObjectId, cancellationToken);
				await fileLifecycle.RecordAuditAsync(new FileLifecycleAudit
				{
					OrganizationId = file.OrganizationId,
					ClubId = file.ClubId,
					FileId = file.Id,
					StoredObjectId = file.StoredObjectId,
					UserId = userId.Value,
					EventType = FileLifecycleEventType.ReferenceDeleted,
					Detail = "Unsent social content deleted."
				}, cancellationToken);
			}
		}
		return NoContent();
	}

	[HttpGet("destinations")]
	public async Task<ActionResult<IReadOnlyList<SocialDestination>>> GetDestinations(CancellationToken cancellationToken)
	{
		var integration = await integrations.GetCurrentAsync(cancellationToken);
		if (integration is null || !integration.IsEnabled || integration.Status != OrganizationIntegrationStatus.Connected) return Ok(Array.Empty<SocialDestination>());
		var mapping = integration.ClubMappings.FirstOrDefault(item => item.ClubId == tenant.ClubId);
		if (mapping is null) return Ok(Array.Empty<SocialDestination>());
		var page = integration.Pages.FirstOrDefault(item => item.Id == mapping.FacebookPageId);
		var result = new List<SocialDestination>();
		if (mapping.FacebookEnabled && page is not null) result.Add(new SocialDestination(SocialPlatform.Facebook, page.Id, page.Name));
		if (mapping.InstagramEnabled && page?.InstagramAccount is { } instagram) result.Add(new SocialDestination(SocialPlatform.Instagram, instagram.Id, instagram.Name, instagram.Username));
		return Ok(result);
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<SocialPublication>>> Get([FromQuery] int limit = 50, CancellationToken cancellationToken = default) =>
		Ok(await publications.GetCurrentClubAsync(limit, cancellationToken));

	[HttpGet("{id:guid}")]
	public async Task<ActionResult<SocialPublication>> Get(Guid id, CancellationToken cancellationToken) =>
		await publications.GetAsync(id, cancellationToken) is { } publication ? Ok(publication) : NotFound();

	[HttpPost]
	public async Task<ActionResult<SocialPublication>> Create(CreateSocialPublicationRequest request, CancellationToken cancellationToken)
	{
		if (request.Title.Trim().Length > 120) return BadRequest("The content title cannot exceed 120 characters.");
		if (request.EditorStateJson?.Length > 100_000) return BadRequest("The editor state is too large.");
		if (request.FacebookCaption.Length > 63206 || request.InstagramCaption.Length > 2200) return BadRequest("A platform caption is too long.");
		if (request.ScheduledForUtc < DateTime.UtcNow.AddMinutes(-1)) return BadRequest("The scheduled time cannot be in the past.");
		var deliveries = new List<SocialPublicationDelivery>();
		if (request.PublishToFacebook || request.PublishToInstagram)
		{
			var integration = await integrations.GetCurrentAsync(cancellationToken);
			if (integration is null || !integration.IsEnabled || integration.Status != OrganizationIntegrationStatus.Connected) return Conflict("Meta publishing is not currently enabled.");
			var mapping = integration.ClubMappings.FirstOrDefault(item => item.ClubId == tenant.ClubId);
			var page = integration.Pages.FirstOrDefault(item => item.Id == mapping?.FacebookPageId);
			if (request.PublishToFacebook)
			{
				if (mapping?.FacebookEnabled != true || page is null) return BadRequest("Facebook is not configured for this club.");
				deliveries.Add(new SocialPublicationDelivery { Platform = SocialPlatform.Facebook, DestinationId = page.Id, DestinationName = page.Name });
			}
			if (request.PublishToInstagram)
			{
				if (mapping?.InstagramEnabled != true || page?.InstagramAccount is not { } instagram) return BadRequest("Instagram is not configured for this club.");
				deliveries.Add(new SocialPublicationDelivery { Platform = SocialPlatform.Instagram, DestinationId = instagram.Id, DestinationName = instagram.Username.Length > 0 ? $"@{instagram.Username}" : instagram.Name });
			}
		}
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		var created = await publications.CreateAsync(new SocialPublication
		{
			CreatedByUserId = userId.Value,
			Title = string.IsNullOrWhiteSpace(request.Title) ? $"Social post {DateTime.UtcNow:dd MMM yyyy HH:mm}" : request.Title.Trim(),
			GraphicKind = request.GraphicKind?.Trim(),
			TemplateId = request.TemplateId?.Trim(),
			EditorStateJson = request.EditorStateJson,
			FacebookCaption = request.FacebookCaption.Trim(),
			InstagramCaption = request.InstagramCaption.Trim(),
			ScheduledForUtc = request.ScheduledForUtc?.ToUniversalTime(),
			Deliveries = deliveries
		}, cancellationToken);
		return Created($"/api/social-publications/{created.Id}", created);
	}

	[HttpPost("{id:guid}/media")]
	public async Task<ActionResult<SocialPublication>> AttachMedia(Guid id, AttachSocialPublicationFileRequest request, CancellationToken cancellationToken)
	{
		var file = await files.GetByIdAsync(request.FileId, cancellationToken);
		if (file is null || file.Status != ClubFileStatus.Uploaded || file.LinkedEntityType != ClubFileLinkedEntityType.SocialPublication || file.LinkedEntityId != id) return BadRequest("Upload a valid JPEG publication image first.");
		if (!string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)) return BadRequest("Meta publication images must be JPEG files.");
		return await publications.AttachFileAsync(id, request.FileId, cancellationToken) is { } publication ? Ok(publication) : NotFound();
	}

	[HttpPost("{id:guid}/queue")]
	public async Task<ActionResult<SocialPublication>> Queue(Guid id, QueueSocialPublicationRequest request, CancellationToken cancellationToken)
	{
		if (request.Mode == SocialPublicationMode.YepsetDraft) return BadRequest("Choose publish now or Facebook draft.");
		return await publications.QueueAsync(id, request.Mode, cancellationToken) is { } publication
			? Ok(publication)
			: Conflict("This content is not ready for that action. Facebook drafts require a configured Facebook destination and an uploaded image.");
	}

	[HttpPost("{id:guid}/cancel")]
	public async Task<ActionResult<SocialPublication>> Cancel(Guid id, CancellationToken cancellationToken) =>
		await publications.CancelAsync(id, cancellationToken) is { } publication ? Ok(publication) : Conflict("This publication can no longer be cancelled.");

	[HttpPost("{id:guid}/retry")]
	public async Task<ActionResult<SocialPublication>> Retry(Guid id, CancellationToken cancellationToken) =>
		await publications.RetryAsync(id, cancellationToken) is { } publication ? Ok(publication) : Conflict("This publication has no failed delivery to retry.");

	private Guid? GetCurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;
}
