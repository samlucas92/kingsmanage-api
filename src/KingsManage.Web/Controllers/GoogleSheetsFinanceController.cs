using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Policy = "ClubAdmin")]
[Route("api/finance/google-sheets")]
public sealed class GoogleSheetsFinanceController : ControllerBase
{
	private readonly IGoogleSheetsFinanceSyncService sync;

	public GoogleSheetsFinanceController(IGoogleSheetsFinanceSyncService sync)
	{
		this.sync = sync;
	}

	[HttpPost("players/{playerId}/import-preview")]
	public async Task<ActionResult<GoogleSheetsFinanceImportPreviewViewModel>> PreviewPlayerImport(
		string playerId,
		GoogleSheetsFinanceSyncRequest request,
		CancellationToken cancellationToken)
	{
		if (!Guid.TryParse(playerId, out var parsedPlayerId)) return BadRequest("Player id must be a valid GUID.");
		if (request.SeasonId == Guid.Empty) return BadRequest("Season id is required.");
		return await Run(() => sync.PreviewPlayerImportAsync(parsedPlayerId, request.SeasonId, cancellationToken));
	}

	[HttpPost("import-preview")]
	public async Task<ActionResult<GoogleSheetsFinanceImportPreviewViewModel>> PreviewSeasonImport(
		GoogleSheetsFinanceSyncRequest request,
		CancellationToken cancellationToken)
	{
		if (request.SeasonId == Guid.Empty) return BadRequest("Season id is required.");
		return await Run(() => sync.PreviewSeasonImportAsync(request.SeasonId, cancellationToken));
	}

	[HttpPost("import")]
	public async Task<ActionResult<GoogleSheetsFinanceImportResultViewModel>> ImportSelected(
		GoogleSheetsFinanceImportRequest request,
		CancellationToken cancellationToken)
	{
		if (request.SeasonId == Guid.Empty) return BadRequest("Season id is required.");
		return await Run(() => sync.ImportSelectedAsync(request.SeasonId, request.TransactionIds, cancellationToken));
	}

	[HttpDelete("payments")]
	public async Task<ActionResult<GoogleSheetsFinanceResetViewModel>> ResetSeasonPayments(
		[FromQuery] Guid seasonId,
		CancellationToken cancellationToken)
	{
		if (seasonId == Guid.Empty) return BadRequest("Season id is required.");
		return await Run(() => sync.ResetSeasonPaymentsAsync(seasonId, cancellationToken));
	}

	[HttpPost("players/{playerId}/export")]
	public async Task<ActionResult<GoogleSheetsFinanceSyncViewModel>> Export(
		string playerId,
		GoogleSheetsFinanceSyncRequest request,
		CancellationToken cancellationToken)
	{
		if (!Guid.TryParse(playerId, out var parsedPlayerId)) return BadRequest("Player id must be a valid GUID.");
		if (request.SeasonId == Guid.Empty) return BadRequest("Season id is required.");
		return await Run(() => sync.ExportPlayerAsync(parsedPlayerId, request.SeasonId, cancellationToken));
	}

	private async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
	{
		try
		{
			return Ok(await action());
		}
		catch (InvalidOperationException error)
		{
			return BadRequest(error.Message);
		}
	}
}
