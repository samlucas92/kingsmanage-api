using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "ClubAdmin")]
[Route("api/finance/google-sheets/players/{playerId}")]
public sealed class GoogleSheetsFinanceController : ControllerBase
{
	private readonly IGoogleSheetsFinanceSyncService sync;

	public GoogleSheetsFinanceController(IGoogleSheetsFinanceSyncService sync)
	{
		this.sync = sync;
	}

	[HttpPost("import")]
	public async Task<ActionResult<GoogleSheetsFinanceSyncViewModel>> Import(
		string playerId,
		GoogleSheetsFinanceSyncRequest request,
		CancellationToken cancellationToken)
	{
		if (!Guid.TryParse(playerId, out var parsedPlayerId)) return BadRequest("Player id must be a valid GUID.");
		if (request.SeasonId == Guid.Empty) return BadRequest("Season id is required.");
		try
		{
			return Ok(await sync.ImportPlayerAsync(parsedPlayerId, request.SeasonId, cancellationToken));
		}
		catch (InvalidOperationException error)
		{
			return BadRequest(error.Message);
		}
	}

	[HttpPost("export")]
	public async Task<ActionResult<GoogleSheetsFinanceSyncViewModel>> Export(
		string playerId,
		GoogleSheetsFinanceSyncRequest request,
		CancellationToken cancellationToken)
	{
		if (!Guid.TryParse(playerId, out var parsedPlayerId)) return BadRequest("Player id must be a valid GUID.");
		if (request.SeasonId == Guid.Empty) return BadRequest("Season id is required.");
		try
		{
			return Ok(await sync.ExportPlayerAsync(parsedPlayerId, request.SeasonId, cancellationToken));
		}
		catch (InvalidOperationException error)
		{
			return BadRequest(error.Message);
		}
	}
}
