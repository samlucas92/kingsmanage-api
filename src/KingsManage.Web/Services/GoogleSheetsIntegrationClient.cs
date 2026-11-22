using System.Net.Http.Headers;
using System.Text.Json;
using KingsManage;
using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed record GoogleAuthorizationResult(string RefreshToken, string? Email);
public sealed record GoogleAccessToken(string Value, DateTime ExpiresAt);
public sealed record GoogleSpreadsheetMetadata(string Id, string Name, string Url, IReadOnlyList<GoogleSheetTab> Sheets);

public interface IGoogleSheetsIntegrationClient
{
	string BuildAuthorizationUrl(string state);
	Task<GoogleAuthorizationResult> CompleteAuthorizationAsync(string code, CancellationToken cancellationToken = default);
	Task<GoogleAccessToken> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
	Task<GoogleSpreadsheetMetadata> GetSpreadsheetAsync(string accessToken, string spreadsheetId, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<IReadOnlyList<string>>> GetValuesAsync(string accessToken, string spreadsheetId, string sheetName, CancellationToken cancellationToken = default);
}

public sealed class GoogleSheetsIntegrationClient : IGoogleSheetsIntegrationClient
{
	private readonly HttpClient http;
	private readonly GoogleSheetsIntegrationSettings settings;

	public GoogleSheetsIntegrationClient(HttpClient http, GoogleSheetsIntegrationSettings settings)
	{
		this.http = http;
		this.settings = settings;
	}

	public string BuildAuthorizationUrl(string state)
	{
		EnsureConfigured();
		var scope = "openid email profile https://www.googleapis.com/auth/drive.file";
		return "https://accounts.google.com/o/oauth2/v2/auth" +
			$"?client_id={Escape(settings.ClientId)}" +
			$"&redirect_uri={Escape(settings.RedirectUri)}" +
			$"&response_type=code&access_type=offline&prompt=consent&include_granted_scopes=true" +
			$"&scope={Escape(scope)}&state={Escape(state)}";
	}

	public async Task<GoogleAuthorizationResult> CompleteAuthorizationAsync(string code, CancellationToken cancellationToken = default)
	{
		EnsureConfigured();
		using var tokenResponse = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["client_id"] = settings.ClientId,
			["client_secret"] = settings.ClientSecret,
			["code"] = code,
			["grant_type"] = "authorization_code",
			["redirect_uri"] = settings.RedirectUri
		}), cancellationToken);
		using var token = await ReadJsonAsync(tokenResponse, "Google authorization failed.", cancellationToken);
		var refreshToken = RequiredString(token.RootElement, "refresh_token");
		var accessToken = RequiredString(token.RootElement, "access_token");

		using var userRequest = AuthorizedRequest(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo", accessToken);
		using var userResponse = await http.SendAsync(userRequest, cancellationToken);
		using var user = await ReadJsonAsync(userResponse, "Google account details could not be loaded.", cancellationToken);
		return new GoogleAuthorizationResult(refreshToken, OptionalString(user.RootElement, "email"));
	}

	public async Task<GoogleAccessToken> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
	{
		EnsureConfigured();
		using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["client_id"] = settings.ClientId,
			["client_secret"] = settings.ClientSecret,
			["refresh_token"] = refreshToken,
			["grant_type"] = "refresh_token"
		}), cancellationToken);
		using var document = await ReadJsonAsync(response, "Google access could not be refreshed. Reconnect the account.", cancellationToken);
		var seconds = document.RootElement.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var value) ? value : 3600;
		return new GoogleAccessToken(RequiredString(document.RootElement, "access_token"), DateTime.UtcNow.AddSeconds(Math.Max(seconds - 60, 60)));
	}

	public async Task<GoogleSpreadsheetMetadata> GetSpreadsheetAsync(string accessToken, string spreadsheetId, CancellationToken cancellationToken = default)
	{
		var url = $"https://sheets.googleapis.com/v4/spreadsheets/{Escape(spreadsheetId)}?fields=spreadsheetId,properties(title),sheets(properties(sheetId,title,index))";
		using var request = AuthorizedRequest(HttpMethod.Get, url, accessToken);
		using var response = await http.SendAsync(request, cancellationToken);
		using var document = await ReadJsonAsync(response, "The selected Google Sheet could not be opened. Select it again and check the connected account has access.", cancellationToken);
		var root = document.RootElement;
		var id = RequiredString(root, "spreadsheetId");
		var name = root.TryGetProperty("properties", out var properties) ? RequiredString(properties, "title") : id;
		var sheets = new List<GoogleSheetTab>();
		if (root.TryGetProperty("sheets", out var sheetValues))
		{
			foreach (var item in sheetValues.EnumerateArray())
			{
				if (!item.TryGetProperty("properties", out var sheet)) continue;
				sheets.Add(new GoogleSheetTab
				{
					SheetId = sheet.TryGetProperty("sheetId", out var sheetId) ? sheetId.GetInt32() : 0,
					Title = RequiredString(sheet, "title"),
					Index = sheet.TryGetProperty("index", out var index) ? index.GetInt32() : sheets.Count
				});
			}
		}
		return new GoogleSpreadsheetMetadata(id, name, $"https://docs.google.com/spreadsheets/d/{id}/edit", sheets.OrderBy(item => item.Index).ToList());
	}

	public async Task<IReadOnlyList<IReadOnlyList<string>>> GetValuesAsync(string accessToken, string spreadsheetId, string sheetName, CancellationToken cancellationToken = default)
	{
		var escapedSheet = sheetName.Replace("'", "''", StringComparison.Ordinal);
		var range = Escape($"'{escapedSheet}'!A1:ZZ200");
		var url = $"https://sheets.googleapis.com/v4/spreadsheets/{Escape(spreadsheetId)}/values/{range}?majorDimension=ROWS&valueRenderOption=FORMATTED_VALUE";
		using var request = AuthorizedRequest(HttpMethod.Get, url, accessToken);
		using var response = await http.SendAsync(request, cancellationToken);
		using var document = await ReadJsonAsync(response, "The selected sheet tab could not be loaded.", cancellationToken);
		if (!document.RootElement.TryGetProperty("values", out var values)) return [];
		return values.EnumerateArray()
			.Select(row => (IReadOnlyList<string>)row.EnumerateArray().Select(cell => cell.ToString()).ToList())
			.ToList();
	}

	private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string accessToken)
	{
		var request = new HttpRequestMessage(method, url);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		return request;
	}

	private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, string fallback, CancellationToken cancellationToken)
	{
		var content = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			try
			{
				using var error = JsonDocument.Parse(content);
				if (error.RootElement.TryGetProperty("error", out var value))
				{
					if (value.ValueKind == JsonValueKind.String) throw new InvalidOperationException($"{fallback} {value.GetString()}");
					if (value.TryGetProperty("message", out var message)) throw new InvalidOperationException($"{fallback} {message.GetString()}");
				}
			}
			catch (JsonException) { }
			throw new InvalidOperationException(fallback);
		}
		return JsonDocument.Parse(content);
	}

	private void EnsureConfigured()
	{
		if (!settings.OAuthIsConfigured) throw new InvalidOperationException("Google Sheets OAuth is not configured on the API.");
	}

	private static string RequiredString(JsonElement value, string name) =>
		OptionalString(value, name) ?? throw new InvalidOperationException($"Google did not return {name}.");
	private static string? OptionalString(JsonElement value, string name) =>
		value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
	private static string Escape(string value) => Uri.EscapeDataString(value);
}
