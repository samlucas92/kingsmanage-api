using KingsManage;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/post-templates")]
public class PostTemplatesController : ControllerBase
{
	private readonly IClubPostTemplateService service;
	private readonly RichTextAssetService richTextAssets;

	public PostTemplatesController(
		IClubPostTemplateService service,
		RichTextAssetService richTextAssets
	)
	{
		this.service = service;
		this.richTextAssets = richTextAssets;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<ClubPostTemplate>>> GetAll(CancellationToken cancellationToken) =>
		Ok(await service.GetAllAsync(cancellationToken));

	[HttpPost]
	public async Task<ActionResult<ClubPostTemplate>> Create(
		ClubPostTemplate template,
		CancellationToken cancellationToken
	)
	{
		var error = Validate(template);
		if (error is not null) return BadRequest(error);
		template.Id = Guid.NewGuid();
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		try
		{
			template.BodyTemplate = await richTextAssets.SynchronizeAsync(
				template.BodyTemplate,
				null,
				ClubFileLinkedEntityType.PostTemplate,
				template.Id,
				userId.Value,
				User.Identity?.Name ?? string.Empty,
				cancellationToken
			);
		}
		catch (InvalidOperationException exception)
		{
			return BadRequest(exception.Message);
		}
		return Ok(await service.CreateAsync(template, cancellationToken));
	}

	[HttpPut("{id:guid}")]
	public async Task<ActionResult<ClubPostTemplate>> Update(
		Guid id,
		ClubPostTemplate template,
		CancellationToken cancellationToken
	)
	{
		var error = Validate(template);
		if (error is not null) return BadRequest(error);
		var existing = await service.GetByIdAsync(id, cancellationToken);
		if (existing is null) return NotFound();
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		template.Id = id;
		try
		{
			template.BodyTemplate = await richTextAssets.SynchronizeAsync(
				template.BodyTemplate,
				existing.BodyTemplate,
				ClubFileLinkedEntityType.PostTemplate,
				id,
				userId.Value,
				User.Identity?.Name ?? string.Empty,
				cancellationToken
			);
		}
		catch (InvalidOperationException exception)
		{
			return BadRequest(exception.Message);
		}
		var updated = await service.UpdateAsync(template, cancellationToken);
		return updated is null ? NotFound() : Ok(updated);
	}

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
	{
		var existing = await service.GetByIdAsync(id, cancellationToken);
		if (existing is null) return NotFound();
		var userId = GetCurrentUserId();
		if (userId is null) return BadRequest("Current user id is invalid.");
		if (!await service.DeleteAsync(id, cancellationToken)) return NotFound();
		await richTextAssets.DeleteAllAsync(
			existing.BodyTemplate,
			ClubFileLinkedEntityType.PostTemplate,
			id,
			userId.Value,
			cancellationToken
		);
		return NoContent();
	}

	private static string? Validate(ClubPostTemplate template)
	{
		if (string.IsNullOrWhiteSpace(template.Name)) return "Template name is required.";
		if (string.IsNullOrWhiteSpace(template.TitleTemplate)) return "Title template is required.";
		if (string.IsNullOrWhiteSpace(template.BodyTemplate)) return "Body template is required.";
		return null;
	}

	private Guid? GetCurrentUserId()
	{
		var value =
			User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
			User.FindFirst("sub")?.Value;
		return Guid.TryParse(value, out var userId) ? userId : null;
	}
}
