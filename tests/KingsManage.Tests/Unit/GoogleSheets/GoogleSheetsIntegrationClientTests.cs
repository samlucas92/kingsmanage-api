using KingsManage.Web.Models;
using KingsManage.Web.Services;

namespace KingsManage.Tests.Unit.GoogleSheets;

[TestFixture]
public sealed class GoogleSheetsIntegrationClientTests
{
	[Test]
	public void AuthorizationUrl_UsesPerFileDriveScopeAndOfflineAccess()
	{
		var client = new GoogleSheetsIntegrationClient(new HttpClient(), new GoogleSheetsIntegrationSettings
		{
			ClientId = "client-id",
			ClientSecret = "client-secret",
			RedirectUri = "https://example.test/organization/integrations/google-sheets/callback"
		});

		var url = Uri.UnescapeDataString(client.BuildAuthorizationUrl("state-value"));

		Assert.Multiple(() =>
		{
			Assert.That(url, Does.Contain("https://www.googleapis.com/auth/drive.file"));
			Assert.That(url, Does.Not.Contain("https://www.googleapis.com/auth/drive" + "&"));
			Assert.That(url, Does.Contain("access_type=offline"));
			Assert.That(url, Does.Contain("prompt=consent"));
			Assert.That(url, Does.Contain("state=state-value"));
		});
	}
}
