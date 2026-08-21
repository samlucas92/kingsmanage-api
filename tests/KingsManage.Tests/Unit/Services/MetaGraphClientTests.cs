using KingsManage.Web.Models;
using KingsManage.Web.Services;

namespace KingsManage.Tests.Unit.Services;

[TestFixture]
public sealed class MetaGraphClientTests
{
	[Test]
	public void AuthorizationUrl_RequestsPublishingAndInsightsPermissions()
	{
		var client = new MetaGraphClient(new HttpClient(), new MetaIntegrationSettings
		{
			AppId = "test-app",
			AppSecret = "test-secret",
			RedirectUri = "https://example.com/meta/callback"
		});

		var url = Uri.UnescapeDataString(client.BuildAuthorizationUrl("state"));

		Assert.Multiple(() =>
		{
			Assert.That(url, Does.Contain("pages_manage_posts"));
			Assert.That(url, Does.Contain("pages_read_engagement"));
			Assert.That(url, Does.Contain("pages_read_user_content"));
			Assert.That(url, Does.Contain("read_insights"));
			Assert.That(url, Does.Contain("instagram_content_publish"));
			Assert.That(url, Does.Contain("instagram_manage_insights"));
			Assert.That(url, Does.Contain("business_management"));
		});
	}

	[Test]
	public async Task CreateFacebookDraftPhoto_CreatesAnUnpublishedDraft()
	{
		string? body = null;
		var handler = new StubMetaHandler(request =>
		{
			body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
			return Json("""{"post_id":"draft-post"}""");
		});
		var client = new MetaGraphClient(new HttpClient(handler), Settings());

		var result = await client.CreateFacebookDraftPhotoAsync("page", "token", "https://example.com/image.jpg", "Caption");

		Assert.Multiple(() =>
		{
			Assert.That(result, Is.EqualTo("draft-post"));
			Assert.That(body, Does.Contain("published=false"));
			Assert.That(body, Does.Contain("unpublished_content_type=DRAFT"));
		});
	}

	[Test]
	public async Task CompleteAuthorization_MergesDirectAndBusinessPortfolioPages()
	{
		var handler = new StubMetaHandler(request =>
		{
			var path = request.RequestUri?.AbsolutePath ?? string.Empty;
			var query = request.RequestUri?.Query ?? string.Empty;
			return path switch
			{
				_ when path.EndsWith("/oauth/access_token") && query.Contains("fb_exchange_token") => Json("""{"access_token":"long-token","expires_in":3600}"""),
				_ when path.EndsWith("/oauth/access_token") => Json("""{"access_token":"short-token"}"""),
				_ when path.EndsWith("/me/accounts") => Json("""{"data":[{"id":"gaming","name":"Gaming Century","access_token":"gaming-token","tasks":["CREATE_CONTENT"]}]}"""),
				_ when path.EndsWith("/me/businesses") => Json("""{"data":[{"id":"portfolio","name":"Club portfolio"}]}"""),
				_ when path.EndsWith("/portfolio/owned_pages") => Json("""{"data":[{"id":"kingsbridge","name":"Kingsbridge Colts Football Club","access_token":"club-token","tasks":["CREATE_CONTENT","MANAGE"]}]}"""),
				_ when path.EndsWith("/portfolio/client_pages") => Json("""{"data":[]}"""),
				_ when path.EndsWith("/me") => Json("""{"id":"user","name":"Sam"}"""),
				_ => throw new InvalidOperationException($"Unexpected Meta request: {request.RequestUri}")
			};
		});
		var client = new MetaGraphClient(new HttpClient(handler), Settings());

		var result = await client.CompleteAuthorizationAsync("code");

		Assert.That(result.Pages.Select(page => page.Name), Is.EqualTo(new[] { "Gaming Century", "Kingsbridge Colts Football Club" }));
		Assert.That(result.Pages.Single(page => page.Id == "kingsbridge").AccessToken, Is.EqualTo("club-token"));
	}

	[Test]
	public async Task CompleteAuthorization_KeepsDirectPagesWhenBusinessDiscoveryIsUnavailable()
	{
		var handler = new StubMetaHandler(request =>
		{
			var path = request.RequestUri?.AbsolutePath ?? string.Empty;
			var query = request.RequestUri?.Query ?? string.Empty;
			return path switch
			{
				_ when path.EndsWith("/oauth/access_token") && query.Contains("fb_exchange_token") => Json("""{"access_token":"long-token"}"""),
				_ when path.EndsWith("/oauth/access_token") => Json("""{"access_token":"short-token"}"""),
				_ when path.EndsWith("/me/accounts") => Json("""{"data":[{"id":"gaming","name":"Gaming Century","access_token":"gaming-token","tasks":[]}]}"""),
				_ when path.EndsWith("/me/businesses") => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden)
				{
					Content = new StringContent("""{"error":{"message":"Missing business_management permission"}}""")
				},
				_ when path.EndsWith("/me") => Json("""{"id":"user","name":"Sam"}"""),
				_ => throw new InvalidOperationException($"Unexpected Meta request: {request.RequestUri}")
			};
		});
		var client = new MetaGraphClient(new HttpClient(handler), Settings());

		var result = await client.CompleteAuthorizationAsync("code");

		Assert.That(result.Pages.Select(page => page.Name), Is.EqualTo(new[] { "Gaming Century" }));
	}

	private static MetaIntegrationSettings Settings() => new()
	{
		AppId = "test-app",
		AppSecret = "test-secret",
		RedirectUri = "https://example.com/meta/callback"
	};

	private static HttpResponseMessage Json(string value) => new(System.Net.HttpStatusCode.OK)
	{
		Content = new StringContent(value, System.Text.Encoding.UTF8, "application/json")
	};

	private sealed class StubMetaHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(respond(request));
	}
}
