using KingsManage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "OrganizationAdmin")]
[Route("api/organization")]
public sealed class OrganizationController : ControllerBase
{
	private readonly IOrganizationService _organizations;
	private readonly ISportsClubService _clubs;

	public OrganizationController(IOrganizationService organizations, ISportsClubService clubs)
	{
		_organizations = organizations;
		_clubs = clubs;
	}

	[HttpGet]
	public async Task<ActionResult<Organization>> Get(CancellationToken cancellationToken) =>
		await _organizations.GetCurrentAsync(cancellationToken) is { } organization
			? Ok(organization)
			: NotFound();

	[HttpPut]
	public async Task<ActionResult<Organization>> Update(Organization organization, CancellationToken cancellationToken)
	{
		var error = ValidateNameAndSlug(organization.Name, organization.Slug);
		if (error is not null) return BadRequest(error);
		return await _organizations.UpdateCurrentAsync(organization, cancellationToken) is { } updated
			? Ok(updated)
			: NotFound();
	}

	[HttpGet("clubs")]
	public async Task<ActionResult<IReadOnlyList<SportsClub>>> GetClubs(CancellationToken cancellationToken) =>
		Ok(await _clubs.GetAllAsync(cancellationToken));

	[HttpPost("clubs")]
	public async Task<ActionResult<SportsClub>> CreateClub(SportsClub club, CancellationToken cancellationToken)
	{
		var error = ValidateClub(club);
		if (error is not null) return BadRequest(error);
		var created = await _clubs.CreateAsync(club, cancellationToken);
		return Created($"/api/organization/clubs/{created.Id}", created);
	}

	[HttpPut("clubs/{id:guid}")]
	public async Task<ActionResult<SportsClub>> UpdateClub(Guid id, SportsClub club, CancellationToken cancellationToken)
	{
		var error = ValidateClub(club);
		if (error is not null) return BadRequest(error);
		return await _clubs.UpdateAsync(id, club, cancellationToken) is { } updated ? Ok(updated) : NotFound();
	}

	[HttpPatch("clubs/{id:guid}/active")]
	public async Task<ActionResult<SportsClub>> SetClubActive(Guid id, [FromBody] SetActiveRequest request, CancellationToken cancellationToken) =>
		await _clubs.SetActiveAsync(id, request.IsActive, cancellationToken) is { } updated ? Ok(updated) : NotFound();

	private static string? ValidateClub(SportsClub club) =>
		ValidateNameAndSlug(club.Name, club.Slug) ??
		(string.IsNullOrWhiteSpace(club.SportKey) ? "Sport is required." : null);

	private static string? ValidateNameAndSlug(string name, string slug)
	{
		if (string.IsNullOrWhiteSpace(name)) return "Name is required.";
		if (name.Trim().Length > 100) return "Name must be 100 characters or fewer.";
		if (string.IsNullOrWhiteSpace(slug)) return "Slug is required.";
		if (!System.Text.RegularExpressions.Regex.IsMatch(slug.Trim(), "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
			return "Slug must contain lowercase letters, numbers and single hyphens only.";
		return null;
	}

	public sealed class SetActiveRequest
	{
		public bool IsActive { get; set; }
	}
}
