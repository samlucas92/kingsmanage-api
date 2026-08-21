using System.Security.Cryptography;
using KingsManage.Web.Models;
using KingsManage.Web.Services;

namespace KingsManage.Tests.Unit.Services;

[TestFixture]
public sealed class IntegrationSecretProtectorTests
{
	[Test]
	public void Protect_RoundTripsWithoutContainingPlaintext()
	{
		var protector = CreateProtector();
		const string token = "EAAB-example-sensitive-token";

		var encrypted = protector.Protect(token);

		Assert.Multiple(() =>
		{
			Assert.That(encrypted, Does.StartWith("v1."));
			Assert.That(encrypted, Does.Not.Contain(token));
			Assert.That(protector.Unprotect(encrypted), Is.EqualTo(token));
		});
	}

	[Test]
	public void Unprotect_WhenCiphertextIsChanged_RejectsIt()
	{
		var protector = CreateProtector();
		var encrypted = protector.Protect("secret");
		var tampered = encrypted[..^2] + (encrypted[^2] == 'A' ? "B" : "A") + encrypted[^1];

		Assert.That(() => protector.Unprotect(tampered), Throws.TypeOf<CryptographicException>());
	}

	[Test]
	public void Protect_WithoutAValidKey_FailsClosed()
	{
		var protector = new AesGcmIntegrationSecretProtector(new MetaIntegrationSettings());
		Assert.That(() => protector.Protect("secret"), Throws.TypeOf<InvalidOperationException>());
	}

	private static AesGcmIntegrationSecretProtector CreateProtector() => new(new MetaIntegrationSettings
	{
		TokenEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
	});
}
