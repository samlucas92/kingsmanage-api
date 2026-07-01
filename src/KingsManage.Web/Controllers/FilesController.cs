using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/files")]
public class FilesController : ControllerBase
{
	private const long MaxFileSizeBytes = 10 * 1024 * 1024;
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

	private readonly IClubFileService _fileService;
	private readonly IStoredFileObjectService _storedObjectService;
	private readonly IFileStorageService _storageService;
	private readonly ITenantContext _tenantContext;
	private readonly IClubPostService _postService;
	private readonly IClubEventService _eventService;
	private readonly IPlayerService _playerService;

	public FilesController(
		IClubFileService fileService,
		IStoredFileObjectService storedObjectService,
		IFileStorageService storageService,
		ITenantContext tenantContext,
		IClubPostService postService,
		IClubEventService eventService,
		IPlayerService playerService
	)
	{
		_fileService = fileService;
		_storedObjectService = storedObjectService;
		_storageService = storageService;
		_tenantContext = tenantContext;
		_postService = postService;
		_eventService = eventService;
		_playerService = playerService;
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

		var files = await _fileService.GetByLinkedEntityAsync(
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

	[Authorize(Roles = "Admin,Coach")]
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
				OrganizationId = _tenantContext.OrganizationId,
				ContentHash = contentHash,
				StorageKey = BuildContentAddressedStorageKey(
					_tenantContext.OrganizationId,
					contentHash
				),
				ContentType = model.ContentType.Trim(),
				SizeBytes = model.SizeBytes
			};

			StoredFileObjectResolution resolution;
			try
			{
				resolution = await _storedObjectService.ResolveAsync(
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

		var file = await _fileService.CreateAsync(
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

		if (storedObject is not null)
		{
			await _storedObjectService.IncrementReferenceCountAsync(
				storedObject.Id,
				cancellationToken
			);
		}

		FileStorageSignedUrl? signedUrl = null;
		if (uploadRequired)
		{
			signedUrl = await _storageService.CreateUploadUrlAsync(
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
			ReusedStoredObject = reusedStoredObject
		});
	}

	[Authorize(Roles = "Admin,Coach")]
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

		var pendingFile = await _fileService.GetByIdAsync(fileId, cancellationToken);
		var file = await _fileService.MarkUploadedAsync(fileId, cancellationToken);

		if (file is null)
		{
			return NotFound();
		}

		if (pendingFile?.StoredObjectId is Guid storedObjectId)
		{
			await _storedObjectService.MarkUploadedAsync(
				storedObjectId,
				cancellationToken
			);
		}

		return Ok(file);
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

		var file = await _fileService.GetByIdAsync(fileId, cancellationToken);

		if (file is null || file.Status != ClubFileStatus.Uploaded)
		{
			return NotFound();
		}

		if (!await CanCurrentUserAccessFileAsync(file, cancellationToken))
		{
			return Forbid();
		}

		var signedUrl = await _storageService.CreateDownloadUrlAsync(
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

	[Authorize(Roles = "Admin,Coach")]
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

		var file = await _fileService.GetByIdAsync(fileId, cancellationToken);
		var deleted = await _fileService.SoftDeleteAsync(
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
			await _storedObjectService.DecrementReferenceCountAsync(
				storedObjectId,
				cancellationToken
			);
		}

		return NoContent();
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
				await _postService.GetByIdAsync(linkedEntityId, cancellationToken) is not null,
			ClubFileLinkedEntityType.Event =>
				await _eventService.GetByIdAsync(linkedEntityId, cancellationToken) is not null,
			ClubFileLinkedEntityType.Player =>
				await _playerService.GetByIdAsync(linkedEntityId, cancellationToken) is not null,
			ClubFileLinkedEntityType.ClubDocument => true,
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
				await _postService.GetByIdAsync(file.LinkedEntityId, cancellationToken) is not null,
			ClubFileLinkedEntityType.Event => await CanCurrentUserAccessEventFileAsync(file.LinkedEntityId, cancellationToken),
			ClubFileLinkedEntityType.Player => IsAdminOrCoach(),
			ClubFileLinkedEntityType.ClubDocument => IsAdminOrCoach(),
			_ => false
		};
	}

	private async Task<bool> CanCurrentUserAccessEventFileAsync(
		Guid eventId,
		CancellationToken cancellationToken
	)
	{
		var clubEvent = await _eventService.GetByIdAsync(eventId, cancellationToken);

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
