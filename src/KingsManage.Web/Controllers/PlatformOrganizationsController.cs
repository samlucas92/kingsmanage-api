using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "SiteAdmin")]
[Route("api/platform/organizations")]
public sealed class PlatformOrganizationsController : ControllerBase
{
	private readonly IOrganizationService _organizations;

	public PlatformOrganizationsController(IOrganizationService organizations)
	{
		_organizations = organizations;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<Organization>>> GetAll(
		CancellationToken cancellationToken) =>
		Ok(await _organizations.GetAllAsync(cancellationToken));

	[HttpPost]
	public async Task<ActionResult<Organization>> Create(
		Organization organization,
		CancellationToken cancellationToken)
	{
		var error = Validate(organization);
		if (error is not null) return BadRequest(error);
		var created = await _organizations.CreateAsync(organization, cancellationToken);
		return created is null
			? Conflict("An organization with this slug already exists.")
			: Created($"/api/platform/organizations/{created.Id}", created);
	}

	[HttpPut("{id:guid}")]
	public async Task<ActionResult<Organization>> Update(
		Guid id,
		Organization organization,
		CancellationToken cancellationToken)
	{
		var error = Validate(organization);
		if (error is not null) return BadRequest(error);
		var updated = await _organizations.UpdateAsync(id, organization, cancellationToken);
		return updated is null
			? NotFound("Organization was not found or its slug is already in use.")
			: Ok(updated);
	}

	[HttpPatch("{id:guid}/active")]
	public async Task<ActionResult<Organization>> SetActive(
		Guid id,
		SetActiveRequest request,
		CancellationToken cancellationToken) =>
		await _organizations.SetActiveAsync(id, request.IsActive, cancellationToken) is { } updated
			? Ok(updated)
			: NotFound();

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(
		Guid id,
		CancellationToken cancellationToken)
	{
		var result = await _organizations.DeleteAsync(id, cancellationToken);
		return result switch
		{
			OrganizationDeleteResult.Deleted => NoContent(),
			OrganizationDeleteResult.NotFound => NotFound(),
			OrganizationDeleteResult.HasClubs => Conflict(
				"Archive the organization instead. Permanent deletion is only available before clubs have been created."),
			_ => StatusCode(StatusCodes.Status500InternalServerError)
		};
	}

	private static string? Validate(Organization organization)
	{
		if (string.IsNullOrWhiteSpace(organization.Name)) return "Name is required.";
		if (organization.Name.Trim().Length > 100) return "Name must be 100 characters or fewer.";
		if (string.IsNullOrWhiteSpace(organization.Slug)) return "Slug is required.";
		if (!System.Text.RegularExpressions.Regex.IsMatch(
			organization.Slug.Trim(),
			"^[a-z0-9]+(?:-[a-z0-9]+)*$"))
			return "Slug must contain lowercase letters, numbers and single hyphens only.";
		return null;
	}

	public sealed class SetActiveRequest
	{
		public bool IsActive { get; set; }
	}
}
