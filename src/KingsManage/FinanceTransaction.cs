namespace KingsManage;

public class FinanceTransaction
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid PlayerId { get; set; }

	public Guid? SeasonId { get; set; }

	public FinanceTransactionType Type { get; set; }

	public decimal Amount { get; set; }

	public string Note { get; set; } = string.Empty;

	public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
