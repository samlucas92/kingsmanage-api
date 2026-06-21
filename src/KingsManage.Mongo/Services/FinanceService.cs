using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class FinanceService : IFinanceService
{
	private const string SeasonChargeNote = "Season amount owed";

	private readonly IMongoCollection<FinanceTransaction> _transactions;
	private readonly TenantMongoScope _tenant;

	public FinanceService(MongoContext context, TenantMongoScope tenant)
	{
		_transactions = context.Database.GetCollection<FinanceTransaction>("financeTransactions");
		_tenant = tenant;
	}

	public async Task<IReadOnlyList<FinanceTransaction>> GetSeasonTransactionsAsync(
		Guid? seasonId,
		CancellationToken cancellationToken = default
	)
	{
		return await _transactions
			.Find(_tenant.Filter<FinanceTransaction>(transaction => transaction.SeasonId == seasonId))
			.SortByDescending(transaction => transaction.TransactionDate)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<FinanceTransaction>> GetPlayerTransactionsAsync(
		Guid playerId,
		Guid? seasonId = null,
		CancellationToken cancellationToken = default
	)
	{
		var filter = Builders<FinanceTransaction>.Filter.Eq(
			transaction => transaction.PlayerId,
			playerId
		);

		if (seasonId.HasValue)
		{
			filter &= Builders<FinanceTransaction>.Filter.Eq(
				transaction => transaction.SeasonId,
				seasonId.Value
			);
		}

		return await _transactions
			.Find(_tenant.Filter<FinanceTransaction>() & filter)
			.SortByDescending(transaction => transaction.TransactionDate)
			.ToListAsync(cancellationToken);
	}

	public async Task<FinanceTransaction> AddTransactionAsync(
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
		_tenant.Assign(transaction);

		await _transactions.InsertOneAsync(
			transaction,
			cancellationToken: cancellationToken
		);

		return transaction;
	}

	public async Task<bool> DeleteTransactionAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var result = await _transactions.DeleteOneAsync(
			_tenant.Filter<FinanceTransaction>(transaction => transaction.Id == id),
			cancellationToken
		);

		return result.DeletedCount > 0;
	}

	public async Task<FinanceTransaction> SetPlayerAmountOwedAsync(
		Guid playerId,
		Guid? seasonId,
		decimal amount,
		CancellationToken cancellationToken = default
	)
	{
		var safeAmount = decimal.Round(Math.Max(0, amount), 2);
		var existingCharge = await _transactions
			.Find(_tenant.Filter<FinanceTransaction>(transaction =>
				transaction.PlayerId == playerId &&
				transaction.SeasonId == seasonId &&
				transaction.Type == FinanceTransactionType.Charge &&
				transaction.Note == SeasonChargeNote
			))
			.FirstOrDefaultAsync(cancellationToken);

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

		await _transactions.ReplaceOneAsync(
			_tenant.Filter<FinanceTransaction>(transaction => transaction.Id == existingCharge.Id),
			existingCharge,
			cancellationToken: cancellationToken
		);

		return existingCharge;
	}

	public async Task<FinanceTransaction> AddPaymentAsync(
		Guid playerId,
		Guid? seasonId,
		decimal amount,
		string? note,
		CancellationToken cancellationToken = default
	)
	{
		return await AddTransactionAsync(
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

	public async Task<FinanceTransaction> AddAdjustmentAsync(
		Guid playerId,
		Guid? seasonId,
		decimal amount,
		string? note,
		CancellationToken cancellationToken = default
	)
	{
		return await AddTransactionAsync(
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
