namespace KingsManage.Web.Models;

public sealed class GoogleSheetsExportSettings
{
	public string WebhookUrl { get; set; } = string.Empty;
	public string SharedSecret { get; set; } = string.Empty;

	public bool IsConfigured =>
		Uri.TryCreate(WebhookUrl, UriKind.Absolute, out var uri) &&
		uri.Scheme == Uri.UriSchemeHttps &&
		!string.IsNullOrWhiteSpace(SharedSecret);
}

public sealed class GoogleSheetsMatchExportResult
{
	public string Action { get; set; } = string.Empty;
	public string SheetName { get; set; } = string.Empty;
	public int MatchColumn { get; set; }
	public int MatchedPlayers { get; set; }
	public List<string> UnmatchedPlayers { get; set; } = [];
}

public sealed class GoogleSheetsMatchExportPayload
{
	public int SchemaVersion { get; set; } = 1;
	public string Secret { get; set; } = string.Empty;
	public string MatchId { get; set; } = string.Empty;
	public string TeamId { get; set; } = string.Empty;
	public string TeamName { get; set; } = string.Empty;
	public DateTime Date { get; set; }
	public string Opponent { get; set; } = string.Empty;
	public string Venue { get; set; } = string.Empty;
	public int HomeGoals { get; set; }
	public int AwayGoals { get; set; }
	public List<GoogleSheetsPlayerExportRow> Players { get; set; } = [];
}

public sealed class GoogleSheetsPlayerExportRow
{
	public string PlayerId { get; set; } = string.Empty;
	public int Number { get; set; }
	public string Name { get; set; } = string.Empty;
	public int? Appearance { get; set; }
	public int? Goals { get; set; }
}

internal sealed class GoogleSheetsWebhookResponse
{
	public bool Ok { get; set; }
	public string? Error { get; set; }
	public string Action { get; set; } = string.Empty;
	public string SheetName { get; set; } = string.Empty;
	public int MatchColumn { get; set; }
	public int MatchedPlayers { get; set; }
	public List<string> UnmatchedPlayers { get; set; } = [];
}
