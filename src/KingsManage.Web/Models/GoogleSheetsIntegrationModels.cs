using KingsManage;

namespace KingsManage.Web.Models;

public sealed class GoogleSheetsIntegrationSettings
{
	public string ClientId { get; set; } = string.Empty;
	public string ClientSecret { get; set; } = string.Empty;
	public string RedirectUri { get; set; } = string.Empty;
	public string PickerApiKey { get; set; } = string.Empty;
	public string PickerAppId { get; set; } = string.Empty;

	public bool OAuthIsConfigured =>
		!string.IsNullOrWhiteSpace(ClientId) &&
		!string.IsNullOrWhiteSpace(ClientSecret) &&
		Uri.TryCreate(RedirectUri, UriKind.Absolute, out _);
}

public sealed class GoogleSheetsIntegrationViewModel
{
	public bool IsConfigured { get; set; }
	public OrganizationIntegrationStatus Status { get; set; }
	public string? ConnectedGoogleUserEmail { get; set; }
	public string? SpreadsheetId { get; set; }
	public string? SpreadsheetName { get; set; }
	public string? SpreadsheetUrl { get; set; }
	public IReadOnlyList<GoogleSheetTab> Sheets { get; set; } = [];
	public DateTime? LastValidatedAt { get; set; }
	public string? LastError { get; set; }
}

public sealed class CompleteGoogleSheetsConnectionRequest
{
	public string Code { get; set; } = string.Empty;
	public string State { get; set; } = string.Empty;
}

public sealed class SelectGoogleSpreadsheetRequest
{
	public string SpreadsheetId { get; set; } = string.Empty;
}

public sealed class GooglePickerTokenViewModel
{
	public string AccessToken { get; set; } = string.Empty;
	public DateTime ExpiresAt { get; set; }
	public string ApiKey { get; set; } = string.Empty;
	public string AppId { get; set; } = string.Empty;
}

public sealed class GoogleSheetValuesViewModel
{
	public string SpreadsheetName { get; set; } = string.Empty;
	public string SpreadsheetUrl { get; set; } = string.Empty;
	public string SheetName { get; set; } = string.Empty;
	public IReadOnlyList<GoogleSheetTab> Sheets { get; set; } = [];
	public IReadOnlyList<IReadOnlyList<string>> Values { get; set; } = [];
	public DateTime RefreshedAt { get; set; }
}
