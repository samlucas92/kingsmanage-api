using KingsManage;

namespace KingsManage.Web.Models;

public class FinanceTransactionRequest
{
	public Guid PlayerId { get; set; }

	public Guid? SeasonId { get; set; }

	public FinanceTransactionType Type { get; set; }

	public decimal Amount { get; set; }

	public string? Note { get; set; }
}
