using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KingsManage;
using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface IGoogleSheetsFinanceSyncService
{
	Task<GoogleSheetsFinanceSyncViewModel> ImportPlayerAsync(Guid playerId, Guid seasonId, CancellationToken cancellationToken = default);
	Task<GoogleSheetsFinanceSyncViewModel> ExportPlayerAsync(Guid playerId, Guid seasonId, CancellationToken cancellationToken = default);
}

public sealed class GoogleSheetsFinanceSyncService : IGoogleSheetsFinanceSyncService
{
	private readonly IOrganizationGoogleSheetsIntegrationService integrations;
	private readonly IGoogleSheetsIntegrationClient google;
	private readonly IIntegrationSecretProtector protector;
	private readonly IPlayerService players;
	private readonly ISeasonService seasons;
	private readonly IFinanceService finance;

	public GoogleSheetsFinanceSyncService(
		IOrganizationGoogleSheetsIntegrationService integrations,
		IGoogleSheetsIntegrationClient google,
		IIntegrationSecretProtector protector,
		IPlayerService players,
		ISeasonService seasons,
		IFinanceService finance)
	{
		this.integrations = integrations;
		this.google = google;
		this.protector = protector;
		this.players = players;
		this.seasons = seasons;
		this.finance = finance;
	}

	public async Task<GoogleSheetsFinanceSyncViewModel> ImportPlayerAsync(Guid playerId, Guid seasonId, CancellationToken cancellationToken = default)
	{
		var context = await LoadContextAsync(playerId, seasonId, cancellationToken);
		var transactions = await finance.GetPlayerTransactionsAsync(playerId, seasonId, cancellationToken);
		var additions = GoogleSheetsFinanceMapper.BuildMissingPayments(context.Layout, transactions, context.SheetName);

		foreach (var addition in additions)
		{
			await finance.AddTransactionAsync(new FinanceTransaction
			{
				PlayerId = playerId,
				SeasonId = seasonId,
				Type = FinanceTransactionType.Payment,
				Amount = addition.Amount,
				Note = addition.Note,
				TransactionDate = addition.TransactionDate
			}, cancellationToken);
		}

		var amount = additions.Sum(item => item.Amount);
		return Result("import", context, additions.Count, amount,
			additions.Count == 0
				? $"{context.Player.Name} already has all payments recorded from {context.SheetName}."
				: $"Added {additions.Count} missing payment{(additions.Count == 1 ? string.Empty : "s")} totalling £{amount:0.00} for {context.Player.Name}.");
	}

	public async Task<GoogleSheetsFinanceSyncViewModel> ExportPlayerAsync(Guid playerId, Guid seasonId, CancellationToken cancellationToken = default)
	{
		var context = await LoadContextAsync(playerId, seasonId, cancellationToken);
		var transactions = await finance.GetPlayerTransactionsAsync(playerId, seasonId, cancellationToken);
		var export = GoogleSheetsFinanceMapper.BuildExport(context.Layout, transactions);
		await google.UpdateValuesAsync(
			context.AccessToken,
			context.Integration.SpreadsheetId!,
			context.SheetName,
			context.Layout.RowNumber,
			export.StartColumn,
			export.Values,
			cancellationToken);

		return Result("export", context, export.ChangedCells, export.Total,
			$"Updated {context.Player.Name}'s finance row in {context.SheetName}. No other player rows were changed.");
	}

	private async Task<SyncContext> LoadContextAsync(Guid playerId, Guid seasonId, CancellationToken cancellationToken)
	{
		var player = await players.GetByIdAsync(playerId, cancellationToken)
			?? throw new InvalidOperationException("Player was not found.");
		var season = await seasons.GetByIdAsync(seasonId, cancellationToken)
			?? throw new InvalidOperationException("Season was not found.");
		var integration = await integrations.GetCurrentAsync(cancellationToken);
		if (integration is null || string.IsNullOrWhiteSpace(integration.SpreadsheetId))
			throw new InvalidOperationException("Connect and choose a Google Sheet before syncing player finance.");

		var sheetName = GoogleSheetsFinanceMapper.ResolveMonthlySubsSheet(integration.Sheets, season);
		var token = await google.RefreshAccessTokenAsync(protector.Unprotect(integration.EncryptedRefreshToken), cancellationToken);
		var values = await google.GetValuesAsync(token.Value, integration.SpreadsheetId, sheetName, cancellationToken);
		var layout = GoogleSheetsFinanceMapper.FindPlayer(values, player, season);
		return new SyncContext(player, season, integration, sheetName, token.Value, layout);
	}

