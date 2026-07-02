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

	private static string? ValidateClub(SportsClub club)
	{
		var basicError = ValidateNameAndSlug(club.Name, club.Slug) ??
			(string.IsNullOrWhiteSpace(club.SportKey) ? "Sport is required." : null);
		if (basicError is not null) return basicError;

		var sport = SportCatalog.Find(club.SportKey);
		if (sport is null) return "Sport is not supported.";

		club.CustomFormations ??= [];
		var builtInKeys = sport.Formations.Select(formation => formation.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var customKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var formation in club.CustomFormations)
		{
			if (string.IsNullOrWhiteSpace(formation.Name) || formation.Name.Trim().Length > 60)
				return "Each custom formation needs a name of 60 characters or fewer.";
			if (string.IsNullOrWhiteSpace(formation.Key) ||
				!System.Text.RegularExpressions.Regex.IsMatch(formation.Key.Trim(), "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
				return "Each custom formation needs a valid key.";
			if (builtInKeys.Contains(formation.Key) || !customKeys.Add(formation.Key))
				return "Custom formation keys must be unique and cannot replace a built-in formation.";
			if (formation.Slots is null || formation.Slots.Count != sport.PlayersPerSide)
				return $"Each {sport.Name} formation must contain {sport.PlayersPerSide} positions.";

			var slotKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var slot in formation.Slots)
			{
				if (string.IsNullOrWhiteSpace(slot.Key) || !slotKeys.Add(slot.Key))
					return "Every formation position needs a unique key.";
				if (string.IsNullOrWhiteSpace(slot.Label) || slot.Label.Trim().Length > 20)
					return "Every formation position needs a label of 20 characters or fewer.";
				if (slot.X is < 0 or > 100 || slot.Y is < 0 or > 100)
					return "Formation positions must remain on the playing surface.";
			}
		}

		return null;
	}

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
