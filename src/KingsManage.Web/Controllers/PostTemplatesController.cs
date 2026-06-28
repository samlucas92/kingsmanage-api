using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Coach")]
[Route("api/post-templates")]
public class PostTemplatesController : ControllerBase
{
	private readonly IClubPostTemplateService _service;

	public PostTemplatesController(IClubPostTemplateService service)
	{
		_service = service;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<ClubPostTemplate>>> GetAll(CancellationToken cancellationToken) =>
		Ok(await _service.GetAllAsync(cancellationToken));

	[HttpPost]
	public async Task<ActionResult<ClubPostTemplate>> Create(
		ClubPostTemplate template,
		CancellationToken cancellationToken
	)
	{
		var error = Validate(template);
		if (error is not null) return BadRequest(error);
		return Ok(await _service.CreateAsync(template, cancellationToken));
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
		template.Id = id;
		var updated = await _service.UpdateAsync(template, cancellationToken);
		return updated is null ? NotFound() : Ok(updated);
	}

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
		await _service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

	private static string? Validate(ClubPostTemplate template)
	{
		if (string.IsNullOrWhiteSpace(template.Name)) return "Template name is required.";
		if (string.IsNullOrWhiteSpace(template.TitleTemplate)) return "Title template is required.";
		if (string.IsNullOrWhiteSpace(template.BodyTemplate)) return "Body template is required.";
		return null;
	}
}
