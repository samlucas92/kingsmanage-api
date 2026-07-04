using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/files")]
public class FilesController : ControllerBase
{
	private const long MaxFileSizeBytes = 10 * 1024 * 1024;
	private const long MaxManagedImageSizeBytes = 5 * 1024 * 1024;
	private const long MaxClubLogoSizeBytes = 2 * 1024 * 1024;
	private static readonly TimeSpan UploadUrlExpiry = TimeSpan.FromMinutes(15);
	private static readonly TimeSpan DownloadUrlExpiry = TimeSpan.FromMinutes(10);
	private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"image/jpeg",
		"image/png",
		"image/webp",
		"application/pdf"
	};

	private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".jpg",
		".jpeg",
		".png",
		".webp",
		".pdf"
	};

	private readonly IClubFileService fileService;
	private readonly IStoredFileObjectService storedObjectService;
	private readonly IFileStorageService storageService;
	private readonly IFileLifecycleService lifecycleService;
	private readonly ITenantContext tenantContext;
	private readonly IClubPostService postService;
	private readonly IClubEventService eventService;
	private readonly IPlayerService playerService;
	private readonly ISportsClubService clubService;
	private readonly IClubPostTemplateService templateService;

	public FilesController(
		IClubFileService fileService,
		IStoredFileObjectService storedObjectService,
		IFileStorageService storageService,
		IFileLifecycleService lifecycleService,
		ITenantContext tenantContext,
		IClubPostService postService,
		IClubEventService eventService,
		IPlayerService playerService,
		ISportsClubService clubService,
		IClubPostTemplateService templateService
	)
	{
		this.fileService = fileService;
		this.storedObjectService = storedObjectService;
		this.storageService = storageService;
		this.lifecycleService = lifecycleService;
		this.tenantContext = tenantContext;
		this.postService = postService;
		this.eventService = eventService;
		this.playerService = playerService;
		this.clubService = clubService;
		this.templateService = templateService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<ClubFile>>> GetForLinkedEntity(
		[FromQuery] ClubFileLinkedEntityType linkedEntityType,
		[FromQuery] Guid linkedEntityId,
		CancellationToken cancellationToken
	)
	{
		if (linkedEntityId == Guid.Empty)
		{
			return BadRequest("Linked entity id is required.");
		}

		var entityExists = await LinkedEntityExistsAsync(
			linkedEntityType,
			linkedEntityId,
			cancellationToken
		);

		if (!entityExists)
		{
			return NotFound();
		}

		var files = await fileService.GetByLinkedEntityAsync(
			linkedEntityType,
			linkedEntityId,
			cancellationToken
		);

		var visibleFiles = new List<ClubFile>();

		foreach (var file in files.Where(file => file.Status == ClubFileStatus.Uploaded))
		{
			if (await CanCurrentUserAccessFileAsync(file, cancellationToken))
			{
				visibleFiles.Add(file);
			}
		}

		return Ok(visibleFiles);
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpPost("upload-url")]
	public async Task<ActionResult<FileUploadUrlResponse>> CreateUploadUrl(
		CreateFileUploadUrlModel model,
		CancellationToken cancellationToken
	)
	{
		var validationError = await ValidateCreateUploadUrlModelAsync(model, cancellationToken);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var userIdResult = GetCurrentUserId();

		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		var capacity = await lifecycleService.CheckUploadCapacityAsync(
			tenantContext.OrganizationId,
			model.ContentHash,
			model.SizeBytes,
			cancellationToken
		);
		if (!capacity.IsAllowed)
		{
			await lifecycleService.RecordAuditAsync(
				new FileLifecycleAudit
				{
					OrganizationId = tenantContext.OrganizationId,
					ClubId = tenantContext.ClubId,
					UserId = userIdResult.UserId,
					EventType = FileLifecycleEventType.UploadRejected,
					Detail = "Organization file-storage quota exceeded."
				},
				cancellationToken
			);
			return StatusCode(
				StatusCodes.Status413PayloadTooLarge,
				"Organization file-storage quota exceeded. Delete unused files or increase the storage allowance."
			);
		}

		var fileId = Guid.NewGuid();
		var safeFileName = BuildSafeFileName(model.OriginalFileName);
		var contentHash = model.ContentHash.Trim().ToLowerInvariant();
		StoredFileObject? storedObject = null;
		var uploadRequired = true;
		var reusedStoredObject = false;
		string storageKey;

		if (!string.IsNullOrWhiteSpace(contentHash))
		{
			var candidate = new StoredFileObject
			{
				Id = Guid.NewGuid(),
				OrganizationId = tenantContext.OrganizationId,
				ContentHash = contentHash,
				StorageKey = BuildContentAddressedStorageKey(
					tenantContext.OrganizationId,
					contentHash
				),
				StorageProvider = "CloudflareR2",
				ContentType = model.ContentType.Trim(),
				SizeBytes = model.SizeBytes
			};

			StoredFileObjectResolution resolution;
			try
			{
				resolution = await storedObjectService.ResolveAsync(
					candidate,
					cancellationToken
				);
			}
			catch (InvalidOperationException exception)
			{
				return BadRequest(exception.Message);
			}

			storedObject = resolution.StoredObject;
			storageKey = storedObject.StorageKey;
			uploadRequired = storedObject.Status != StoredFileObjectStatus.Uploaded;
			reusedStoredObject = !resolution.WasCreated;
		}
		else
		{
			storageKey = BuildStorageKey(
				model.LinkedEntityType,
				model.LinkedEntityId,
				fileId,
				safeFileName
			);
		}

		if (
			storedObject is not null &&
			!await storedObjectService.IncrementReferenceCountAsync(
				storedObject.Id,
				cancellationToken
			)
		)
		{
			return Conflict("The stored object is currently being removed. Please retry the upload.");
		}

		var file = await fileService.CreateAsync(
			new ClubFile
			{
				Id = fileId,
				OriginalFileName = Path.GetFileName(model.OriginalFileName).Trim(),
				StoredFileName = safeFileName,
				StorageKey = storageKey,
				StoredObjectId = storedObject?.Id,
				ContentHash = contentHash,
				ContentType = model.ContentType.Trim(),
				SizeBytes = model.SizeBytes,
				Visibility = model.Visibility,
				LinkedEntityType = model.LinkedEntityType,
				LinkedEntityId = model.LinkedEntityId,
				Status = uploadRequired
					? ClubFileStatus.PendingUpload
					: ClubFileStatus.Uploaded,
				UploadedByUserId = userIdResult.UserId,
				UploadedByUserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow,
				UploadedAt = uploadRequired ? null : DateTime.UtcNow
			},
			cancellationToken
		);
		await RecordAuditAsync(
			uploadRequired
				? FileLifecycleEventType.UploadRequested
				: FileLifecycleEventType.UploadReused,
			file,
			storedObject,
			userIdResult.UserId,
			uploadRequired
				? "New upload session created."
				: "Existing validated object reused.",
			cancellationToken
		);

		FileStorageSignedUrl? signedUrl = null;
		if (uploadRequired)
		{
			signedUrl = await storageService.CreateUploadUrlAsync(
				file.StorageKey,
				UploadUrlExpiry,
				cancellationToken
			);
		}

		return Ok(new FileUploadUrlResponse
		{
			File = file,
			UploadUrl = signedUrl?.Url ?? string.Empty,
			ExpiresAtUtc = signedUrl?.ExpiresAtUtc ?? DateTime.UtcNow,
			UploadRequired = uploadRequired,
			ReusedStoredObject = reusedStoredObject,
			StorageWarning = capacity.Usage.IsNearLimit
				? $"Organization storage is {capacity.Usage.UsedPercent:0.##}% full."
				: string.Empty
		});
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpPost("{id}/mark-uploaded")]
	public async Task<ActionResult<ClubFile>> MarkUploaded(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "File", out var fileId, out var errorResult))
		{
			return errorResult!;
		}

		var pendingFile = await fileService.GetByIdAsync(fileId, cancellationToken);

		if (pendingFile is null || pendingFile.Status == ClubFileStatus.Deleted)
		{
			return NotFound();
		}

		if (pendingFile.Status == ClubFileStatus.Uploaded)
		{
			return Ok(pendingFile);
		}

		var validation = await storageService.ValidateObjectAsync(
			pendingFile.StorageKey,
			pendingFile.ContentHash,
			pendingFile.ContentType,
			pendingFile.SizeBytes,
			cancellationToken
		);

		if (!validation.IsValid)
		{
			await RecordAuditAsync(
				FileLifecycleEventType.UploadRejected,
				pendingFile,
				null,
				GetCurrentUserId().UserId,
				validation.ErrorMessage,
				cancellationToken
			);
			return BadRequest(
				string.IsNullOrWhiteSpace(validation.ErrorMessage)
					? "Uploaded file failed validation."
					: validation.ErrorMessage
			);
		}

		if (!validation.IsSafe)
		{
			var reason = string.IsNullOrWhiteSpace(validation.ThreatName)
				? "File content failed security scanning."
				: validation.ThreatName;
			if (pendingFile.StoredObjectId is Guid quarantinedObjectId)
			{
				await storedObjectService.MarkQuarantinedAsync(
					quarantinedObjectId,
					reason,
					cancellationToken
				);
			}

			var quarantinedFile = await fileService.MarkQuarantinedAsync(
				pendingFile.Id,
				reason,
				cancellationToken
			);
			await RecordAuditAsync(
				FileLifecycleEventType.FileQuarantined,
				quarantinedFile ?? pendingFile,
				null,
				GetCurrentUserId().UserId,
				reason,
				cancellationToken
			);
			return UnprocessableEntity("The uploaded file was quarantined by security scanning.");
		}

		if (pendingFile.StoredObjectId is Guid storedObjectId)
		{
			await storedObjectService.MarkUploadedAsync(
				storedObjectId,
				cancellationToken
			);
		}

		var file = await fileService.MarkUploadedAsync(fileId, cancellationToken);

		if (file is null)
		{
			return NotFound();
		}

		await RecordAuditAsync(
			FileLifecycleEventType.UploadValidated,
			file,
			null,
			GetCurrentUserId().UserId,
			"Upload checksum, size, content type and security scan validated.",
			cancellationToken
		);

		return Ok(file);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpGet("storage-usage")]
	public async Task<ActionResult<FileStorageUsage>> GetStorageUsage(
		CancellationToken cancellationToken
	)
	{
		return Ok(
			await lifecycleService.GetUsageAsync(
				tenantContext.OrganizationId,
				cancellationToken
			)
		);
	}

	[Authorize(Policy = "OrganizationAdmin")]
	[HttpGet("audit")]
	public async Task<ActionResult<IReadOnlyList<FileLifecycleAudit>>> GetAudit(
		[FromQuery] int limit = 100,
		CancellationToken cancellationToken = default
	)
	{
		return Ok(
			await lifecycleService.GetAuditAsync(
				tenantContext.OrganizationId,
				limit,
				cancellationToken
			)
		);
	}

	[Authorize(Policy = "OrganizationAdmin")]
	[HttpPost("{id}/assign-club-logo")]
	public async Task<ActionResult<SportsClub>> AssignClubLogo(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "File", out var fileId, out var errorResult))
		{
			return errorResult!;
		}

		var file = await fileService.GetByIdAsync(fileId, cancellationToken);
		if (
			file is null ||
			file.Status != ClubFileStatus.Uploaded ||
			file.LinkedEntityType != ClubFileLinkedEntityType.ClubLogo ||
			!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
		)
		{
			return BadRequest("A valid uploaded club logo is required.");
		}

		var club = await clubService.GetByIdAsync(file.LinkedEntityId, cancellationToken);
		if (club is null)
		{
			return NotFound();
		}
		if (IsClubAdminOnly() && club.Id != tenantContext.ClubId) return Forbid();

		var previousFileId = club.LogoFileId;
		var updated = await clubService.SetLogoFileAsync(
			club.Id,
			file.Id,
			cancellationToken
		);
		if (updated is null)
		{
			return NotFound();
		}

		if (previousFileId is Guid previousId && previousId != file.Id)
		{
			await DeleteManagedReferenceAsync(previousId, cancellationToken);
		}

		return Ok(updated);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpDelete("club-logo/{clubId:guid}")]
	public async Task<ActionResult<SportsClub>> RemoveClubLogo(
		Guid clubId,
		CancellationToken cancellationToken
	)
	{
		var club = await clubService.GetByIdAsync(clubId, cancellationToken);
		if (club is null)
		{
			return NotFound();
		}
		if (IsClubAdminOnly() && club.Id != tenantContext.ClubId) return Forbid();

		var updated = await clubService.SetLogoFileAsync(clubId, null, cancellationToken);
		if (club.LogoFileId is Guid fileId)
		{
			await DeleteManagedReferenceAsync(fileId, cancellationToken);
		}

		return Ok(updated);
	}

	[HttpGet("{id}/download-url")]
	public async Task<ActionResult<FileDownloadUrlResponse>> CreateDownloadUrl(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "File", out var fileId, out var errorResult))
		{
			return errorResult!;
		}

		var file = await fileService.GetByIdAsync(fileId, cancellationToken);

		if (file is null || file.Status != ClubFileStatus.Uploaded)
		{
			return NotFound();
		}

		if (!await CanCurrentUserAccessFileAsync(file, cancellationToken))
		{
			return Forbid();
		}

		var signedUrl = await storageService.CreateDownloadUrlAsync(
			file.StorageKey,
			DownloadUrlExpiry,
			cancellationToken
		);

		return Ok(new FileDownloadUrlResponse
		{
			File = file,
			DownloadUrl = signedUrl.Url,
			ExpiresAtUtc = signedUrl.ExpiresAtUtc
		});
	}

	[Authorize(Policy = "TeamManagement")]
	[HttpDelete("{id}")]
	public async Task<IActionResult> Delete(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "File", out var fileId, out var errorResult))
		{
			return errorResult!;
		}

		var userIdResult = GetCurrentUserId();

		if (!userIdResult.Success)
		{
			return BadRequest(userIdResult.ErrorMessage);
		}

		var file = await fileService.GetByIdAsync(fileId, cancellationToken);
		var deleted = await fileService.SoftDeleteAsync(
			fileId,
			userIdResult.UserId,
			cancellationToken
		);

		if (!deleted)
		{
			return NotFound();
		}

		if (file?.StoredObjectId is Guid storedObjectId)
		{
			await storedObjectService.DecrementReferenceCountAsync(
				storedObjectId,
				cancellationToken
			);
		}

		if (file is not null)
		{
			await RecordAuditAsync(
				FileLifecycleEventType.ReferenceDeleted,
				file,
				null,
				userIdResult.UserId,
				"File reference deleted.",
				cancellationToken
			);
		}

		return NoContent();
	}

	private Task RecordAuditAsync(
		FileLifecycleEventType eventType,
		ClubFile file,
		StoredFileObject? storedObject,
		Guid? userId,
		string detail,
		CancellationToken cancellationToken
	)
	{
		return lifecycleService.RecordAuditAsync(
			new FileLifecycleAudit
			{
				OrganizationId = file.OrganizationId == Guid.Empty
					? tenantContext.OrganizationId
					: file.OrganizationId,
				ClubId = file.ClubId == Guid.Empty ? tenantContext.ClubId : file.ClubId,
				FileId = file.Id,
				StoredObjectId = storedObject?.Id ?? file.StoredObjectId,
				UserId = userId,
				EventType = eventType,
				Detail = detail
			},
			cancellationToken
		);
	}

	private async Task<string?> ValidateCreateUploadUrlModelAsync(
		CreateFileUploadUrlModel model,
		CancellationToken cancellationToken
	)
	{
		if (string.IsNullOrWhiteSpace(model.OriginalFileName))
		{
			return "Original file name is required.";
		}

		var fileName = Path.GetFileName(model.OriginalFileName).Trim();

		if (string.IsNullOrWhiteSpace(fileName))
		{
			return "Original file name is invalid.";
		}

		if (string.IsNullOrWhiteSpace(model.ContentType))
		{
			return "Content type is required.";
		}

		if (!AllowedContentTypes.Contains(model.ContentType.Trim()))
		{
			return "File type is not allowed.";
		}

		var extension = Path.GetExtension(fileName);

		if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
		{
			return "File extension is not allowed.";
		}

		if (model.SizeBytes <= 0)
		{
			return "File size must be greater than zero.";
		}

		if (model.SizeBytes > MaxFileSizeBytes)
		{
			return "File size must be 10MB or less.";
		}

		if (
			model.LinkedEntityType is ClubFileLinkedEntityType.ClubLogo
				or ClubFileLinkedEntityType.PostTemplate
				or ClubFileLinkedEntityType.RichTextDraft
		)
		{
			if (!model.ContentType.Trim().StartsWith("image/", StringComparison.OrdinalIgnoreCase))
			{
				return "Managed editor and branding assets must be images.";
			}

			var imageLimit = model.LinkedEntityType == ClubFileLinkedEntityType.ClubLogo
				? MaxClubLogoSizeBytes
				: MaxManagedImageSizeBytes;
			if (model.SizeBytes > imageLimit)
			{
				return model.LinkedEntityType == ClubFileLinkedEntityType.ClubLogo
					? "Club logos must be 2MB or less."
					: "Embedded images must be 5MB or less.";
			}
		}

		if (
			!string.IsNullOrWhiteSpace(model.ContentHash) &&
			!IsSha256Hash(model.ContentHash)
		)
		{
			return "Content hash must be a 64-character SHA-256 value.";
		}

		if (model.LinkedEntityId == Guid.Empty)
		{
			return "Linked entity id is required.";
		}

		var entityExists = await LinkedEntityExistsAsync(
			model.LinkedEntityType,
			model.LinkedEntityId,
			cancellationToken
		);

		if (!entityExists)
		{
			return "Linked entity was not found.";
		}

		return null;
	}

	private async Task<bool> LinkedEntityExistsAsync(
		ClubFileLinkedEntityType linkedEntityType,
		Guid linkedEntityId,
		CancellationToken cancellationToken
	)
	{
		return linkedEntityType switch
		{
			ClubFileLinkedEntityType.Post =>
				await postService.GetByIdAsync(linkedEntityId, cancellationToken) is not null,
			ClubFileLinkedEntityType.Event =>
				await eventService.GetByIdAsync(linkedEntityId, cancellationToken) is not null,
			ClubFileLinkedEntityType.Player =>
				await playerService.GetByIdAsync(linkedEntityId, cancellationToken) is not null,
			ClubFileLinkedEntityType.ClubDocument => true,
			ClubFileLinkedEntityType.ClubLogo =>
				await clubService.GetByIdAsync(linkedEntityId, cancellationToken) is not null,
			ClubFileLinkedEntityType.PostTemplate =>
				await templateService.GetByIdAsync(linkedEntityId, cancellationToken) is not null,
			ClubFileLinkedEntityType.RichTextDraft => true,
			_ => false
		};
	}

	private async Task<bool> CanCurrentUserAccessFileAsync(
		ClubFile file,
		CancellationToken cancellationToken
	)
	{
		if (file.Visibility == ClubFileVisibility.AdminAndCoach && !IsAdminOrCoach())
		{
			return false;
		}

		return file.LinkedEntityType switch
		{
			ClubFileLinkedEntityType.Post =>
				await postService.GetByIdAsync(file.LinkedEntityId, cancellationToken) is not null,
			ClubFileLinkedEntityType.Event => await CanCurrentUserAccessEventFileAsync(file.LinkedEntityId, cancellationToken),
			ClubFileLinkedEntityType.Player => IsAdminOrCoach(),
			ClubFileLinkedEntityType.ClubDocument => IsAdminOrCoach(),
			ClubFileLinkedEntityType.ClubLogo => true,
			ClubFileLinkedEntityType.PostTemplate => IsAdminOrCoach(),
			ClubFileLinkedEntityType.RichTextDraft => IsAdminOrCoach(),
			_ => false
		};
	}

	private async Task DeleteManagedReferenceAsync(
		Guid fileId,
		CancellationToken cancellationToken
	)
	{
		var file = await fileService.GetByIdAsync(fileId, cancellationToken);
		if (file is null)
		{
			return;
		}

		var user = GetCurrentUserId();
		if (!user.Success)
		{
			return;
		}

		if (!await fileService.SoftDeleteAsync(fileId, user.UserId, cancellationToken))
		{
			return;
		}

		if (file.StoredObjectId is Guid storedObjectId)
		{
			await storedObjectService.DecrementReferenceCountAsync(
				storedObjectId,
				cancellationToken
			);
		}
	}

	private async Task<bool> CanCurrentUserAccessEventFileAsync(
		Guid eventId,
		CancellationToken cancellationToken
	)
	{
		var clubEvent = await eventService.GetByIdAsync(eventId, cancellationToken);

		if (clubEvent is null)
		{
			return false;
		}

		if (clubEvent.Type == ClubEventType.Meeting && !IsAdminOrCoach())
		{
			return false;
		}

		return true;
	}

	private bool IsAdminOrCoach()
	{
		return User.IsInRole(UserRole.Admin.ToString()) || User.IsInRole(UserRole.Coach.ToString());
	}

	private static string BuildStorageKey(
		ClubFileLinkedEntityType linkedEntityType,
		Guid linkedEntityId,
		Guid fileId,
		string safeFileName
	)
	{
		return $"{linkedEntityType.ToString().ToLowerInvariant()}/{linkedEntityId}/{fileId}/{safeFileName}";
	}

	private static string BuildContentAddressedStorageKey(
		Guid organizationId,
		string contentHash
	)
	{
		return $"organizations/{organizationId}/objects/{contentHash[..2]}/{contentHash}";
	}

	private static bool IsSha256Hash(string value)
	{
		var hash = value.Trim();
		return hash.Length == 64 && hash.All(character =>
			character is >= '0' and <= '9' ||
			character is >= 'a' and <= 'f' ||
			character is >= 'A' and <= 'F'
		);
	}

	private static string BuildSafeFileName(string originalFileName)
	{
		var fileName = Path.GetFileName(originalFileName).Trim();
		var builder = new System.Text.StringBuilder(fileName.Length);

		foreach (var character in fileName)
		{
			if (char.IsLetterOrDigit(character) || character is '.' or '-' or '_')
			{
				builder.Append(character);
			}
			else if (char.IsWhiteSpace(character))
			{
				builder.Append('-');
			}
		}

		var safeFileName = builder.ToString().Trim('-', '.', '_');

		return string.IsNullOrWhiteSpace(safeFileName)
			? $"file{Path.GetExtension(fileName)}"
			: safeFileName;
	}

	private static bool TryParseGuid(
		string value,
		string label,
		out Guid id,
		out ActionResult? errorResult
	)
	{
		if (Guid.TryParse(value, out id))
		{
			errorResult = null;
			return true;
		}

		errorResult = new BadRequestObjectResult($"{label} id is invalid.");
		return false;
	}

	private CurrentUserIdResult GetCurrentUserId()
	{
		var userIdClaim =
			User.FindFirstValue(ClaimTypes.NameIdentifier) ??
			User.FindFirstValue("sub") ??
			User.FindFirstValue("id");

		if (string.IsNullOrWhiteSpace(userIdClaim))
		{
			return CurrentUserIdResult.Failed("Current user id was not found in the auth token.");
		}

		if (!Guid.TryParse(userIdClaim, out var userId))
		{
			return CurrentUserIdResult.Failed("Current user id in the auth token is invalid.");
		}

		return CurrentUserIdResult.SuccessResult(userId);
	}

	private bool IsClubAdminOnly() =>
		User.HasClaim(
			HttpTenantContext.TenantRoleClaim,
			TenantRole.ClubAdmin.ToString()) &&
		!User.HasClaim(HttpTenantContext.PlatformAdminClaim, "true");

	private sealed record CurrentUserIdResult(
		bool Success,
		Guid UserId,
		string ErrorMessage
	)
	{
		public static CurrentUserIdResult SuccessResult(Guid userId) =>
			new(true, userId, string.Empty);

		public static CurrentUserIdResult Failed(string errorMessage) =>
			new(false, Guid.Empty, errorMessage);
	}
}
