using KingsManage;

namespace KingsManage.Web.Models;

public class FinanceTransactionViewModel
{
	public Guid Id { get; set; }

	public Guid PlayerId { get; set; }

	public Guid? SeasonId { get; set; }

	public FinanceTransactionType Type { get; set; }

	public decimal Amount { get; set; }

	public string Note { get; set; } = string.Empty;

	public DateTime TransactionDate { get; set; }

	public static FinanceTransactionViewModel FromTransaction(
		FinanceTransaction transaction
	)
	{
		return new FinanceTransactionViewModel
		{
			Id = transaction.Id,
			PlayerId = transaction.PlayerId,
			SeasonId = transaction.SeasonId,
			Type = transaction.Type,
			Amount = transaction.Amount,
			Note = transaction.Note,
			TransactionDate = transaction.TransactionDate
		};
	}
}