	private static GoogleSheetsFinanceSyncViewModel Result(
		string direction,
		SyncContext context,
		int cellsChanged,
		decimal amountChanged,
		string message) => new()
	{
		Direction = direction,
		PlayerId = context.Player.Id,
		PlayerName = context.Player.Name,
		SheetName = context.SheetName,
		RowNumber = context.Layout.RowNumber,
		CellsChanged = cellsChanged,
		AmountChanged = amountChanged,
		Message = message
	};

	private sealed record SyncContext(
		Player Player,
		Season Season,
		OrganizationGoogleSheetsIntegration Integration,
		string SheetName,
		string AccessToken,
		GoogleSheetsPlayerFinanceLayout Layout);
}

public sealed record GoogleSheetsFinanceCell(int Column, string Label, DateTime TransactionDate, bool IsRegistration, decimal Amount);
public sealed record GoogleSheetsPlayerFinanceLayout(int RowNumber, int StartColumn, IReadOnlyList<string> RowValues, IReadOnlyList<GoogleSheetsFinanceCell> Cells);
public sealed record GoogleSheetsPaymentAddition(decimal Amount, string Note, DateTime TransactionDate);
public sealed record GoogleSheetsFinanceExport(int StartColumn, IReadOnlyList<object?> Values, int ChangedCells, decimal Total);

public static partial class GoogleSheetsFinanceMapper
{
	private static readonly CultureInfo BritishCulture = CultureInfo.GetCultureInfo("en-GB");

	public static string ResolveMonthlySubsSheet(IReadOnlyList<GoogleSheetTab> sheets, Season season)
	{
		var candidates = sheets.Where(item => Normalize(item.Title).Contains("monthly subs", StringComparison.Ordinal)).ToList();
		if (candidates.Count == 0) throw new InvalidOperationException("The connected workbook does not contain a Monthly Subs sheet tab.");
		if (candidates.Count == 1) return candidates[0].Title;

		var seasonTokens = new[]
		{
			$"{season.StartDate:yy}/{season.EndDate:yy}",
			$"{season.StartDate:yyyy}/{season.EndDate:yyyy}",
			$"{season.StartDate:yyyy}-{season.EndDate:yyyy}",
			season.Name
		}.Select(Normalize).Where(value => value.Length > 0).Distinct().ToList();
		var matches = candidates.Where(item => seasonTokens.Any(token => Normalize(item.Title).Contains(token, StringComparison.Ordinal))).ToList();
		if (matches.Count == 1) return matches[0].Title;
		throw new InvalidOperationException($"More than one Monthly Subs tab exists and none could be matched uniquely to {season.Name}.");
	}

	public static GoogleSheetsPlayerFinanceLayout FindPlayer(IReadOnlyList<IReadOnlyList<string>> values, Player player, Season season)
	{
		var headerIndex = -1;
		var playerColumn = -1;
		var numberColumn = -1;
		var registrationColumn = -1;
		var monthlyColumns = new List<(int Column, string Label, DateTime Date)>();

		for (var rowIndex = 0; rowIndex < values.Count; rowIndex++)
		{
			var row = values[rowIndex];
			var normalized = row.Select(Normalize).ToList();
			var possiblePlayerColumn = normalized.FindIndex(value => value == "player");
			var possibleRegistrationColumn = normalized.FindIndex(value => value.Contains("registration fee", StringComparison.Ordinal));
			if (possiblePlayerColumn < 0 || possibleRegistrationColumn < 0) continue;

			headerIndex = rowIndex;
			playerColumn = possiblePlayerColumn;
			registrationColumn = possibleRegistrationColumn;
			numberColumn = normalized.FindIndex(value => value is "no" or "number" or "squad number");
			for (var column = registrationColumn + 1; column < row.Count; column++)
			{
				if (TryParseMonth(row[column], out var month)) monthlyColumns.Add((column, row[column].Trim(), month));
			}
			break;
		}

		if (headerIndex < 0 || monthlyColumns.Count == 0)
			throw new InvalidOperationException("The Monthly Subs tab must contain Player, Registration Fee, and month headers such as Aug 2026.");

		var normalizedPlayerName = Normalize(player.Name);
		var matchingRows = new List<int>();
		for (var rowIndex = headerIndex + 1; rowIndex < values.Count; rowIndex++)
		{
			var row = values[rowIndex];
			var name = Cell(row, playerColumn);
			if (Normalize(name) == normalizedPlayerName) matchingRows.Add(rowIndex);
		}

		if (matchingRows.Count == 0 && numberColumn >= 0 && player.Number > 0)
		{
			for (var rowIndex = headerIndex + 1; rowIndex < values.Count; rowIndex++)
			{
				if (int.TryParse(Cell(values[rowIndex], numberColumn), out var number) && number == player.Number) matchingRows.Add(rowIndex);
			}
		}

		if (matchingRows.Count != 1)
			throw new InvalidOperationException(matchingRows.Count == 0
				? $"{player.Name} was not found in the Monthly Subs player rows."
				: $"More than one row matched {player.Name}; make the player name or squad number unique.");

		var playerRow = values[matchingRows[0]];
		var cells = new List<GoogleSheetsFinanceCell>
		{
			new(registrationColumn + 1, "Registration Fee", season.StartDate.Date, true, ParseCurrency(Cell(playerRow, registrationColumn)))
		};
		cells.AddRange(monthlyColumns.Select(item => new GoogleSheetsFinanceCell(
			item.Column + 1,
			item.Label,
			DateTime.SpecifyKind(new DateTime(item.Date.Year, item.Date.Month, 1), DateTimeKind.Utc),
			false,
			ParseCurrency(Cell(playerRow, item.Column)))));

		return new GoogleSheetsPlayerFinanceLayout(matchingRows[0] + 1, registrationColumn + 1, playerRow, cells);
	}

