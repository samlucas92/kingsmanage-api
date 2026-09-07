using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/opposition-teams")]
public sealed class OppositionTeamsController : ControllerBase
{
	private readonly IOppositionTeamService teamService;

	public OppositionTeamsController(IOppositionTeamService teamService) => this.teamService = teamService;

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<OppositionTeam>>> GetAll(CancellationToken cancellationToken) =>
		Ok(await teamService.GetAllAsync(cancellationToken));

	[HttpGet("{id:guid}")]
	public async Task<ActionResult<OppositionTeam>> GetById(Guid id, CancellationToken cancellationToken)
	{
		var team = await teamService.GetByIdAsync(id, cancellationToken);
		return team is null ? NotFound() : Ok(team);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost]
	public async Task<ActionResult<OppositionTeam>> Create(SaveOppositionTeamModel model, CancellationToken cancellationToken)
	{
		var error = Validate(model);
		if (error is not null) return BadRequest(error);
		try
		{
			var created = await teamService.CreateAsync(model.ToTeam(), cancellationToken);
			return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
		}
		catch (ArgumentException exception)
		{
			return Conflict(exception.Message);
		}
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPut("{id:guid}")]
	public async Task<ActionResult<OppositionTeam>> Update(Guid id, SaveOppositionTeamModel model, CancellationToken cancellationToken)
	{
		var error = Validate(model);
		if (error is not null) return BadRequest(error);
		try
		{
			var updated = await teamService.UpdateAsync(model.ToTeam(id), cancellationToken);
			return updated is null ? NotFound() : Ok(updated);
		}
		catch (ArgumentException exception)
		{
			return Conflict(exception.Message);
		}
	}

	private static string? Validate(SaveOppositionTeamModel model)
	{
		if (string.IsNullOrWhiteSpace(model.Name)) return "Team name is required.";
		if (model.Name.Trim().Length > 120) return "Team name must be 120 characters or fewer.";
		if ((model.Location ?? string.Empty).Trim().Length > 500) return "Location must be 500 characters or fewer.";
		return null;
	}
}
