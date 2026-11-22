using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "TeamManagement")]
[Route("api/matches/{id}/exports")]
public sealed class MatchExportsController(
	IMatchService matchService,
	IPlayerService playerService,
	IClubTeamService clubTeamService,
	IGoogleSheetsMatchExporter googleSheetsExporter) : ControllerBase
{
	[HttpPost("google-sheets")]
	public async Task<ActionResult<GoogleSheetsMatchExportResult>> ExportToGoogleSheets(
		string id,
		CancellationToken cancellationToken)
	{
		if (!Guid.TryParse(id, out var matchId) || matchId == Guid.Empty)
		{
			return BadRequest("Match id must be a valid identifier.");
		}

		if (!googleSheetsExporter.IsConfigured)
		{
			return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
			{
				Title = "Google Sheets export is not configured",
				Detail = "Set GOOGLE_SHEETS_EXPORT_WEBHOOK_URL and GOOGLE_SHEETS_EXPORT_SECRET on the API."
			});
		}

		var match = await matchService.GetByIdAsync(matchId, cancellationToken);
		if (match is null)
		{
			return NotFound();
		}

		if (!match.IsCompleted || match.Result is null)
		{
			return Conflict(new ProblemDetails
			{
				Title = "Match is not ready to export",
				Detail = "Enter the final result before exporting this match."
			});
		}

		if (match.PlayerStats.Count == 0)
		{
			return Conflict(new ProblemDetails
			{
				Title = "Player stats are not ready to export",
				Detail = "Save the match events before exporting player stats."
			});
		}

		var players = await playerService.GetAllAsync(cancellationToken);
		var teamId = match.TeamId ?? DefaultClubTeams.FromLegacy(match.Team);
		var team = await clubTeamService.GetByIdAsync(teamId, cancellationToken);
		var teamName = team?.DisplayName ?? match.Team.ToString();

		try
		{
			return Ok(await googleSheetsExporter.ExportAsync(
				match,
				players,
				teamId,
				teamName,
				cancellationToken));
		}
		catch (GoogleSheetsExportException exception)
		{
			return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
			{
				Title = "Google Sheets export failed",
				Detail = exception.Message
			});
		}
	}
}
