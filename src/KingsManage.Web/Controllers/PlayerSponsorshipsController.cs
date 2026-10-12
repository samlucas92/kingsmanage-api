using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "ClubAdmin")]
[Route("api/player-sponsorships")]
public sealed class PlayerSponsorshipsController : ControllerBase
{
	private const int MaxImageBytes = 2 * 1024 * 1024;
	private static readonly string[] AllowedImagePrefixes = ["data:image/jpeg;base64,", "data:image/png;base64,", "data:image/webp;base64,"];
	private readonly IPlayerSponsorshipService sponsorshipService;
	private readonly IPlayerService playerService;
	private readonly ISportsClubService clubService;
	private readonly ITenantContext tenantContext;
	private readonly IFileContentScanner contentScanner;

	public PlayerSponsorshipsController(
		IPlayerSponsorshipService sponsorshipService,
		IPlayerService playerService,
		ISportsClubService clubService,
		ITenantContext tenantContext,
		IFileContentScanner contentScanner)
	{
		this.sponsorshipService = sponsorshipService;
		this.playerService = playerService;
		this.clubService = clubService;
		this.tenantContext = tenantContext;
		this.contentScanner = contentScanner;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<PlayerSponsorshipViewModel>>> GetAll(CancellationToken cancellationToken)
	{
		var items = await sponsorshipService.GetAllAsync(cancellationToken);
		return Ok(items.Select(item => PlayerSponsorshipViewModel.FromSponsorship(item, false)));
	}

	[HttpGet("{id:guid}")]
	public async Task<ActionResult<PlayerSponsorshipViewModel>> GetById(Guid id, CancellationToken cancellationToken)
	{
		var item = await sponsorshipService.GetByIdAsync(id, cancellationToken);
		return item is null ? NotFound() : Ok(PlayerSponsorshipViewModel.FromSponsorship(item));
	}

	[AllowAnonymous]
	[HttpGet("go/{goCode}")]
	public async Task<ActionResult<PlayerSponsorshipViewModel>> GetPublic(string goCode, CancellationToken cancellationToken)
	{
		var item = await sponsorshipService.GetByGoCodeAsync(goCode, cancellationToken);
		return item is null || item.Status == PlayerSponsorshipStatus.Closed
			? NotFound()
			: Ok(PlayerSponsorshipViewModel.FromSponsorship(item));
	}

	[HttpPost]
	public async Task<ActionResult<PlayerSponsorshipViewModel>> Create(CreatePlayerSponsorshipModel model, CancellationToken cancellationToken)
	{
		if (model.PlayerId == Guid.Empty) return BadRequest("Player is required.");
		var player = await playerService.GetByIdAsync(model.PlayerId, cancellationToken);
		if (player is null || !player.IsActive) return BadRequest("Select an active player.");
		var club = await clubService.GetByIdAsync(tenantContext.ClubId, cancellationToken);
		if (club is null) return BadRequest("The current club could not be found.");
		var userId = ReadUserId();
		if (userId is null) return BadRequest("The current user could not be identified.");

		var created = await sponsorshipService.CreateAsync(new PlayerSponsorship
		{
			PlayerId = player.Id,
			PlayerName = player.Name,
			ClubName = club.Name,
			ClubPrimaryColor = club.PrimaryColor,
			ClubSecondaryColor = club.SecondaryColor,
			Status = PlayerSponsorshipStatus.Open,
			CreatedByUserId = userId.Value
		}, cancellationToken);
		return CreatedAtAction(nameof(GetById), new { id = created.Id }, PlayerSponsorshipViewModel.FromSponsorship(created));
	}

	[HttpPut("{id:guid}")]
	public async Task<ActionResult<PlayerSponsorshipViewModel>> Update(Guid id, UpdatePlayerSponsorshipModel model, CancellationToken cancellationToken)
	{
		var existing = await sponsorshipService.GetByIdAsync(id, cancellationToken);
		if (existing is null) return NotFound();
		if (!Enum.IsDefined(model.Status)) return BadRequest("Sponsorship status is invalid.");
		var validationError = await ValidateDetailsAsync(model.SponsorName, model.SponsorDescription, model.SponsorImageDataUrl, existing.TermsAccepted, cancellationToken, requireDetails: model.Status == PlayerSponsorshipStatus.Completed);
		if (validationError is not null) return BadRequest(validationError);
		existing.SponsorName = model.SponsorName ?? string.Empty;
		existing.SponsorDescription = model.SponsorDescription ?? string.Empty;
		existing.SponsorImageDataUrl = model.SponsorImageDataUrl ?? string.Empty;
		existing.Status = model.Status;
		var updated = await sponsorshipService.UpdateAsync(existing, cancellationToken);
		return updated is null ? NotFound() : Ok(PlayerSponsorshipViewModel.FromSponsorship(updated));
	}

	[AllowAnonymous]
	[HttpPost("go/{goCode}/submit")]
	public async Task<ActionResult<PlayerSponsorshipViewModel>> Submit(string goCode, SubmitPlayerSponsorshipModel model, CancellationToken cancellationToken)
	{
		var item = await sponsorshipService.GetByGoCodeAsync(goCode, cancellationToken);
		if (item is null || item.Status == PlayerSponsorshipStatus.Closed) return NotFound();
		if (item.Status != PlayerSponsorshipStatus.Open || item.SubmittedAt.HasValue) return Conflict("This sponsorship has already been completed and can now only be edited by a club administrator.");
		var validationError = await ValidateDetailsAsync(model.SponsorName, model.SponsorDescription, model.SponsorImageDataUrl, model.TermsAccepted, cancellationToken, requireDetails: true);
		if (validationError is not null) return BadRequest(validationError);

		item.SponsorName = model.SponsorName ?? string.Empty;
		item.SponsorDescription = model.SponsorDescription ?? string.Empty;
		item.SponsorImageDataUrl = model.SponsorImageDataUrl ?? string.Empty;
		item.TermsAccepted = true;
		item.AcceptedTermsText = PlayerSponsorshipTerms.Text;
		item.TermsAcceptedAt = DateTime.UtcNow;
		item.SubmittedAt = DateTime.UtcNow;
		item.Status = PlayerSponsorshipStatus.Completed;
		var submitted = await sponsorshipService.SubmitAsync(item, cancellationToken);
		return submitted is null
			? Conflict("This sponsorship has already been completed and can now only be edited by a club administrator.")
			: Ok(PlayerSponsorshipViewModel.FromSponsorship(submitted));
	}

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
		await sponsorshipService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

	private async Task<string?> ValidateDetailsAsync(string sponsorName, string description, string imageDataUrl, bool termsAccepted, CancellationToken cancellationToken, bool requireDetails)
	{
		sponsorName ??= string.Empty;
		description ??= string.Empty;
		imageDataUrl ??= string.Empty;
		if (requireDetails && string.IsNullOrWhiteSpace(sponsorName)) return "Sponsor name is required.";
		if (sponsorName.Trim().Length > 120) return "Sponsor name must be 120 characters or less.";
		if (requireDetails && string.IsNullOrWhiteSpace(description)) return "Sponsor description is required.";
		if (description.Trim().Length > 1000) return "Sponsor description must be 1,000 characters or less.";
		if (requireDetails && string.IsNullOrWhiteSpace(imageDataUrl)) return "Sponsor image is required.";
		if (!string.IsNullOrWhiteSpace(imageDataUrl))
		{
			if (imageDataUrl.Length > (MaxImageBytes * 4 / 3) + 128) return "Sponsor image must be 2MB or less.";
			var prefix = AllowedImagePrefixes.FirstOrDefault(candidate => imageDataUrl.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));
			if (prefix is null) return "Sponsor image must be a JPEG, PNG or WebP image.";
			byte[] bytes;
			try { bytes = Convert.FromBase64String(imageDataUrl[prefix.Length..]); }
			catch (FormatException) { return "Sponsor image is not valid."; }
			if (bytes.Length == 0 || bytes.Length > MaxImageBytes) return "Sponsor image must be 2MB or less.";
			var scan = await contentScanner.ScanAsync(bytes, prefix[5..prefix.IndexOf(';')], cancellationToken);
			if (!scan.IsSafe) return "Sponsor image failed the security scan.";
		}
		if (requireDetails && !termsAccepted) return "You must accept the sponsorship terms.";
		return null;
	}

	private Guid? ReadUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