	public static IReadOnlyList<GoogleSheetsPaymentAddition> BuildMissingPayments(
		GoogleSheetsPlayerFinanceLayout layout,
		IReadOnlyList<FinanceTransaction> transactions,
		string sheetName)
	{
		var payments = transactions.Where(item => item.Type == FinanceTransactionType.Payment).ToList();
		var additions = new List<GoogleSheetsPaymentAddition>();
		foreach (var cell in layout.Cells.Where(item => item.Amount > 0))
		{
			var recorded = payments.Where(payment => cell.IsRegistration
				? IsRegistration(payment)
				: !IsRegistration(payment) && payment.TransactionDate.Year == cell.TransactionDate.Year && payment.TransactionDate.Month == cell.TransactionDate.Month)
				.Sum(payment => payment.Amount);
			var missing = decimal.Round(cell.Amount - recorded, 2);
			if (missing <= 0) continue;
			additions.Add(new GoogleSheetsPaymentAddition(
				missing,
				$"Imported from Google Sheets · {sheetName} · {cell.Label}",
				cell.TransactionDate));
		}
		return additions;
	}

	public static GoogleSheetsFinanceExport BuildExport(GoogleSheetsPlayerFinanceLayout layout, IReadOnlyList<FinanceTransaction> transactions)
	{
		var payments = transactions.Where(item => item.Type == FinanceTransactionType.Payment).ToList();
		var startColumn = layout.Cells.Min(item => item.Column);
		var endColumn = layout.Cells.Max(item => item.Column);
		var output = Enumerable.Range(startColumn, endColumn - startColumn + 1)
			.Select(column => (object?)Cell(layout.RowValues, column - 1))
			.ToList();
		var changed = 0;
		var total = 0m;

		foreach (var cell in layout.Cells)
		{
			var amount = payments.Where(payment => cell.IsRegistration
				? IsRegistration(payment)
				: !IsRegistration(payment) && payment.TransactionDate.Year == cell.TransactionDate.Year && payment.TransactionDate.Month == cell.TransactionDate.Month)
				.Sum(payment => payment.Amount);
			amount = decimal.Round(amount, 2);
			if (amount != cell.Amount) changed++;
			total += amount;
			output[cell.Column - startColumn] = amount > 0 ? amount : string.Empty;
		}

		return new GoogleSheetsFinanceExport(startColumn, output, changed, total);
	}

	private static bool IsRegistration(FinanceTransaction transaction) =>
		transaction.Note.Contains("registration", StringComparison.OrdinalIgnoreCase);

	private static bool TryParseMonth(string value, out DateTime month) =>
		DateTime.TryParseExact(value.Trim(), ["MMM yyyy", "MMMM yyyy"], BritishCulture, DateTimeStyles.AllowWhiteSpaces, out month);

	private static decimal ParseCurrency(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return 0;
		if (decimal.TryParse(value, NumberStyles.Currency, BritishCulture, out var amount)) return decimal.Round(amount, 2);
		var cleaned = CurrencyCleaner().Replace(value, string.Empty);
		return decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount)
			? decimal.Round(amount, 2)
			: 0;
	}

	private static string Cell(IReadOnlyList<string> row, int column) => column >= 0 && column < row.Count ? row[column].Trim() : string.Empty;

	private static string Normalize(string value)
	{
		var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
		var characters = decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark);
		return Whitespace().Replace(new string(characters.ToArray()).Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace('/', ' ').Replace('-', ' '), " ").Trim();
	}

	[GeneratedRegex(@"[^0-9.,()\-]+")]
	private static partial Regex CurrencyCleaner();

	[GeneratedRegex(@"[^a-z0-9]+")]
	private static partial Regex Whitespace();
}
