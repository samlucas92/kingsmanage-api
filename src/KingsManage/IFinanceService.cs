namespace KingsManage;

public interface IFinanceService
{
	Task<IReadOnlyList<FinanceTransaction>> GetSeasonTransactionsAsync(
		Guid? seasonId,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<FinanceTransaction>> GetPlayerTransactionsAsync(
		Guid playerId,
		Guid? seasonId = null,
		CancellationToken cancellationToken = default
	);

	Task<FinanceTransaction> AddTransactionAsync(
		FinanceTransaction transaction,
		CancellationToken cancellationToken = default
	);

	Task<bool> DeleteTransactionAsync(
		Guid id,
		CancellationToken cancellationToken = default
	);

	Task<FinanceTransaction> SetPlayerAmountOwedAsync(
		Guid playerId,
		Guid? seasonId,
		decimal amount,
		CancellationToken cancellationToken = default
	);

	Task<FinanceTransaction> AddPaymentAsync(
		Guid playerId,
		Guid? seasonId,
		decimal amount,
		string? note,
		CancellationToken cancellationToken = default
	);

	Task<FinanceTransaction> AddAdjustmentAsync(
		Guid playerId,
		Guid? seasonId,
		decimal amount,
		string? note,
		CancellationToken cancellationToken = default
	);
}
