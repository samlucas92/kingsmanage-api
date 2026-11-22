using KingsManage;
using KingsManage.Web.Services;

namespace KingsManage.Tests.Unit.GoogleSheets;

[TestFixture]
public sealed class GoogleSheetsFinanceMapperTests
{
	private static readonly Season Season = new()
	{
		Id = Guid.NewGuid(),
		Name = "2026-2027",
		StartDate = new DateTime(2026, 8, 1),
		EndDate = new DateTime(2027, 7, 31)
	};

	[Test]
	public void ResolveMonthlySubsSheet_MatchesTheSelectedSeason()
	{
		var sheets = new List<GoogleSheetTab>
		{
			new() { Title = "25/26 Monthly Subs" },
			new() { Title = "26/27 Monthly Subs" },
			new() { Title = "26/27 1st Season Stats" }
		};

		var result = GoogleSheetsFinanceMapper.ResolveMonthlySubsSheet(sheets, Season);

		Assert.That(result, Is.EqualTo("26/27 Monthly Subs"));
	}

	[Test]
	public void ImportPreview_RecognisesSignOnFeesAndCalculatesOnlyMissingAmounts()
	{
		var layout = Layout();
		var existing = new List<FinanceTransaction>
		{
			Payment(30, new DateTime(2026, 8, 1), "Sign on fee"),
			Payment(50, new DateTime(2026, 9, 10), "Bank transfer")
		};

		var candidates = GoogleSheetsFinanceMapper.BuildImportCandidates(layout, existing);

		Assert.Multiple(() =>
		{
			Assert.That(candidates.Select(item => item.AmountToAdd), Is.EqualTo(new[] { 0m, 60m, 20m }));
			Assert.That(candidates[0].RecordedAmount, Is.EqualTo(30m));
			Assert.That(candidates[0].Label, Is.EqualTo("Registration Fee"));
			Assert.That(candidates[1].TransactionDate, Is.EqualTo(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)));
		});
	}

	[Test]
	public void Export_OverwritesOnlyTheFinanceCellSpanForThePlayerRow()
	{
		var layout = Layout();
		var transactions = new List<FinanceTransaction>
		{
			Payment(30, new DateTime(2026, 8, 1), "Sign-on fee"),
			Payment(40, new DateTime(2026, 9, 10), "First instalment"),
			Payment(70, new DateTime(2026, 9, 20), "Second instalment")
		};

		var result = GoogleSheetsFinanceMapper.BuildExport(layout, transactions);

		Assert.Multiple(() =>
		{
			Assert.That(result.StartColumn, Is.EqualTo(3));
			Assert.That(result.Values, Is.EqualTo(new object?[] { 30m, string.Empty, 110m, string.Empty }));
			Assert.That(result.Total, Is.EqualTo(140m));
			Assert.That(result.ChangedCells, Is.EqualTo(1));
		});
	}

	[Test]
	public void FindPlayer_UsesTheSheetHeadersAndPlayerName()
	{
		IReadOnlyList<IReadOnlyList<string>> values = new List<IReadOnlyList<string>>
		{
			new[] { "26/27 Season" },
			Array.Empty<string>(),
			Array.Empty<string>(),
			new[] { "No.", "Player", "Registration Fee", "Aug 2026", "\u200eSep\u00a02026", "Oct 2026" },
			new[] { "12", "Alex Wilson", "£20.00", "", "£110.00", "" }
		};
		var player = new Player { Id = Guid.NewGuid(), Name = "Alex Wilson", Number = 12 };

		var result = GoogleSheetsFinanceMapper.FindPlayer(values, player, Season);

		Assert.Multiple(() =>
		{
			Assert.That(result.RowNumber, Is.EqualTo(5));
			Assert.That(result.StartColumn, Is.EqualTo(3));
			Assert.That(result.Cells.Select(item => item.Amount), Is.EqualTo(new[] { 20m, 0m, 110m, 0m }));
		});
	}

	private static GoogleSheetsPlayerFinanceLayout Layout() => new(
		5,
		3,
		new[] { "12", "Alex Wilson", "£30.00", "", "£110.00", "£20.00" },
		new List<GoogleSheetsFinanceCell>
		{
			new(3, "Registration Fee", new DateTime(2026, 8, 1), true, 30),
			new(4, "Aug 2026", new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), false, 0),
			new(5, "Sep 2026", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), false, 110),
			new(6, "Oct 2026", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), false, 20)
		});

	private static FinanceTransaction Payment(decimal amount, DateTime date, string note) => new()
	{
		Type = FinanceTransactionType.Payment,
		Amount = amount,
		TransactionDate = date,
		Note = note
	};
}
