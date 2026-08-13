using KingsManage;
using KingsManage.Web.Services;

namespace KingsManage.Tests.Unit.Services;

public class FinanceReportQueryServiceTests
{
	[Test]
	public async Task GetAsync_UsesActivePlayersAndBuildsActionableBreakdown()
	{
		var seasonId = Guid.NewGuid();
		var unpaidId = Guid.NewGuid();
		var partPaidId = Guid.NewGuid();
		var paidId = Guid.NewGuid();
		var noChargeId = Guid.NewGuid();
		var inactiveId = Guid.NewGuid();
		var now = DateTime.UtcNow;
		var players = new[]
		{
			Player(unpaidId), Player(partPaidId), Player(paidId), Player(noChargeId),
			Player(inactiveId, isActive: false)
		};
		var transactions = new[]
		{
			Transaction(unpaidId, seasonId, FinanceTransactionType.Charge, 100, now),
			Transaction(partPaidId, seasonId, FinanceTransactionType.Charge, 100, now),
			Transaction(partPaidId, seasonId, FinanceTransactionType.Payment, 40, now),
			Transaction(paidId, seasonId, FinanceTransactionType.Charge, 100, now),
			Transaction(paidId, seasonId, FinanceTransactionType.Payment, 100, now),
			Transaction(inactiveId, seasonId, FinanceTransactionType.Charge, 500, now),
			Transaction(inactiveId, seasonId, FinanceTransactionType.Payment, 500, now)
		};
		var service = new FinanceReportQueryService(
			new StubFinanceService(transactions),
			new StubPlayerService(players),
			new StubSeasonService(new Season { Id = seasonId, StartDate = now.AddDays(-50), EndDate = now.AddDays(50) }));

		var report = await service.GetAsync(seasonId);

		Assert.That(report, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(report!.Expected, Is.EqualTo(300));
			Assert.That(report.Collected, Is.EqualTo(140));
			Assert.That(report.Outstanding, Is.EqualTo(160));
			Assert.That(report.PlayersOwing, Is.EqualTo(2));
			Assert.That(report.OutstandingBreakdown.Unpaid.PlayerCount, Is.EqualTo(1));
			Assert.That(report.OutstandingBreakdown.Unpaid.Outstanding, Is.EqualTo(100));
			Assert.That(report.OutstandingBreakdown.PartPaid.PlayerCount, Is.EqualTo(1));
			Assert.That(report.OutstandingBreakdown.PartPaid.Outstanding, Is.EqualTo(60));
			Assert.That(report.OutstandingBreakdown.Paid.PlayerCount, Is.EqualTo(1));
			Assert.That(report.OutstandingBreakdown.NoCharge.PlayerCount, Is.EqualTo(1));
			Assert.That(report.Months.Sum(month => month.Collected), Is.EqualTo(140));
			Assert.That(report.ForecastStatus, Is.EqualTo("Ahead of pace"));
		});
	}

	private static Player Player(Guid id, bool isActive = true) => new() { Id = id, Name = id.ToString(), IsActive = isActive };

	private static FinanceTransaction Transaction(Guid playerId, Guid seasonId, FinanceTransactionType type, decimal amount, DateTime date) =>
		new() { PlayerId = playerId, SeasonId = seasonId, Type = type, Amount = amount, TransactionDate = date };

	private sealed class StubFinanceService(IReadOnlyList<FinanceTransaction> transactions) : IFinanceService
	{
		public Task<IReadOnlyList<FinanceTransaction>> GetSeasonTransactionsAsync(Guid? seasonId, CancellationToken cancellationToken = default) => Task.FromResult(transactions);
		public Task<IReadOnlyList<FinanceTransaction>> GetPlayerTransactionsAsync(Guid playerId, Guid? seasonId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<FinanceTransaction> AddTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<bool> DeleteTransactionAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<FinanceTransaction> SetPlayerAmountOwedAsync(Guid playerId, Guid? seasonId, decimal amount, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<FinanceTransaction> AddPaymentAsync(Guid playerId, Guid? seasonId, decimal amount, string? note, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<FinanceTransaction> AddAdjustmentAsync(Guid playerId, Guid? seasonId, decimal amount, string? note, CancellationToken cancellationToken = default) => throw new NotSupportedException();
	}

	private sealed class StubPlayerService(IReadOnlyList<Player> players) : IPlayerService
	{
		public Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(players);
		public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<Player> CreateAsync(Player player, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<Player?> UpdateAsync(Player player, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<Player?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
	}

	private sealed class StubSeasonService(Season season) : ISeasonService
	{
		public Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<Season?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Season?>(id == season.Id ? season : null);
		public Task<Season?> GetActiveAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<Season> CreateAsync(Season season, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<Season?> UpdateAsync(Season season, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<Season?> SetActiveAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
	}
}
