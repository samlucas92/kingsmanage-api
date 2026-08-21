using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KingsManage;
using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public interface IIntegrationSecretProtector
{
	string Protect(string value);
	string Unprotect(string value);
}

public sealed class AesGcmIntegrationSecretProtector : IIntegrationSecretProtector
{
	private readonly byte[] key;

	public AesGcmIntegrationSecretProtector(MetaIntegrationSettings settings)
	{
		try { key = Convert.FromBase64String(settings.TokenEncryptionKey); }
		catch (FormatException) { key = []; }
	}

	public string Protect(string value)
	{
		EnsureConfigured();
		var nonce = RandomNumberGenerator.GetBytes(12);
		var plaintext = Encoding.UTF8.GetBytes(value);
		var ciphertext = new byte[plaintext.Length];
		var tag = new byte[16];
		using var aes = new AesGcm(key, 16);
		aes.Encrypt(nonce, plaintext, ciphertext, tag);
		return $"v1.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(ciphertext)}.{Convert.ToBase64String(tag)}";
	}

	public string Unprotect(string value)
	{
		EnsureConfigured();
		try
		{
			var parts = value.Split('.');
			if (parts.Length != 4 || parts[0] != "v1") throw new CryptographicException("The encrypted integration credential is invalid.");
			var nonce = Convert.FromBase64String(parts[1]);
			var ciphertext = Convert.FromBase64String(parts[2]);
			var tag = Convert.FromBase64String(parts[3]);
			if (nonce.Length != 12 || tag.Length != 16) throw new CryptographicException("The encrypted integration credential is invalid.");
			var plaintext = new byte[ciphertext.Length];
			using var aes = new AesGcm(key, 16);
			aes.Decrypt(nonce, ciphertext, tag, plaintext);
			return Encoding.UTF8.GetString(plaintext);
		}
		catch (Exception exception) when (exception is FormatException or ArgumentException)
		{
			throw new CryptographicException("The encrypted integration credential is invalid.", exception);
		}
	}

	private void EnsureConfigured()
	{
		if (key.Length != 32) throw new InvalidOperationException("META_TOKEN_ENCRYPTION_KEY must be a base64-encoded 32-byte key.");
	}
}

public sealed record MetaAuthorizationResult(
	string MetaUserId,
	string MetaUserName,
	string UserAccessToken,
	DateTime? TokenExpiresAt,
	IReadOnlyList<MetaGraphPage> Pages);

public sealed record MetaGraphPage(
	string Id,
	string Name,
	string AccessToken,
	IReadOnlyList<string> Tasks,
	MetaInstagramAccount? InstagramAccount);

public interface IMetaGraphClient
{
	string BuildAuthorizationUrl(string state);
	Task<MetaAuthorizationResult> CompleteAuthorizationAsync(string code, CancellationToken cancellationToken = default);
	Task ValidateAsync(string accessToken, CancellationToken cancellationToken = default);
	Task<string> PublishFacebookPhotoAsync(string pageId, string pageAccessToken, string mediaUrl, string caption, CancellationToken cancellationToken = default);
	Task<string> PublishInstagramImageAsync(string instagramAccountId, string pageAccessToken, string mediaUrl, string caption, CancellationToken cancellationToken = default);
}

public sealed class MetaGraphClient : IMetaGraphClient
{
	private readonly HttpClient http;
	private readonly MetaIntegrationSettings settings;

	public MetaGraphClient(HttpClient http, MetaIntegrationSettings settings)
	{
		this.http = http;
		this.settings = settings;
	}

	public string BuildAuthorizationUrl(string state)
	{
		EnsureConfigured();
		var scopes = "pages_show_list,pages_manage_posts,pages_read_engagement,instagram_basic,instagram_content_publish";
		return $"https://www.facebook.com/{settings.GraphApiVersion}/dialog/oauth?client_id={Uri.EscapeDataString(settings.AppId)}&redirect_uri={Uri.EscapeDataString(settings.RedirectUri)}&state={Uri.EscapeDataString(state)}&scope={Uri.EscapeDataString(scopes)}&response_type=code";
	}

