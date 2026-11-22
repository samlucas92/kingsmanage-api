namespace KingsManage.Web.Models;

public sealed class GoogleSheetsFinanceSyncRequest
{
	public Guid SeasonId { get; set; }
}

public sealed class GoogleSheetsFinanceImportRequest
{
	public Guid SeasonId { get; set; }
	public IReadOnlyList<string> TransactionIds { get; set; } = [];
}

public sealed class GoogleSheetsFinanceImportPreviewViewModel
{
	public string SheetName { get; set; } = string.Empty;
	public IReadOnlyList<GoogleSheetsFinanceImportItemViewModel> Items { get; set; } = [];
	public IReadOnlyList<string> UnmatchedPlayers { get; set; } = [];
}

public sealed class GoogleSheetsFinanceImportItemViewModel
{
	public string Id { get; set; } = string.Empty;
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public int PlayerNumber { get; set; }
	public string Label { get; set; } = string.Empty;
	public decimal SheetAmount { get; set; }
	public decimal RecordedAmount { get; set; }
	public decimal AmountToAdd { get; set; }
	public bool AlreadyRecorded => AmountToAdd <= 0;
}

public sealed class GoogleSheetsFinanceResetViewModel
{
	public long DeletedPayments { get; set; }
	public string Message { get; set; } = string.Empty;
}

public sealed class GoogleSheetsFinanceImportResultViewModel
{
	public string SheetName { get; set; } = string.Empty;
	public int TransactionsCreated { get; set; }
	public int PlayersUpdated { get; set; }
	public decimal AmountCreated { get; set; }
	public string Message { get; set; } = string.Empty;
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
