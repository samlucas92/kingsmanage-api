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
	Task<SocialInsightsOverview> GetInsightsOverviewAsync(MetaPageConnection page, string pageAccessToken, CancellationToken cancellationToken = default);
	Task<SocialPostInsightsDetail> GetPostInsightsAsync(SocialPlatform platform, string postId, string pageAccessToken, CancellationToken cancellationToken = default);
}

public sealed class MetaGraphClient : IMetaGraphClient
{
	private const string PageDiscoveryFields = "id,name,access_token,tasks,instagram_business_account{id,username,name,profile_picture_url}";
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
		var scopes = "pages_show_list,pages_manage_posts,pages_read_engagement,read_insights,instagram_basic,instagram_content_publish,instagram_manage_insights,business_management";
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
		var pages = await DiscoverPagesAsync(userToken, cancellationToken);
		return new MetaAuthorizationResult(
			RequiredString(user.RootElement, "id"),
			RequiredString(user.RootElement, "name"),
			userToken,
			expiresIn is int value ? DateTime.UtcNow.AddSeconds(value) : null,
			pages);
	}

	private async Task<IReadOnlyList<MetaGraphPage>> DiscoverPagesAsync(string userToken, CancellationToken cancellationToken)
	{
		var pages = new Dictionary<string, MetaGraphPage>(StringComparer.Ordinal);
		await AddPagesAsync($"me/accounts?fields={Escape(PageDiscoveryFields)}&limit=100&access_token={Escape(userToken)}", userToken, pages, cancellationToken);

		try
		{
			var businessesDocument = await GetJsonAsync($"me/businesses?fields=id,name&limit=100&access_token={Escape(userToken)}", cancellationToken);
			if (businessesDocument.RootElement.TryGetProperty("data", out var businesses))
			{
				foreach (var business in businesses.EnumerateArray())
				{
					var businessId = OptionalString(business, "id");
					if (string.IsNullOrWhiteSpace(businessId)) continue;
					await TryAddBusinessPagesAsync(businessId, "owned_pages", userToken, pages, cancellationToken);
					await TryAddBusinessPagesAsync(businessId, "client_pages", userToken, pages, cancellationToken);
				}
			}
		}
		catch (InvalidOperationException)
		{
			// Business Portfolio discovery is additive. Direct Page discovery must still work
			// when the user declines business_management or has no portfolio access.
		}

		return pages.Values.OrderBy(page => page.Name, StringComparer.OrdinalIgnoreCase).ToList();
	}

	private async Task TryAddBusinessPagesAsync(string businessId, string edge, string userToken, IDictionary<string, MetaGraphPage> pages, CancellationToken cancellationToken)
	{
		try
		{
			await AddPagesAsync($"{Escape(businessId)}/{edge}?fields={Escape(PageDiscoveryFields)}&limit=100&access_token={Escape(userToken)}", userToken, pages, cancellationToken);
		}
		catch (InvalidOperationException)
		{
			// A portfolio can expose only one of the owned/client Page edges to this user.
		}
	}

	private async Task AddPagesAsync(string path, string userToken, IDictionary<string, MetaGraphPage> pages, CancellationToken cancellationToken)
	{
		var document = await GetJsonAsync(path, cancellationToken);
		if (!document.RootElement.TryGetProperty("data", out var data)) return;
		foreach (var item in data.EnumerateArray())
		{
			var page = await ParseDiscoverablePageAsync(item, userToken, cancellationToken);
			if (page is not null) pages.TryAdd(page.Id, page);
		}
	}

	private async Task<MetaGraphPage?> ParseDiscoverablePageAsync(JsonElement item, string userToken, CancellationToken cancellationToken)
	{
		var id = OptionalString(item, "id");
		if (string.IsNullOrWhiteSpace(id)) return null;

		var source = item;
		if (string.IsNullOrWhiteSpace(OptionalString(source, "access_token")))
		{
			try
			{
				var pageDocument = await GetJsonAsync($"{Escape(id)}?fields={Escape(PageDiscoveryFields)}&access_token={Escape(userToken)}", cancellationToken);
				source = pageDocument.RootElement.Clone();
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		var accessToken = OptionalString(source, "access_token");
		if (string.IsNullOrWhiteSpace(accessToken)) return null;
		MetaInstagramAccount? instagram = null;
		if (source.TryGetProperty("instagram_business_account", out var account))
		{
			instagram = new MetaInstagramAccount
			{
				Id = RequiredString(account, "id"),
				Username = OptionalString(account, "username") ?? string.Empty,
				Name = OptionalString(account, "name") ?? string.Empty,
				ProfilePictureUrl = OptionalString(account, "profile_picture_url")
			};
		}
		var tasks = source.TryGetProperty("tasks", out var taskValues)
			? taskValues.EnumerateArray().Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToList()
			: [];
		return new MetaGraphPage(id, OptionalString(source, "name") ?? OptionalString(item, "name") ?? id, accessToken, tasks, instagram);
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

	public async Task<SocialInsightsOverview> GetInsightsOverviewAsync(MetaPageConnection page, string pageAccessToken, CancellationToken cancellationToken = default)
	{
		var facebookAccount = await GetJsonAsync($"{page.Id}?fields=id,name,fan_count,followers_count&access_token={Escape(pageAccessToken)}", cancellationToken);
		var facebookPosts = await GetJsonAsync($"{page.Id}/published_posts?fields=id,message,created_time,permalink_url,full_picture,shares,comments.limit(0).summary(true),likes.limit(0).summary(true)&limit=50&access_token={Escape(pageAccessToken)}", cancellationToken);
		var posts = ParseFacebookPosts(facebookPosts.RootElement).ToList();
		var accounts = new List<SocialAccountInsights>
		{
			new()
			{
				Platform = SocialPlatform.Facebook,
				Name = OptionalString(facebookAccount.RootElement, "name") ?? page.Name,
				FollowerCount = OptionalLong(facebookAccount.RootElement, "followers_count") ?? OptionalLong(facebookAccount.RootElement, "fan_count")
			}
		};

		if (page.InstagramAccount is { } instagram)
		{
			var instagramAccount = await GetJsonAsync($"{instagram.Id}?fields=id,name,username,followers_count,media_count,profile_picture_url&access_token={Escape(pageAccessToken)}", cancellationToken);
			var instagramMedia = await GetJsonAsync($"{instagram.Id}/media?fields=id,caption,media_type,media_url,thumbnail_url,permalink,timestamp,like_count,comments_count&limit=50&access_token={Escape(pageAccessToken)}", cancellationToken);
			accounts.Add(new SocialAccountInsights
			{
				Platform = SocialPlatform.Instagram,
				Name = OptionalString(instagramAccount.RootElement, "name") ?? instagram.Name,
				Username = OptionalString(instagramAccount.RootElement, "username") ?? instagram.Username,
				FollowerCount = OptionalLong(instagramAccount.RootElement, "followers_count"),
				PostCount = OptionalLong(instagramAccount.RootElement, "media_count")
			});
			posts.AddRange(ParseInstagramPosts(instagramMedia.RootElement));
		}

		return new SocialInsightsOverview
		{
			GeneratedAt = DateTime.UtcNow,
			Accounts = accounts,
			Posts = posts.OrderByDescending(item => item.CreatedAt).ToList()
		};
	}

	public async Task<SocialPostInsightsDetail> GetPostInsightsAsync(SocialPlatform platform, string postId, string pageAccessToken, CancellationToken cancellationToken = default)
	{
		if (platform == SocialPlatform.Instagram)
		{
			var post = await GetJsonAsync($"{Escape(postId)}?fields=id,caption,media_type,media_url,thumbnail_url,permalink,timestamp,like_count,comments_count&access_token={Escape(pageAccessToken)}", cancellationToken);
			var summary = ParseInstagramPost(post.RootElement);
			var metrics = await GetMetricsIndividuallyAsync(postId, ["views", "reach", "likes", "comments", "shares", "saved", "total_interactions"], pageAccessToken, cancellationToken);
			return SocialPostInsightsDetail.From(summary, metrics);
		}

		var facebookPost = await GetJsonAsync($"{Escape(postId)}?fields=id,message,created_time,permalink_url,full_picture,shares,comments.limit(0).summary(true),likes.limit(0).summary(true)&access_token={Escape(pageAccessToken)}", cancellationToken);
		var facebookSummary = ParseFacebookPost(facebookPost.RootElement);
		var facebookMetrics = await GetMetricsIndividuallyAsync(postId, ["post_impressions", "post_impressions_unique", "post_engaged_users", "post_clicks"], pageAccessToken, cancellationToken);
		return SocialPostInsightsDetail.From(facebookSummary, facebookMetrics);
	}

	private async Task<Dictionary<string, long>> GetMetricsIndividuallyAsync(string objectId, IReadOnlyList<string> metricNames, string accessToken, CancellationToken cancellationToken)
	{
		var values = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		string? permissionError = null;
		foreach (var metric in metricNames)
		{
			try
			{
				var document = await GetJsonAsync($"{Escape(objectId)}/insights?metric={Escape(metric)}&access_token={Escape(accessToken)}", cancellationToken);
				if (!document.RootElement.TryGetProperty("data", out var data)) continue;
				var item = data.EnumerateArray().FirstOrDefault();
				if (item.ValueKind == JsonValueKind.Undefined || !item.TryGetProperty("values", out var metricValues)) continue;
				var value = metricValues.EnumerateArray().LastOrDefault();
				if (value.ValueKind != JsonValueKind.Undefined && value.TryGetProperty("value", out var metricValue) && metricValue.TryGetInt64(out var number)) values[metric] = number;
			}
			catch (InvalidOperationException exception)
			{
				if (exception.Message.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
					exception.Message.Contains("OAuth", StringComparison.OrdinalIgnoreCase) ||
					exception.Message.Contains("(#200)", StringComparison.OrdinalIgnoreCase))
				{
					permissionError ??= exception.Message;
				}
			}
		}
		if (values.Count == 0 && permissionError is not null) throw new InvalidOperationException(permissionError);
		return values;
	}

	private static IEnumerable<SocialPostInsightsSummary> ParseFacebookPosts(JsonElement root)
	{
		if (!root.TryGetProperty("data", out var data)) yield break;
		foreach (var item in data.EnumerateArray()) yield return ParseFacebookPost(item);
	}

	private static SocialPostInsightsSummary ParseFacebookPost(JsonElement item) => new()
	{
		Platform = SocialPlatform.Facebook,
		Id = RequiredString(item, "id"),
		Caption = OptionalString(item, "message") ?? string.Empty,
		CreatedAt = OptionalDateTime(item, "created_time") ?? DateTime.UtcNow,
		Permalink = OptionalString(item, "permalink_url"),
		ThumbnailUrl = OptionalString(item, "full_picture"),
		LikeCount = NestedSummaryCount(item, "likes"),
		CommentCount = NestedSummaryCount(item, "comments"),
		ShareCount = item.TryGetProperty("shares", out var shares) ? OptionalLong(shares, "count") : null
	};

	private static IEnumerable<SocialPostInsightsSummary> ParseInstagramPosts(JsonElement root)
	{
		if (!root.TryGetProperty("data", out var data)) yield break;
		foreach (var item in data.EnumerateArray()) yield return ParseInstagramPost(item);
	}

	private static SocialPostInsightsSummary ParseInstagramPost(JsonElement item) => new()
	{
		Platform = SocialPlatform.Instagram,
		Id = RequiredString(item, "id"),
		Caption = OptionalString(item, "caption") ?? string.Empty,
		MediaType = OptionalString(item, "media_type"),
		CreatedAt = OptionalDateTime(item, "timestamp") ?? DateTime.UtcNow,
		Permalink = OptionalString(item, "permalink"),
		ThumbnailUrl = OptionalString(item, "thumbnail_url") ?? OptionalString(item, "media_url"),
		LikeCount = OptionalLong(item, "like_count"),
		CommentCount = OptionalLong(item, "comments_count")
	};

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
	private static long? OptionalLong(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : null;
	private static DateTime? OptionalDateTime(JsonElement element, string name) => element.TryGetProperty(name, out var value) && DateTime.TryParse(value.GetString(), out var date) ? date.ToUniversalTime() : null;
	private static long? NestedSummaryCount(JsonElement element, string name) => element.TryGetProperty(name, out var edge) && edge.TryGetProperty("summary", out var summary) ? OptionalLong(summary, "total_count") : null;
	private void EnsureConfigured()
	{
		if (!settings.OAuthIsConfigured) throw new InvalidOperationException("Meta OAuth has not been configured for this deployment.");
	}
}
