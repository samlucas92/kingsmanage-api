using KingsManage;

namespace KingsManage.Web.Models;

public class PlayerFinanceViewModel
{
	public Guid PlayerId { get; set; }

	public string PlayerName { get; set; } = string.Empty;

	public int PlayerNumber { get; set; }

	public bool IsActive { get; set; }

	public Guid? SeasonId { get; set; }

	public decimal TotalCharged { get; set; }

	public decimal TotalAdjustments { get; set; }

	public decimal AmountOwed { get; set; }

	public decimal TotalPaid { get; set; }

	public decimal Balance { get; set; }

	public IReadOnlyList<FinanceTransactionViewModel> Transactions { get; set; } = [];

	public static PlayerFinanceViewModel FromPlayer(
		Player player,
		Guid? seasonId,
		IReadOnlyList<FinanceTransaction> transactions
	)
	{
		var playerTransactions = transactions
			.Where(transaction => transaction.PlayerId == player.Id)
			.OrderByDescending(transaction => transaction.TransactionDate)
			.ToList();

		var totalCharged = playerTransactions
			.Where(transaction => transaction.Type == FinanceTransactionType.Charge)
			.Sum(transaction => transaction.Amount);
		var totalAdjustments = playerTransactions
			.Where(transaction => transaction.Type == FinanceTransactionType.Adjustment)
			.Sum(transaction => transaction.Amount);
		var totalPaid = playerTransactions
			.Where(transaction => transaction.Type == FinanceTransactionType.Payment)
			.Sum(transaction => transaction.Amount);
		var amountOwed = Math.Max(0, totalCharged + totalAdjustments);
		var balance = Math.Max(0, amountOwed - totalPaid);

		return new PlayerFinanceViewModel
		{
			PlayerId = player.Id,
			PlayerName = player.Name,
			PlayerNumber = player.Number,
			IsActive = player.IsActive,
			SeasonId = seasonId,
			TotalCharged = totalCharged,
			TotalAdjustments = totalAdjustments,
			AmountOwed = amountOwed,
			TotalPaid = totalPaid,
			Balance = balance,
			Transactions = playerTransactions
				.Select(FinanceTransactionViewModel.FromTransaction)
				.ToList()
		};
	}
}
