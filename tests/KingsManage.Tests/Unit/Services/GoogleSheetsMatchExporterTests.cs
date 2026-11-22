using System.Net;
using System.Text;
using System.Text.Json;
using KingsManage;
using KingsManage.Web.Models;
using KingsManage.Web.Services;

namespace KingsManage.Tests.Unit.Services;

[TestFixture]
public sealed class GoogleSheetsMatchExporterTests
{
	private static readonly Guid MatchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
	private static readonly Guid TeamId = Guid.Parse("11111111-1111-1111-1111-111111111101");
	private static readonly Guid StarterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid UnusedSubstituteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
	private static readonly Guid AbsentPlayerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

	[Test]
	public async Task ExportAsync_SendsCompletedMatchAndUsesBlankStatsForNonAppearances()
	{
		string? requestBody = null;
		var handler = new StubHandler(request =>
		{
			requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
			return Json("""{"ok":true,"action":"created","sheetName":"26/27 1st Season Stats","matchColumn":19,"matchedPlayers":3,"unmatchedPlayers":[]}""");
		});
		var exporter = CreateExporter(handler);

		var result = await exporter.ExportAsync(
			CreateCompletedMatch(),
			CreatePlayers(),
			TeamId,
			"1st Team");

		Assert.Multiple(() =>
		{
			Assert.That(result.Action, Is.EqualTo("created"));
			Assert.That(result.SheetName, Is.EqualTo("26/27 1st Season Stats"));
			Assert.That(result.MatchColumn, Is.EqualTo(19));
		});

		using var document = JsonDocument.Parse(requestBody!);
		var root = document.RootElement;
		var players = root.GetProperty("players").EnumerateArray().ToDictionary(
			player => player.GetProperty("playerId").GetString()!);

		Assert.Multiple(() =>
		{
			Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
			Assert.That(root.GetProperty("secret").GetString(), Is.EqualTo("test-secret"));
			Assert.That(root.GetProperty("matchId").GetString(), Is.EqualTo(MatchId.ToString()));
			Assert.That(root.GetProperty("teamId").GetString(), Is.EqualTo(TeamId.ToString()));
			Assert.That(root.GetProperty("homeGoals").GetInt32(), Is.EqualTo(3));
			Assert.That(root.GetProperty("awayGoals").GetInt32(), Is.EqualTo(1));
			Assert.That(players[StarterId.ToString()].GetProperty("appearance").GetInt32(), Is.EqualTo(1));
			Assert.That(players[StarterId.ToString()].GetProperty("goals").GetInt32(), Is.EqualTo(2));
			Assert.That(players[UnusedSubstituteId.ToString()].GetProperty("appearance").ValueKind, Is.EqualTo(JsonValueKind.Null));
			Assert.That(players[UnusedSubstituteId.ToString()].GetProperty("goals").ValueKind, Is.EqualTo(JsonValueKind.Null));
			Assert.That(players[AbsentPlayerId.ToString()].GetProperty("appearance").ValueKind, Is.EqualTo(JsonValueKind.Null));
		});
	}

	[Test]
	public void ExportAsync_WhenWebhookRejectsPayload_SurfacesItsSafeError()
	{
		var handler = new StubHandler(_ => Json("""{"ok":false,"error":"Player Fred was not found."}"""));
		var exporter = CreateExporter(handler);

		var exception = Assert.ThrowsAsync<GoogleSheetsExportException>(() => exporter.ExportAsync(
			CreateCompletedMatch(),
			CreatePlayers(),
			TeamId,
			"1st Team"));

		Assert.That(exception!.Message, Is.EqualTo("Player Fred was not found."));
	}

	[Test]
	public void ExportAsync_WhenMatchIsIncomplete_DoesNotCallWebhook()
	{
		var calls = 0;
		var handler = new StubHandler(_ =>
		{
			calls += 1;
			return Json("""{"ok":true}""");
		});
		var exporter = CreateExporter(handler);
		var match = CreateCompletedMatch();
		match.IsCompleted = false;

		var exception = Assert.ThrowsAsync<GoogleSheetsExportException>(() => exporter.ExportAsync(
			match,
			CreatePlayers(),
			TeamId,
			"1st Team"));

		Assert.Multiple(() =>
		{
			Assert.That(exception!.Message, Does.Contain("completed matches"));
			Assert.That(calls, Is.Zero);
		});
	}

	[Test]
	public void ExportAsync_WhenPlayerStatsHaveNotBeenSaved_DoesNotCallWebhook()
	{
		var calls = 0;
		var handler = new StubHandler(_ =>
		{
			calls += 1;
			return Json("""{"ok":true}""");
		});
		var exporter = CreateExporter(handler);
		var match = CreateCompletedMatch();
		match.PlayerStats = [];

		var exception = Assert.ThrowsAsync<GoogleSheetsExportException>(() => exporter.ExportAsync(
			match,
			CreatePlayers(),
			TeamId,
			"1st Team"));

		Assert.Multiple(() =>
		{
			Assert.That(exception!.Message, Does.Contain("Save the match events"));
			Assert.That(calls, Is.Zero);
		});
	}

	private static GoogleSheetsMatchExporter CreateExporter(HttpMessageHandler handler) => new(
		new HttpClient(handler),
		new GoogleSheetsExportSettings
		{
			WebhookUrl = "https://script.google.com/macros/s/test/exec",
			SharedSecret = "test-secret"
		});

	private static Match CreateCompletedMatch() => new()
	{
		Id = MatchId,
		TeamId = TeamId,
		Team = ClubTeam.First,
		Opponent = "Rosehill",
		Date = new DateTime(2026, 9, 5, 14, 30, 0, DateTimeKind.Utc),
		Venue = MatchVenue.Home,
		IsCompleted = true,
		Result = new MatchResult { HomeGoals = 3, AwayGoals = 1 },
		PlayerStats =
		[
			new MatchPlayerStats
			{
				PlayerId = StarterId,
				AppearanceType = MatchAppearanceType.Started,
				Goals = 2
			},
			new MatchPlayerStats
			{
				PlayerId = UnusedSubstituteId,
				AppearanceType = MatchAppearanceType.UnusedSubstitute,
				Goals = 0
			}
		]
	};

	private static IReadOnlyList<Player> CreatePlayers() =>
	[
		new Player { Id = StarterId, Number = 9, Name = "Starter Player" },
		new Player { Id = UnusedSubstituteId, Number = 12, Name = "Unused Player" },
		new Player { Id = AbsentPlayerId, Number = 3, Name = "Absent Player" }
	];

	private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
	{
		Content = new StringContent(body, Encoding.UTF8, "application/json")
	};

	private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) => Task.FromResult(respond(request));
	}
}
