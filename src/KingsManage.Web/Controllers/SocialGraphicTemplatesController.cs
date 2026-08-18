using System.Text.Json;
using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/social-graphic-templates")]
public class SocialGraphicTemplatesController : ControllerBase
{
	private const int MaximumDefinitionLength = 250_000;
	private static readonly HashSet<string> SupportedTemplateIds =
		new(StringComparer.OrdinalIgnoreCase)
		{
			"blank-editorial-gold",
			"upcoming-editorial-gold",
			"matchday-editorial-gold",
			"lineup-editorial-gold",
			"result-editorial-gold"
		};
	private readonly ISocialGraphicTemplateService service;

	public SocialGraphicTemplatesController(ISocialGraphicTemplateService service)
	{
		this.service = service;
	}

	[HttpGet("{templateId}")]
	public async Task<ActionResult<SocialGraphicTemplateResponse>> Get(
		string templateId,
		CancellationToken cancellationToken
	)
	{
		if (!IsSupported(templateId)) return NotFound();
		var customization = await service.GetAsync(templateId, cancellationToken);
		return Ok(new SocialGraphicTemplateResponse
		{
			TemplateId = templateId,
			Customization = customization is null ? null : ToViewModel(customization)
		});
	}

	[HttpGet("{templateId}/revisions")]
	public async Task<ActionResult<IReadOnlyList<SocialGraphicTemplateRevisionViewModel>>> GetRevisions(
		string templateId,
		[FromQuery] int limit = 20,
		CancellationToken cancellationToken = default
	)
	{
		if (!IsSupported(templateId)) return NotFound();
		var revisions = await service.GetRevisionsAsync(
			templateId,
			Math.Clamp(limit, 1, 50),
			cancellationToken
		);
		return Ok(revisions.Select(ToViewModel).ToList());
	}

	[HttpPut("{templateId}")]
	public async Task<ActionResult<SocialGraphicTemplateCustomizationViewModel>> Save(
		string templateId,
		SaveSocialGraphicTemplateRequest request,
		CancellationToken cancellationToken
	)
	{
		if (!IsSupported(templateId)) return NotFound();
		var validationError = Validate(request);
		if (validationError is not null) return BadRequest(validationError);
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");

		var result = await service.SaveAsync(
			templateId,
			request.SchemaVersion,
			request.DefinitionJson,
			request.ExpectedRevision,
			userId.Value,
			cancellationToken
		);
		if (result.Status == SocialGraphicTemplateSaveStatus.Conflict)
		{
			return Conflict(new
			{
				message = "This template was changed by someone else. Reload it before saving again."
			});
		}
		return Ok(ToViewModel(result.Customization!));
	}

	[HttpPost("{templateId}/revisions/{revision:int}/restore")]
	public async Task<ActionResult<SocialGraphicTemplateCustomizationViewModel>> RestoreRevision(
		string templateId,
		int revision,
		RestoreSocialGraphicTemplateRevisionRequest request,
		CancellationToken cancellationToken
	)
	{
		if (!IsSupported(templateId)) return NotFound();
		if (revision < 1 || request.ExpectedRevision < 0) return BadRequest("Revision is invalid.");
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		var result = await service.RestoreRevisionAsync(
			templateId,
			revision,
			request.ExpectedRevision,
			userId.Value,
			cancellationToken
		);
		return result.Status switch
		{
			SocialGraphicTemplateSaveStatus.Saved => Ok(ToViewModel(result.Customization!)),
			SocialGraphicTemplateSaveStatus.RevisionNotFound => NotFound(),
			_ => Conflict(new
			{
				message = "This template was changed by someone else. Reload it before restoring a revision."
			})
		};
	}

	[HttpDelete("{templateId}")]
	public async Task<IActionResult> Reset(
		string templateId,
		[FromQuery] int expectedRevision,
		CancellationToken cancellationToken
	)
	{
		if (!IsSupported(templateId)) return NotFound();
		if (expectedRevision < 0) return BadRequest("Expected revision is invalid.");
		var result = await service.ResetAsync(
			templateId,
			expectedRevision,
			cancellationToken
		);
		return result switch
		{
			SocialGraphicTemplateResetResult.Conflict => Conflict(new
			{
				message = "This template was changed by someone else. Reload it before restoring the original."
			}),
			_ => NoContent()
		};
	}

	private static string? Validate(SaveSocialGraphicTemplateRequest request)
	{
		if (request.SchemaVersion != 1) return "Only template schema version 1 is supported.";
		if (request.ExpectedRevision < 0) return "Expected revision is invalid.";
		if (string.IsNullOrWhiteSpace(request.DefinitionJson)) return "Template definition is required.";
		if (request.DefinitionJson.Length > MaximumDefinitionLength) return "Template definition is too large.";

		try
		{
			using var document = JsonDocument.Parse(
				request.DefinitionJson,
				new JsonDocumentOptions { MaxDepth = 64 }
			);
			if (document.RootElement.ValueKind != JsonValueKind.Object)
			{
				return "Template definition must be a JSON object.";
			}
			if (
				!document.RootElement.TryGetProperty("version", out var version) ||
				version.ValueKind != JsonValueKind.Number ||
				!version.TryGetInt32(out var parsedVersion) ||
				parsedVersion != request.SchemaVersion
			)
			{
				return "Template definition version does not match its schema version.";
			}
		}
		catch (JsonException)
		{
			return "Template definition must contain valid JSON.";
		}

		return null;
	}

	private static bool IsSupported(string templateId) =>
		SupportedTemplateIds.Contains(templateId);

	private Guid? GetCurrentUserId()
	{
		var value =
			User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
			User.FindFirst("sub")?.Value;
		return Guid.TryParse(value, out var userId) ? userId : null;
	}

	private static SocialGraphicTemplateCustomizationViewModel ToViewModel(
		SocialGraphicTemplateCustomization customization
	) => new()
	{
		Id = customization.Id,
		TemplateId = customization.TemplateId,
		SchemaVersion = customization.SchemaVersion,
		DefinitionJson = customization.DefinitionJson,
		Revision = customization.Revision,
		UpdatedByUserId = customization.UpdatedByUserId,
		CreatedAt = customization.CreatedAt,
		UpdatedAt = customization.UpdatedAt
	};

	private static SocialGraphicTemplateRevisionViewModel ToViewModel(
		SocialGraphicTemplateRevision revision
	) => new()
	{
		Revision = revision.Revision,
		SchemaVersion = revision.SchemaVersion,
		DefinitionJson = revision.DefinitionJson,
		CreatedByUserId = revision.CreatedByUserId,
		CreatedAt = revision.CreatedAt
	};
}
