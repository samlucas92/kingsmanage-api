using System.Net.Http.Json;
using System.Text.Json;
using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface IGoogleSheetsMatchExporter
{
	bool IsConfigured { get; }

	Task<GoogleSheetsMatchExportResult> ExportAsync(
		Match match,
		IReadOnlyList<Player> players,
		Guid teamId,
		string teamName,
		CancellationToken cancellationToken = default);
}

public sealed class GoogleSheetsExportException(string message, Exception? innerException = null)
	: Exception(message, innerException);

public sealed class GoogleSheetsMatchExporter(
	HttpClient httpClient,
	GoogleSheetsExportSettings settings) : IGoogleSheetsMatchExporter
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	public bool IsConfigured => settings.IsConfigured;

	public async Task<GoogleSheetsMatchExportResult> ExportAsync(
		Match match,
		IReadOnlyList<Player> players,
		Guid teamId,
		string teamName,
		CancellationToken cancellationToken = default)
	{
		if (!IsConfigured)
		{
			throw new GoogleSheetsExportException(
				"Google Sheets export is not configured on the server.");
		}

		if (!match.IsCompleted || match.Result is null)
		{
			throw new GoogleSheetsExportException(
				"Only completed matches with a final result can be exported.");
		}

		if (match.PlayerStats.Count == 0)
		{
			throw new GoogleSheetsExportException(
				"Save the match events before exporting player stats.");
		}

		var payload = BuildPayload(match, players, teamId, teamName, settings.SharedSecret);

		HttpResponseMessage response;
		try
		{
			response = await httpClient.PostAsJsonAsync(
				settings.WebhookUrl,
				payload,
				JsonOptions,
				cancellationToken);
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			throw new GoogleSheetsExportException(
				"The Google Sheets webhook could not be reached.",
				exception);
		}

		using (response)
		{
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				throw new GoogleSheetsExportException(
					$"Google Sheets returned HTTP {(int)response.StatusCode}.");
			}

			GoogleSheetsWebhookResponse? webhookResponse;
			try
			{
				webhookResponse = JsonSerializer.Deserialize<GoogleSheetsWebhookResponse>(
					responseBody,
					JsonOptions);
			}
			catch (JsonException exception)
			{
				throw new GoogleSheetsExportException(
					"Google Apps Script did not return JSON. Check that the web app is deployed for public access.",
					exception);
			}

			if (webhookResponse is null)
			{
				throw new GoogleSheetsExportException(
					"Google Apps Script returned an empty response.");
			}

			if (!webhookResponse.Ok)
			{
				throw new GoogleSheetsExportException(
					string.IsNullOrWhiteSpace(webhookResponse.Error)
						? "Google Sheets rejected the export."
						: webhookResponse.Error);
			}

			return new GoogleSheetsMatchExportResult
			{
				Action = webhookResponse.Action,
				SheetName = webhookResponse.SheetName,
				MatchColumn = webhookResponse.MatchColumn,
				MatchedPlayers = webhookResponse.MatchedPlayers,
				UnmatchedPlayers = webhookResponse.UnmatchedPlayers
			};
		}
	}

	internal static GoogleSheetsMatchExportPayload BuildPayload(
		Match match,
		IReadOnlyList<Player> players,
		Guid teamId,
		string teamName,
		string sharedSecret)
	{
		var statsByPlayer = match.PlayerStats
			.GroupBy(stats => stats.PlayerId)
			.ToDictionary(group => group.Key, group => group.First());

		return new GoogleSheetsMatchExportPayload
		{
			Secret = sharedSecret,
			MatchId = match.Id.ToString(),
			TeamId = teamId.ToString(),
			TeamName = teamName,
			Date = match.Date,
			Opponent = match.Opponent,
			Venue = match.Venue.ToString().ToLowerInvariant(),
			HomeGoals = match.Result!.HomeGoals,
			AwayGoals = match.Result.AwayGoals,
			Players = players
				.OrderBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
				.Select(player => BuildPlayerRow(player, statsByPlayer))
				.ToList()
		};
	}

	private static GoogleSheetsPlayerExportRow BuildPlayerRow(
		Player player,
		IReadOnlyDictionary<Guid, MatchPlayerStats> statsByPlayer)
	{
		statsByPlayer.TryGetValue(player.Id, out var stats);
		var played = stats?.AppearanceType is
			MatchAppearanceType.Started or MatchAppearanceType.SubstituteUsed;

		return new GoogleSheetsPlayerExportRow
		{
			PlayerId = player.Id.ToString(),
			Number = player.Number,
			Name = player.Name,
			Appearance = played ? 1 : null,
			Goals = played ? Math.Max(stats!.Goals, 0) : null
		};
	}
}
