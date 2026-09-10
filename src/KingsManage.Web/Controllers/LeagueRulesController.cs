using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/league-rules")]
public sealed class LeagueRulesController : ControllerBase
{
	private readonly ILeagueRuleService service;
	public LeagueRulesController(ILeagueRuleService service) => this.service = service;

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<LeagueRule>>> GetAll(CancellationToken cancellationToken) =>
		Ok(await service.GetAllAsync(cancellationToken));

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost]
	public async Task<ActionResult<LeagueRule>> Create(SaveLeagueRuleModel model, CancellationToken cancellationToken)
	{
		var error = Validate(model);
		if (error is not null) return BadRequest(error);
		var created = await service.CreateAsync(model.ToRule(), cancellationToken);
		return Ok(created);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPut("{id:guid}")]
	public async Task<ActionResult<LeagueRule>> Update(Guid id, SaveLeagueRuleModel model, CancellationToken cancellationToken)
	{
		var error = Validate(model);
		if (error is not null) return BadRequest(error);
		var updated = await service.UpdateAsync(model.ToRule(id), cancellationToken);
		return updated is null ? NotFound() : Ok(updated);
	}

	private static string? Validate(SaveLeagueRuleModel model)
	{
		if (string.IsNullOrWhiteSpace(model.Name)) return "Rule name is required.";
		if (model.HigherTeamId == Guid.Empty || model.RestrictedTeamId == Guid.Empty) return "Both teams are required.";
		if (model.HigherTeamId == model.RestrictedTeamId) return "Choose two different teams.";
		if (model.RuleType == LeagueRuleType.RecentHigherTeamAppearanceLimit && (!model.MaxPlayers.HasValue || model.MaxPlayers < 0))
			return "A valid player limit is required.";
		return null;
	}
}