	public async Task<MetaAuthorizationResult> CompleteAuthorizationAsync(string code, CancellationToken cancellationToken = default)
	{
		EnsureConfigured();
		var token = await GetJsonAsync($"oauth/access_token?client_id={Escape(settings.AppId)}&client_secret={Escape(settings.AppSecret)}&redirect_uri={Escape(settings.RedirectUri)}&code={Escape(code)}", cancellationToken);
		var shortToken = RequiredString(token.RootElement, "access_token");
		var longTokenDocument = await GetJsonAsync($"oauth/access_token?grant_type=fb_exchange_token&client_id={Escape(settings.AppId)}&client_secret={Escape(settings.AppSecret)}&fb_exchange_token={Escape(shortToken)}", cancellationToken);
		var userToken = RequiredString(longTokenDocument.RootElement, "access_token");
		var expiresIn = longTokenDocument.RootElement.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds) ? seconds : (int?)null;
		var user = await GetJsonAsync($"me?fields=id,name&access_token={Escape(userToken)}", cancellationToken);
		var pagesDocument = await GetJsonAsync($"me/accounts?fields=id,name,access_token,tasks,instagram_business_account{{id,username,name,profile_picture_url}}&limit=100&access_token={Escape(userToken)}", cancellationToken);
		var pages = new List<MetaGraphPage>();
		if (pagesDocument.RootElement.TryGetProperty("data", out var data))
		{
			foreach (var page in data.EnumerateArray())
			{
				MetaInstagramAccount? instagram = null;
				if (page.TryGetProperty("instagram_business_account", out var account))
				{
					instagram = new MetaInstagramAccount
					{
						Id = RequiredString(account, "id"),
						Username = OptionalString(account, "username") ?? string.Empty,
						Name = OptionalString(account, "name") ?? string.Empty,
						ProfilePictureUrl = OptionalString(account, "profile_picture_url")
					};
				}
				var tasks = page.TryGetProperty("tasks", out var taskValues)
					? taskValues.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToList()
					: [];
				pages.Add(new MetaGraphPage(RequiredString(page, "id"), RequiredString(page, "name"), RequiredString(page, "access_token"), tasks, instagram));
			}
		}
		return new MetaAuthorizationResult(
			RequiredString(user.RootElement, "id"),
			RequiredString(user.RootElement, "name"),
			userToken,
			expiresIn is int value ? DateTime.UtcNow.AddSeconds(value) : null,
			pages);
	}

	public async Task ValidateAsync(string accessToken, CancellationToken cancellationToken = default) =>
		_ = await GetJsonAsync($"me?fields=id&access_token={Escape(accessToken)}", cancellationToken);

	public async Task<string> PublishFacebookPhotoAsync(string pageId, string pageAccessToken, string mediaUrl, string caption, CancellationToken cancellationToken = default)
	{
		var response = await PostFormAsync($"{pageId}/photos", new Dictionary<string, string> { ["url"] = mediaUrl, ["caption"] = caption, ["published"] = "true", ["access_token"] = pageAccessToken }, cancellationToken);
		return OptionalString(response.RootElement, "post_id") ?? RequiredString(response.RootElement, "id");
	}

	public async Task<string> PublishInstagramImageAsync(string instagramAccountId, string pageAccessToken, string mediaUrl, string caption, CancellationToken cancellationToken = default)
	{
		var container = await PostFormAsync($"{instagramAccountId}/media", new Dictionary<string, string> { ["image_url"] = mediaUrl, ["caption"] = caption, ["access_token"] = pageAccessToken }, cancellationToken);
		var containerId = RequiredString(container.RootElement, "id");
		for (var attempt = 0; attempt < 10; attempt++)
		{
			var status = await GetJsonAsync($"{containerId}?fields=status_code&access_token={Escape(pageAccessToken)}", cancellationToken);
			var code = OptionalString(status.RootElement, "status_code");
			if (code == "FINISHED") break;
			if (code is "ERROR" or "EXPIRED") throw new InvalidOperationException("Instagram could not prepare the image for publishing.");
			await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
		}
		var published = await PostFormAsync($"{instagramAccountId}/media_publish", new Dictionary<string, string> { ["creation_id"] = containerId, ["access_token"] = pageAccessToken }, cancellationToken);
		return RequiredString(published.RootElement, "id");
	}

	private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken cancellationToken)
	{
		using var response = await http.GetAsync(GraphUrl(path), cancellationToken);
		return await ReadResponseAsync(response, cancellationToken);
	}

	private async Task<JsonDocument> PostFormAsync(string path, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
	{
		using var response = await http.PostAsync(GraphUrl(path), new FormUrlEncodedContent(values), cancellationToken);
		return await ReadResponseAsync(response, cancellationToken);
	}

	private static async Task<JsonDocument> ReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		var body = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			try
			{
				using var document = JsonDocument.Parse(body);
				var message = document.RootElement.GetProperty("error").GetProperty("message").GetString();
				throw new InvalidOperationException(message ?? "Meta rejected the request.");
			}
			catch (JsonException) { throw new InvalidOperationException("Meta rejected the request."); }
		}
		return JsonDocument.Parse(body);
	}

	private string GraphUrl(string path) => $"https://graph.facebook.com/{settings.GraphApiVersion}/{path}";
	private static string Escape(string value) => Uri.EscapeDataString(value);
	private static string RequiredString(JsonElement element, string name) => OptionalString(element, name) ?? throw new InvalidOperationException($"Meta did not return {name}.");
	private static string? OptionalString(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value.GetString() : null;
	private void EnsureConfigured()
	{
		if (!settings.OAuthIsConfigured) throw new InvalidOperationException("Meta OAuth has not been configured for this deployment.");
	}
}
