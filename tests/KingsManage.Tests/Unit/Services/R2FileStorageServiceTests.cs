using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using KingsManage.Web.Models;
using KingsManage.Web.Services;

namespace KingsManage.Tests.Unit.Services;

[TestFixture]
public sealed class R2FileStorageServiceTests
{
	[Test]
	public async Task ValidateObjectAsync_WhenContentMatches_ReturnsValidResult()
	{
		var content = "verified file content"u8.ToArray();
		var service = CreateService(content, "application/pdf");
		var expectedHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

		var result = await service.ValidateObjectAsync(
			"organization/file.pdf",
			expectedHash,
			"application/pdf",
			content.Length
		);

		Assert.Multiple(() =>
		{
			Assert.That(result.IsValid, Is.True);
			Assert.That(result.ContentHash, Is.EqualTo(expectedHash));
			Assert.That(result.ContentType, Is.EqualTo("application/pdf"));
			Assert.That(result.SizeBytes, Is.EqualTo(content.Length));
		});
	}

	[Test]
	public async Task ValidateObjectAsync_WhenChecksumDoesNotMatch_ReturnsInvalidResult()
	{
		var content = "different content"u8.ToArray();
		var service = CreateService(content, "application/pdf");

		var result = await service.ValidateObjectAsync(
			"organization/file.pdf",
			new string('a', 64),
			"application/pdf",
			content.Length
		);

		Assert.That(result.IsValid, Is.False);
		Assert.That(result.ErrorMessage, Does.Contain("checksum"));
	}

	[Test]
	public async Task ValidateObjectAsync_WhenSizeExceedsExpected_ReturnsInvalidResult()
	{
		var content = "content is too large"u8.ToArray();
		var service = CreateService(content, "application/pdf");

		var result = await service.ValidateObjectAsync(
			"organization/file.pdf",
			string.Empty,
			"application/pdf",
			content.Length - 1
		);

		Assert.That(result.IsValid, Is.False);
		Assert.That(result.ErrorMessage, Does.Contain("size"));
	}

	[Test]
	public async Task ValidateObjectAsync_WhenMalwareMarkerIsPresent_ReturnsUnsafeResult()
	{
		var content = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR"u8.ToArray();
		var service = CreateService(content, "application/pdf");
		var expectedHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

		var result = await service.ValidateObjectAsync(
			"organization/file.pdf",
			expectedHash,
			"application/pdf",
			content.Length
		);

		Assert.That(result.IsValid, Is.True);
		Assert.That(result.IsSafe, Is.False);
		Assert.That(result.ThreatName, Does.Contain("EICAR"));
	}

	[Test]
	public async Task DeleteObjectAsync_WhenStorageReturnsNotFound_IsSuccessful()
	{
		var handler = new StubHttpMessageHandler(() =>
			new HttpResponseMessage(HttpStatusCode.NotFound));
		var service = new R2FileStorageService(
			CreateSettings(),
			new StubHttpClientFactory(new HttpClient(handler)),
			new BasicFileContentScanner()
		);

		var result = await service.DeleteObjectAsync("organization/missing.pdf");

		Assert.That(result, Is.True);
	}

	private static R2FileStorageService CreateService(
		byte[] content,
		string contentType
	)
	{
		var handler = new StubHttpMessageHandler(() =>
		{
			var responseContent = new ByteArrayContent(content);
			responseContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = responseContent
			};
		});
		var factory = new StubHttpClientFactory(new HttpClient(handler));

		return new R2FileStorageService(
			CreateSettings(),
			factory,
			new BasicFileContentScanner()
		);
	}

	private static R2StorageSettings CreateSettings()
	{
		return new R2StorageSettings
		{
			AccountId = "account",
			AccessKeyId = "access-key",
			SecretAccessKey = "secret-key",
			BucketName = "bucket"
		};
	}

	private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return client;
		}
	}

	private sealed class StubHttpMessageHandler(
		Func<HttpResponseMessage> createResponse
	) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken
		)
		{
			return Task.FromResult(createResponse());
		}
	}
}
