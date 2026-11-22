namespace KingsManage.Web.Models;

public sealed class GoogleSheetsFinanceSyncRequest
{
	public Guid SeasonId { get; set; }
}

public sealed class GoogleSheetsFinanceSyncViewModel
{
	public string Direction { get; set; } = string.Empty;
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public string SheetName { get; set; } = string.Empty;
	public int RowNumber { get; set; }
	public int CellsChanged { get; set; }
	public decimal AmountChanged { get; set; }
	public string Message { get; set; } = string.Empty;
}
