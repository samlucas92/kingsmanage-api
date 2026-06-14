using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public class FinanceControllerTests
{
	[Test]
	public async Task GetSeasonFinance_ShouldReturnFinanceSummaryForEveryPlayer()
	{
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new FinanceController(financeService, playerService);
		var seasonId = Guid.NewGuid();
		var playerId = Guid.NewGuid();

		playerService.Players.Add(new Player
		{
			Id = playerId,
			Name = "Test Player",
			Number = 10,
			Positions = ["CM"],
			IsActive = true
		});

		await financeService.SetPlayerAmountOwedAsync(playerId, seasonId, 50m);
		await financeService.AddPaymentAsync(playerId, seasonId, 15m, "Bank transfer");
		await financeService.AddAdjustmentAsync(playerId, seasonId, -5m, "Discount");

		var result = await controller.GetSeasonFinance(seasonId.ToString(), CancellationToken.None);
		var okResult = result.Result as OkObjectResult;
		var summaries = okResult?.Value as IReadOnlyList<PlayerFinanceViewModel>;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(summaries, Is.Not.Null);
		Assert.That(summaries, Has.Count.EqualTo(1));
		Assert.That(summaries![0].PlayerId, Is.EqualTo(playerId));
		Assert.That(summaries[0].TotalCharged, Is.EqualTo(50m));
		Assert.That(summaries[0].TotalAdjustments, Is.EqualTo(-5m));
		Assert.That(summaries[0].AmountOwed, Is.EqualTo(45m));
		Assert.That(summaries[0].TotalPaid, Is.EqualTo(15m));
		Assert.That(summaries[0].Balance, Is.EqualTo(30m));
	}

	[Test]
	public async Task SetPlayerAmountOwed_WhenPlayerExists_ShouldCreateSeasonCharge()
	{
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new FinanceController(financeService, playerService);
		var seasonId = Guid.NewGuid();
		var playerId = Guid.NewGuid();

		playerService.Players.Add(new Player
		{
			Id = playerId,
			Name = "Test Player",
			Number = 7,
			Positions = ["ST"],
			IsActive = true
		});

		var result = await controller.SetPlayerAmountOwed(
			playerId.ToString(),
			seasonId.ToString(),
			new FinanceAmountModel { Amount = 60m },
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var transaction = okResult?.Value as FinanceTransactionViewModel;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(transaction, Is.Not.Null);
		Assert.That(transaction!.PlayerId, Is.EqualTo(playerId));
		Assert.That(transaction.SeasonId, Is.EqualTo(seasonId));
		Assert.That(transaction.Type, Is.EqualTo(FinanceTransactionType.Charge));
		Assert.That(transaction.Amount, Is.EqualTo(60m));
		Assert.That(transaction.Note, Is.EqualTo("Season amount owed"));
		Assert.That(financeService.Transactions, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task SetPlayerAmountOwed_WhenCalledTwice_ShouldUpdateExistingSeasonCharge()
	{
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new FinanceController(financeService, playerService);
		var seasonId = Guid.NewGuid();
		var playerId = Guid.NewGuid();

		playerService.Players.Add(new Player
		{
			Id = playerId,
			Name = "Test Player",
			Number = 4,
			Positions = ["CB"],
			IsActive = true
		});

		await controller.SetPlayerAmountOwed(
			playerId.ToString(),
			seasonId.ToString(),
			new FinanceAmountModel { Amount = 50m },
			CancellationToken.None
		);
		await controller.SetPlayerAmountOwed(
			playerId.ToString(),
			seasonId.ToString(),
			new FinanceAmountModel { Amount = 75m },
			CancellationToken.None
		);

		var chargeTransactions = financeService.Transactions
			.Where(transaction =>
				transaction.PlayerId == playerId &&
				transaction.SeasonId == seasonId &&
				transaction.Type == FinanceTransactionType.Charge)
			.ToList();

		Assert.That(chargeTransactions, Has.Count.EqualTo(1));
		Assert.That(chargeTransactions[0].Amount, Is.EqualTo(75m));
	}

	[Test]
	public async Task AddTransaction_WhenPayment_ShouldCreatePaymentTransaction()
	{
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new FinanceController(financeService, playerService);
		var seasonId = Guid.NewGuid();
		var playerId = Guid.NewGuid();

		playerService.Players.Add(new Player
		{
			Id = playerId,
			Name = "Test Player",
			Number = 11,
			Positions = ["LW"],
			IsActive = true
		});

		var result = await controller.AddTransaction(
			new FinanceTransactionRequest
			{
				PlayerId = playerId,
				SeasonId = seasonId,
				Type = FinanceTransactionType.Payment,
				Amount = 20m,
				Note = "Cash"
			},
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var transaction = okResult?.Value as FinanceTransactionViewModel;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(transaction, Is.Not.Null);
		Assert.That(transaction!.Type, Is.EqualTo(FinanceTransactionType.Payment));
		Assert.That(transaction.Amount, Is.EqualTo(20m));
		Assert.That(transaction.Note, Is.EqualTo("Cash"));
	}

	[Test]
	public async Task AddTransaction_WhenNegativeAdjustment_ShouldReduceAmountOwedInSeasonSummary()
	{
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new FinanceController(financeService, playerService);
		var seasonId = Guid.NewGuid();
		var playerId = Guid.NewGuid();

		playerService.Players.Add(new Player
		{
			Id = playerId,
			Name = "Test Player",
			Number = 8,
			Positions = ["CM"],
			IsActive = true
		});

		await financeService.SetPlayerAmountOwedAsync(playerId, seasonId, 50m);

		await controller.AddTransaction(
			new FinanceTransactionRequest
			{
				PlayerId = playerId,
				SeasonId = seasonId,
				Type = FinanceTransactionType.Adjustment,
				Amount = -10m,
				Note = "Discount"
			},
			CancellationToken.None
		);

		var result = await controller.GetSeasonFinance(seasonId.ToString(), CancellationToken.None);
		var okResult = result.Result as OkObjectResult;
		var summaries = okResult?.Value as IReadOnlyList<PlayerFinanceViewModel>;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(summaries, Is.Not.Null);
		Assert.That(summaries![0].TotalCharged, Is.EqualTo(50m));
		Assert.That(summaries[0].TotalAdjustments, Is.EqualTo(-10m));
		Assert.That(summaries[0].AmountOwed, Is.EqualTo(40m));
		Assert.That(summaries[0].Balance, Is.EqualTo(40m));
	}

	[Test]
	public async Task AddTransaction_WhenAdjustmentIsZero_ShouldReturnBadRequest()
	{
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new FinanceController(financeService, playerService);

		var result = await controller.AddTransaction(
			new FinanceTransactionRequest
			{
				PlayerId = Guid.NewGuid(),
				SeasonId = Guid.NewGuid(),
				Type = FinanceTransactionType.Adjustment,
				Amount = 0m,
				Note = "Invalid adjustment"
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task SetPlayerAmountOwed_WhenAmountIsNegative_ShouldReturnBadRequest()
	{
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new FinanceController(financeService, playerService);

		var result = await controller.SetPlayerAmountOwed(
			Guid.NewGuid().ToString(),
			Guid.NewGuid().ToString(),
			new FinanceAmountModel { Amount = -1m },
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	private class FakeFinanceService : IFinanceService
	{
		private const string SeasonChargeNote = "Season amount owed";

		public List<FinanceTransaction> Transactions { get; } = [];

		public Task<IReadOnlyList<FinanceTransaction>> GetSeasonTransactionsAsync(
			Guid? seasonId,
			CancellationToken cancellationToken = default
		)
		{
			var transactions = Transactions
				.Where(transaction => transaction.SeasonId == seasonId)
				.OrderByDescending(transaction => transaction.TransactionDate)
				.ToList();

			return Task.FromResult<IReadOnlyList<FinanceTransaction>>(transactions);
		}

		public Task<IReadOnlyList<FinanceTransaction>> GetPlayerTransactionsAsync(
			Guid playerId,
			Guid? seasonId = null,
			CancellationToken cancellationToken = default
		)
		{
			var transactions = Transactions
				.Where(transaction => transaction.PlayerId == playerId)
				.Where(transaction => !seasonId.HasValue || transaction.SeasonId == seasonId.Value)
				.OrderByDescending(transaction => transaction.TransactionDate)
				.ToList();

			return Task.FromResult<IReadOnlyList<FinanceTransaction>>(transactions);
		}

		public Task<FinanceTransaction> AddTransactionAsync(
			FinanceTransaction transaction,
			CancellationToken cancellationToken = default
		)
		{
			transaction.Id = transaction.Id == Guid.Empty ? Guid.NewGuid() : transaction.Id;
			transaction.Note = transaction.Note.Trim();
			transaction.TransactionDate = transaction.TransactionDate == default
				? DateTime.UtcNow
				: transaction.TransactionDate;
			transaction.CreatedAt = DateTime.UtcNow;
			transaction.UpdatedAt = DateTime.UtcNow;

			Transactions.Add(transaction);

			return Task.FromResult(transaction);
		}

		public Task<bool> DeleteTransactionAsync(
			Guid id,
			CancellationToken cancellationToken = default
		)
		{
			var transaction = Transactions.FirstOrDefault(transaction => transaction.Id == id);
			if (transaction is null)
			{
				return Task.FromResult(false);
			}

			Transactions.Remove(transaction);
			return Task.FromResult(true);
		}

		public async Task<FinanceTransaction> SetPlayerAmountOwedAsync(
			Guid playerId,
			Guid? seasonId,
			decimal amount,
			CancellationToken cancellationToken = default
		)
		{
			var safeAmount = decimal.Round(Math.Max(0, amount), 2);
			var existingCharge = Transactions.FirstOrDefault(transaction =>
				transaction.PlayerId == playerId &&
				transaction.SeasonId == seasonId &&
				transaction.Type == FinanceTransactionType.Charge &&
				transaction.Note == SeasonChargeNote
			);

			if (existingCharge is null)
			{
				return await AddTransactionAsync(
					new FinanceTransaction
					{
						PlayerId = playerId,
						SeasonId = seasonId,
						Type = FinanceTransactionType.Charge,
						Amount = safeAmount,
						Note = SeasonChargeNote
					},
					cancellationToken
				);
			}

			existingCharge.Amount = safeAmount;
			existingCharge.UpdatedAt = DateTime.UtcNow;

			return existingCharge;
		}

		public Task<FinanceTransaction> AddPaymentAsync(
			Guid playerId,
			Guid? seasonId,
			decimal amount,
			string? note,
			CancellationToken cancellationToken = default
		)
		{
			return AddTransactionAsync(
				new FinanceTransaction
				{
					PlayerId = playerId,
					SeasonId = seasonId,
					Type = FinanceTransactionType.Payment,
					Amount = decimal.Round(Math.Max(0, amount), 2),
					Note = note ?? string.Empty
				},
				cancellationToken
			);
		}

		public Task<FinanceTransaction> AddAdjustmentAsync(
			Guid playerId,
			Guid? seasonId,
			decimal amount,
			string? note,
			CancellationToken cancellationToken = default
		)
		{
			return AddTransactionAsync(
				new FinanceTransaction
				{
					PlayerId = playerId,
					SeasonId = seasonId,
					Type = FinanceTransactionType.Adjustment,
					Amount = decimal.Round(amount, 2),
					Note = note ?? string.Empty
				},
				cancellationToken
			);
		}
	}

	private class FakePlayerService : IPlayerService
	{
		public List<Player> Players { get; } = [];

		public Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<Player>>(Players);
		}

		public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var player = Players.FirstOrDefault(player => player.Id == id);
			return Task.FromResult(player);
		}

		public Task<Player> CreateAsync(Player player, CancellationToken cancellationToken = default)
		{
			player.Id = player.Id == Guid.Empty ? Guid.NewGuid() : player.Id;
			Players.Add(player);
			return Task.FromResult(player);
		}

		public Task<Player?> UpdateAsync(Player player, CancellationToken cancellationToken = default)
		{
			var existingPlayerIndex = Players.FindIndex(existingPlayer => existingPlayer.Id == player.Id);
			if (existingPlayerIndex == -1)
			{
				return Task.FromResult<Player?>(null);
			}

			Players[existingPlayerIndex] = player;
			return Task.FromResult<Player?>(player);
		}

		public Task<Player?> SetActiveAsync(
			Guid id,
			bool isActive,
			CancellationToken cancellationToken = default
		)
		{
			var player = Players.FirstOrDefault(player => player.Id == id);
			if (player is null)
			{
				return Task.FromResult<Player?>(null);
			}

			player.IsActive = isActive;
			return Task.FromResult<Player?>(player);
		}
	}
}
