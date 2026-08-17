using System.Security.Claims;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Route("api/organization-documents")]
[Authorize(Policy = "TeamManagement")]
public sealed class OrganizationDocumentsController : ControllerBase
{
	private readonly IOrganizationDocumentService documents;
	private readonly RichTextAssetService richTextAssets;
	private readonly IHandoverVaultService vault;

	public OrganizationDocumentsController(
		IOrganizationDocumentService documents,
		RichTextAssetService richTextAssets,
		IHandoverVaultService vault)
	{
		this.documents = documents;
		this.richTextAssets = richTextAssets;
		this.vault = vault;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<ClubPost>>> GetAll(CancellationToken cancellationToken) =>
		Ok(await documents.GetAllAsync(cancellationToken));

	[HttpGet("{id:guid}")]
	public async Task<ActionResult<ClubPost>> Get(Guid id, CancellationToken cancellationToken)
	{
		var document = await documents.GetByIdAsync(id, cancellationToken);
		return document is null ? NotFound() : Ok(document);
	}

	[HttpPost]
	public async Task<ActionResult<ClubPost>> Create(
		SaveOrganizationDocumentModel model,
		CancellationToken cancellationToken)
	{
		var validation = Validate(model);
		if (validation is not null)
		{
			return BadRequest(validation);
		}

		var userId = CurrentUserId();
		var document = new ClubPost
		{
			Id = Guid.NewGuid(),
			Title = model.Title,
			Body = model.Body,
			CreatedByUserId = userId,
			CreatedByUserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty
		};
		try
		{
			document.Body = await richTextAssets.SynchronizeAsync(
				document.Body,
				null,
				ClubFileLinkedEntityType.ClubDocument,
				document.Id,
				userId,
				document.CreatedByUserEmail,
				cancellationToken);
		}
		catch (InvalidOperationException exception)
		{
			return BadRequest(exception.Message);
		}

		var created = await documents.CreateAsync(document, cancellationToken);
		return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
	}

	[HttpPut("{id:guid}")]
	public async Task<ActionResult<ClubPost>> Update(
		Guid id,
		SaveOrganizationDocumentModel model,
		CancellationToken cancellationToken)
	{
		var validation = Validate(model);
		if (validation is not null)
		{
			return BadRequest(validation);
		}

		var existing = await documents.GetByIdAsync(id, cancellationToken);
		if (existing is null)
		{
			return NotFound();
		}

		try
		{
			existing.Body = await richTextAssets.SynchronizeAsync(
				model.Body,
				existing.Body,
				ClubFileLinkedEntityType.ClubDocument,
				existing.Id,
				CurrentUserId(),
				User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
				cancellationToken);
		}
		catch (InvalidOperationException exception)
		{
			return BadRequest(exception.Message);
		}
		existing.Title = model.Title;
		return Ok(await documents.UpdateAsync(existing, cancellationToken));
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPatch("{id:guid}/archive")]
	public async Task<ActionResult<ClubPost>> Archive(Guid id, [FromBody] bool archived, CancellationToken cancellationToken)
	{
		var document = await documents.SetArchivedAsync(id, archived, cancellationToken);
		if (document is not null && archived)
		{
			await vault.NotifyDocumentArchivedAsync(id, CurrentUserId(), cancellationToken);
		}
		return document is null ? NotFound() : Ok(document);
	}

	private static string? Validate(SaveOrganizationDocumentModel model)
	{
		if (string.IsNullOrWhiteSpace(model.Title)) return "Document title is required.";
		if (string.IsNullOrWhiteSpace(RichTextBody.ToPlainText(model.Body))) return "Document content is required.";
		return null;
	}

	private Guid CurrentUserId()
	{
		var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
		if (Guid.TryParse(claim, out var id)) return id;
		throw new UnauthorizedAccessException("Current user id is unavailable.");
	}
}
