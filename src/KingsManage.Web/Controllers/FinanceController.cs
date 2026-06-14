using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/finance")]
public class FinanceController : ControllerBase
{
	private readonly IFinanceService _financeService;
	private readonly IPlayerService _playerService;

	public FinanceController(
		IFinanceService financeService,
		IPlayerService playerService
	)
	{
		_financeService = financeService;
		_playerService = playerService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<PlayerFinanceViewModel>>> GetSeasonFinance(
		[FromQuery] string? seasonId,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseOptionalGuid(seasonId, "Season", out var parsedSeasonId, out var errorResult))
		{
			return errorResult!;
		}

		var players = await _playerService.GetAllAsync(cancellationToken);
		var transactions = await _financeService.GetSeasonTransactionsAsync(
			parsedSeasonId,
			cancellationToken
		);

		var viewModels = players
			.OrderBy(player => player.Name)
			.Select(player => PlayerFinanceViewModel.FromPlayer(
				player,
				parsedSeasonId,
				transactions
			))
			.ToList();

		return Ok(viewModels);
	}

	[HttpGet("player/{playerId}")]
	public async Task<ActionResult<PlayerFinanceViewModel>> GetPlayerFinance(
		string playerId,
		[FromQuery] string? seasonId,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(playerId, "Player", out var parsedPlayerId, out var errorResult))
		{
			return errorResult!;
		}

		if (!TryParseOptionalGuid(seasonId, "Season", out var parsedSeasonId, out errorResult))
		{
			return errorResult!;
		}

		var player = await _playerService.GetByIdAsync(parsedPlayerId, cancellationToken);

		if (player is null)
		{
			return NotFound();
		}

		var transactions = await _financeService.GetPlayerTransactionsAsync(
			parsedPlayerId,
			parsedSeasonId,
			cancellationToken
		);

		return Ok(PlayerFinanceViewModel.FromPlayer(
			player,
			parsedSeasonId,
			transactions
		));
	}

	[HttpPost("transactions")]
	public async Task<ActionResult<FinanceTransactionViewModel>> AddTransaction(
		FinanceTransactionRequest request,
		CancellationToken cancellationToken
	)
	{
		var validationError = ValidateTransactionRequest(request);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var player = await _playerService.GetByIdAsync(request.PlayerId, cancellationToken);

		if (player is null)
		{
			return NotFound("Player was not found.");
		}

		FinanceTransaction transaction;

		if (request.Type == FinanceTransactionType.Payment)
		{
			transaction = await _financeService.AddPaymentAsync(
				request.PlayerId,
				request.SeasonId,
				request.Amount,
				request.Note,
				cancellationToken
			);
		}
		else if (request.Type == FinanceTransactionType.Adjustment)
		{
			transaction = await _financeService.AddAdjustmentAsync(
				request.PlayerId,
				request.SeasonId,
				request.Amount,
				request.Note,
				cancellationToken
			);
		}
		else
		{
			transaction = await _financeService.AddTransactionAsync(
				new FinanceTransaction
				{
					PlayerId = request.PlayerId,
					SeasonId = request.SeasonId,
					Type = FinanceTransactionType.Charge,
					Amount = request.Amount,
					Note = request.Note ?? string.Empty
				},
				cancellationToken
			);
		}

		return Ok(FinanceTransactionViewModel.FromTransaction(transaction));
	}

	[HttpPut("players/{playerId}/amount-owed")]
	public async Task<ActionResult<FinanceTransactionViewModel>> SetPlayerAmountOwed(
		string playerId,
		[FromQuery] string? seasonId,
		[FromBody] FinanceAmountModel model,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(playerId, "Player", out var parsedPlayerId, out var errorResult))
		{
			return errorResult!;
		}

		if (!TryParseOptionalGuid(seasonId, "Season", out var parsedSeasonId, out errorResult))
		{
			return errorResult!;
		}

		if (model.Amount < 0)
		{
			return BadRequest("Amount owed must be 0 or above.");
		}

		var player = await _playerService.GetByIdAsync(parsedPlayerId, cancellationToken);

		if (player is null)
		{
			return NotFound();
		}

		var transaction = await _financeService.SetPlayerAmountOwedAsync(
			parsedPlayerId,
			parsedSeasonId,
			model.Amount,
			cancellationToken
		);

		return Ok(FinanceTransactionViewModel.FromTransaction(transaction));
	}

	[HttpDelete("transactions/{id}")]
	public async Task<IActionResult> DeleteTransaction(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Transaction", out var parsedTransactionId, out var errorResult))
		{
			return errorResult!;
		}

		var deleted = await _financeService.DeleteTransactionAsync(
			parsedTransactionId,
			cancellationToken
		);

		if (!deleted)
		{
			return NotFound();
		}

		return NoContent();
	}

	private static string? ValidateTransactionRequest(FinanceTransactionRequest request)
	{
		if (request.PlayerId == Guid.Empty)
		{
			return "Player id is required.";
		}

		if (request.Type == FinanceTransactionType.Payment && request.Amount <= 0)
		{
			return "Payment amount must be more than 0.";
		}

		if (request.Type == FinanceTransactionType.Charge && request.Amount < 0)
		{
			return "Charge amount must be 0 or above.";
		}

		if (request.Type == FinanceTransactionType.Adjustment && request.Amount == 0)
		{
			return "Adjustment amount cannot be 0.";
		}

		return null;
	}

	private bool TryParseOptionalGuid(
		string? id,
		string entityName,
		out Guid? parsedId,
		out BadRequestObjectResult? errorResult
	)
	{
		parsedId = null;
		errorResult = null;

		if (string.IsNullOrWhiteSpace(id))
		{
			return true;
		}

		if (!Guid.TryParse(id, out var value))
		{
			errorResult = BadRequest($"{entityName} id must be a valid GUID.");
			return false;
		}

		parsedId = value;
		return true;
	}

	private bool TryParseGuid(
		string id,
		string entityName,
		out Guid parsedId,
		out BadRequestObjectResult? errorResult
	)
	{
		parsedId = Guid.Empty;
		errorResult = null;

		if (string.IsNullOrWhiteSpace(id))
		{
			errorResult = BadRequest($"{entityName} id is required.");
			return false;
		}

		if (!Guid.TryParse(id, out parsedId))
		{
			errorResult = BadRequest($"{entityName} id must be a valid GUID.");
			return false;
		}

		return true;
	}
}

public class FinanceAmountModel
{
	public decimal Amount { get; set; }
}
