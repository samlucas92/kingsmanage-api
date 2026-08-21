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
			Assert.That(url, Does.Contain("read_insights"));
			Assert.That(url, Does.Contain("instagram_content_publish"));
			Assert.That(url, Does.Contain("instagram_manage_insights"));
		});
	}
}
