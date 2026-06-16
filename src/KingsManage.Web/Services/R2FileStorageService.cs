using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KingsManage;
using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class R2FileStorageService : IFileStorageService
{
	private const string Algorithm = "AWS4-HMAC-SHA256";
	private const string Region = "auto";
	private const string Service = "s3";
	private const string PayloadHash = "UNSIGNED-PAYLOAD";

	private readonly R2StorageSettings _settings;

	public R2FileStorageService(R2StorageSettings settings)
	{
		_settings = settings;
	}

	public Task<FileStorageSignedUrl> CreateUploadUrlAsync(
		string storageKey,
		TimeSpan expiresIn,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(CreateSignedUrl("PUT", storageKey, expiresIn));
	}

	public Task<FileStorageSignedUrl> CreateDownloadUrlAsync(
		string storageKey,
		TimeSpan expiresIn,
		CancellationToken cancellationToken = default
	)
	{
		return Task.FromResult(CreateSignedUrl("GET", storageKey, expiresIn));
	}

	private FileStorageSignedUrl CreateSignedUrl(
		string method,
		string storageKey,
		TimeSpan expiresIn
	)
	{
		ValidateSettings();

		if (string.IsNullOrWhiteSpace(storageKey))
		{
			throw new InvalidOperationException("R2 storage key is required.");
		}

		var now = DateTime.UtcNow;
		var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
		var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
		var expiresSeconds = Math.Clamp((int)expiresIn.TotalSeconds, 1, 604800);
		var host = $"{_settings.AccountId}.r2.cloudflarestorage.com";
		var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
		var canonicalUri = $"/{UrlEncodePathSegment(_settings.BucketName)}/{EncodeStorageKey(storageKey)}";

		var queryParameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
		{
			["X-Amz-Algorithm"] = Algorithm,
			["X-Amz-Credential"] = $"{_settings.AccessKeyId}/{credentialScope}",
			["X-Amz-Date"] = amzDate,
			["X-Amz-Expires"] = expiresSeconds.ToString(CultureInfo.InvariantCulture),
			["X-Amz-SignedHeaders"] = "host"
		};

		var canonicalQueryString = BuildCanonicalQueryString(queryParameters);
		var canonicalHeaders = $"host:{host}\n";
		var signedHeaders = "host";
		var canonicalRequest = string.Join(
			"\n",
			method,
			canonicalUri,
			canonicalQueryString,
			canonicalHeaders,
			signedHeaders,
			PayloadHash
		);

		var stringToSign = string.Join(
			"\n",
			Algorithm,
			amzDate,
			credentialScope,
			ToHex(Sha256Hash(canonicalRequest))
		);

		var signingKey = GetSignatureKey(_settings.SecretAccessKey, dateStamp, Region, Service);
		var signature = ToHex(HmacSha256(signingKey, stringToSign));
		var url = $"https://{host}{canonicalUri}?{canonicalQueryString}&X-Amz-Signature={signature}";

		return new FileStorageSignedUrl
		{
			Url = url,
			ExpiresAtUtc = now.AddSeconds(expiresSeconds)
		};
	}

	private void ValidateSettings()
	{
		if (
			string.IsNullOrWhiteSpace(_settings.AccountId) ||
			string.IsNullOrWhiteSpace(_settings.AccessKeyId) ||
			string.IsNullOrWhiteSpace(_settings.SecretAccessKey) ||
			string.IsNullOrWhiteSpace(_settings.BucketName)
		)
		{
			throw new InvalidOperationException("R2 storage settings are missing.");
		}
	}

	private static string EncodeStorageKey(string storageKey)
	{
		return string.Join(
			"/",
			storageKey
				.Split('/', StringSplitOptions.RemoveEmptyEntries)
				.Select(UrlEncodePathSegment)
		);
	}

	private static string BuildCanonicalQueryString(SortedDictionary<string, string> queryParameters)
	{
		return string.Join(
			"&",
			queryParameters.Select(parameter =>
				$"{UrlEncodeQueryValue(parameter.Key)}={UrlEncodeQueryValue(parameter.Value)}")
		);
	}

	private static string UrlEncodePathSegment(string value)
	{
		return Uri.EscapeDataString(value).Replace("%2F", "/", StringComparison.Ordinal);
	}

	private static string UrlEncodeQueryValue(string value)
	{
		return Uri.EscapeDataString(value)
			.Replace("%20", "%20", StringComparison.Ordinal)
			.Replace("+", "%20", StringComparison.Ordinal);
	}

	private static byte[] Sha256Hash(string value)
	{
		return SHA256.HashData(Encoding.UTF8.GetBytes(value));
	}

	private static byte[] HmacSha256(byte[] key, string data)
	{
		using var hmac = new HMACSHA256(key);
		return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
	}

	private static byte[] HmacSha256(string key, string data)
	{
		return HmacSha256(Encoding.UTF8.GetBytes(key), data);
	}

	private static byte[] GetSignatureKey(
		string key,
		string dateStamp,
		string regionName,
		string serviceName
	)
	{
		var dateKey = HmacSha256($"AWS4{key}", dateStamp);
		var dateRegionKey = HmacSha256(dateKey, regionName);
		var dateRegionServiceKey = HmacSha256(dateRegionKey, serviceName);
		return HmacSha256(dateRegionServiceKey, "aws4_request");
	}

	private static string ToHex(byte[] bytes)
	{
		var builder = new StringBuilder(bytes.Length * 2);

		foreach (var value in bytes)
		{
			builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
		}

		return builder.ToString();
	}
}
