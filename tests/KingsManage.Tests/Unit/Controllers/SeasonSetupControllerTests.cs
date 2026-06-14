using KingsManage;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public class SeasonSetupControllerTests
{
	[Test]
	public async Task SetupSeason_WhenStartingFinanceIsEnabled_ShouldCreateChargesForActivePlayersOnly()
	{
		var seasonService = new FakeSeasonService();
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new SeasonsController(seasonService);
		var activePlayerId = Guid.NewGuid();
		var inactivePlayerId = Guid.NewGuid();

		playerService.Players.Add(new Player
		{
			Id = activePlayerId,
			Name = "Active Player",
			Number = 1,
			Positions = ["GK"],
			IsActive = true
		});
		playerService.Players.Add(new Player
		{
			Id = inactivePlayerId,
			Name = "Inactive Player",
			Number = 2,
			Positions = ["CB"],
			IsActive = false
		});

		var result = await controller.SetupSeason(
			new SeasonSetupRequest
			{
				Name = "2026-2027",
				StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
				EndDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
				MakeActive = true,
				SetStartingFinanceAmount = true,
				StartingFinanceAmount = 50m
			},
			playerService,
			financeService,
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var setupResult = okResult?.Value as SeasonSetupResult;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(setupResult, Is.Not.Null);
		Assert.That(setupResult!.CreatedSeason, Is.True);
		Assert.That(setupResult.FinanceChargesCreatedOrUpdated, Is.EqualTo(1));
		Assert.That(financeService.Transactions, Has.Count.EqualTo(1));
		Assert.That(financeService.Transactions[0].PlayerId, Is.EqualTo(activePlayerId));
		Assert.That(financeService.Transactions[0].SeasonId, Is.EqualTo(setupResult.Season.Id));
		Assert.That(financeService.Transactions[0].Type, Is.EqualTo(FinanceTransactionType.Charge));
		Assert.That(financeService.Transactions[0].Amount, Is.EqualTo(50m));
		Assert.That(financeService.Transactions.Any(transaction => transaction.PlayerId == inactivePlayerId), Is.False);
	}

	[Test]
	public async Task SetupSeason_WhenStartingFinanceIsDisabled_ShouldNotCreateFinanceCharges()
	{
		var seasonService = new FakeSeasonService();
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new SeasonsController(seasonService);

		playerService.Players.Add(new Player
		{
			Id = Guid.NewGuid(),
			Name = "Active Player",
			Number = 5,
			Positions = ["CM"],
			IsActive = true
		});

		var result = await controller.SetupSeason(
			new SeasonSetupRequest
			{
				Name = "2026-2027",
				StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
				EndDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
				MakeActive = true,
				SetStartingFinanceAmount = false,
				StartingFinanceAmount = 0m
			},
			playerService,
			financeService,
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var setupResult = okResult?.Value as SeasonSetupResult;

		Assert.That(okResult, Is.Not.Null);
		Assert.That(setupResult, Is.Not.Null);
		Assert.That(setupResult!.FinanceChargesCreatedOrUpdated, Is.EqualTo(0));
		Assert.That(financeService.Transactions, Is.Empty);
	}

	[Test]
	public async Task SetupSeason_WhenStartingFinanceAmountIsNegative_ShouldReturnBadRequest()
	{
		var seasonService = new FakeSeasonService();
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new SeasonsController(seasonService);

		var result = await controller.SetupSeason(
			new SeasonSetupRequest
			{
				Name = "2026-2027",
				StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
				EndDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
				MakeActive = true,
				SetStartingFinanceAmount = true,
				StartingFinanceAmount = -10m
			},
			playerService,
			financeService,
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
		Assert.That(financeService.Transactions, Is.Empty);
	}

	[Test]
	public async Task SetupSeason_WhenSeasonAlreadyExists_ShouldUpdateExistingSeasonChargeInsteadOfDuplicating()
	{
		var seasonService = new FakeSeasonService();
		var playerService = new FakePlayerService();
		var financeService = new FakeFinanceService();
		var controller = new SeasonsController(seasonService);
		var seasonId = Guid.NewGuid();
		var playerId = Guid.NewGuid();

		seasonService.Seasons.Add(new Season
		{
			Id = seasonId,
			Name = "2026-2027",
			StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
			EndDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
			IsActive = true
		});
		playerService.Players.Add(new Player
		{
			Id = playerId,
			Name = "Active Player",
			Number = 6,
			Positions = ["CM"],
			IsActive = true
		});

		await financeService.SetPlayerAmountOwedAsync(playerId, seasonId, 35m);

		var result = await controller.SetupSeason(
			new SeasonSetupRequest
			{
				Name = "2026-2027",
				StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
				EndDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
				MakeActive = true,
				SetStartingFinanceAmount = true,
				StartingFinanceAmount = 60m
			},
			playerService,
			financeService,
			CancellationToken.None
		);

		var okResult = result.Result as OkObjectResult;
		var setupResult = okResult?.Value as SeasonSetupResult;
		var chargeTransactions = financeService.Transactions
			.Where(transaction => transaction.PlayerId == playerId && transaction.SeasonId == seasonId)
			.Where(transaction => transaction.Type == FinanceTransactionType.Charge)
			.ToList();

		Assert.That(okResult, Is.Not.Null);
		Assert.That(setupResult, Is.Not.Null);
		Assert.That(setupResult!.CreatedSeason, Is.False);
		Assert.That(setupResult.FinanceChargesCreatedOrUpdated, Is.EqualTo(1));
		Assert.That(chargeTransactions, Has.Count.EqualTo(1));
		Assert.That(chargeTransactions[0].Amount, Is.EqualTo(60m));
	}

	private class FakeSeasonService : ISeasonService
	{
		public List<Season> Seasons { get; } = [];

		public Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<Season>>(Seasons);
		}

		public Task<Season?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var season = Seasons.FirstOrDefault(season => season.Id == id);
			return Task.FromResult(season);
		}

		public Task<Season?> GetActiveAsync(CancellationToken cancellationToken = default)
		{
			var season = Seasons.FirstOrDefault(season => season.IsActive);
			return Task.FromResult(season);
		}

		public Task<Season> CreateAsync(Season season, CancellationToken cancellationToken = default)
		{
			season.Id = season.Id == Guid.Empty ? Guid.NewGuid() : season.Id;
			season.CreatedAt = DateTime.UtcNow;
			season.UpdatedAt = DateTime.UtcNow;

			if (season.IsActive)
			{
				foreach (var existingSeason in Seasons)
				{
					existingSeason.IsActive = false;
				}
			}

			Seasons.Add(season);
			return Task.FromResult(season);
		}

		public Task<Season?> UpdateAsync(Season season, CancellationToken cancellationToken = default)
		{
			var existingSeasonIndex = Seasons.FindIndex(existingSeason => existingSeason.Id == season.Id);
			if (existingSeasonIndex == -1)
			{
				return Task.FromResult<Season?>(null);
			}

			Seasons[existingSeasonIndex] = season;
			return Task.FromResult<Season?>(season);
		}

		public Task<Season?> SetActiveAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var season = Seasons.FirstOrDefault(season => season.Id == id);
			if (season is null)
			{
				return Task.FromResult<Season?>(null);
			}

			foreach (var existingSeason in Seasons)
			{
				existingSeason.IsActive = false;
			}

			season.IsActive = true;
			season.UpdatedAt = DateTime.UtcNow;

			return Task.FromResult<Season?>(season);
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
}
