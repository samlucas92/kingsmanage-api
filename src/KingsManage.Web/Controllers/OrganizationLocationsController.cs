using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/organization-locations")]
public sealed class OrganizationLocationsController : ControllerBase
{
	private readonly IOrganizationLocationService locationService;

	public OrganizationLocationsController(IOrganizationLocationService locationService)
	{
		this.locationService = locationService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<OrganizationLocation>>> GetAll(
		CancellationToken cancellationToken
	)
	{
		return Ok(await locationService.GetAllAsync(cancellationToken));
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost]
	public async Task<ActionResult<OrganizationLocation>> Create(
		OrganizationLocation location,
		CancellationToken cancellationToken
	)
	{
		var validationError = Validate(location);
		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var created = await locationService.CreateAsync(location, cancellationToken);
		return Created($"/api/organization-locations/{created.Id}", created);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPut("{id:guid}")]
	public async Task<ActionResult<OrganizationLocation>> Update(
		Guid id,
		OrganizationLocation location,
		CancellationToken cancellationToken
	)
	{
		var validationError = Validate(location);
		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		location.Id = id;
		var updated = await locationService.UpdateAsync(location, cancellationToken);
		return updated is null ? NotFound() : Ok(updated);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
	{
		return await locationService.DeleteAsync(id, cancellationToken)
			? NoContent()
			: NotFound();
	}

	private static string? Validate(OrganizationLocation location)
	{
		if (string.IsNullOrWhiteSpace(location.Name))
		{
			return "Location name is required.";
		}

		if (location.Name.Trim().Length > 100)
		{
			return "Location name must be 100 characters or fewer.";
		}

		if (string.IsNullOrWhiteSpace(location.Address))
		{
			return "Address is required.";
		}

		if (location.Address.Trim().Length > 300)
		{
			return "Address must be 300 characters or fewer.";
		}

		if ((location.Notes ?? string.Empty).Trim().Length > 500)
		{
			return "Notes must be 500 characters or fewer.";
		}

		return null;
	}
}
